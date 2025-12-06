using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Kernlogica voor de due diligence-checklist (tab 7/8/11):
    /// - Bepaalt per checklist-vraag: toepasbaarheid, antwoord, bewijssamenvatting en einduitkomst.
    /// - Houdt beslissingen bij voor kolom K/M (Negatief resultaat acceptabel + afwijkingstekst),
    ///   met koppeling naar een persistente repository.
    /// - Biedt statische helpers die in de unit tests worden gebruikt.
    /// </summary>
    public class DueDiligenceService
    {
        /// <summary>
        /// In-memory cache van beslissingen per assessment + ChecklistId
        /// (kolom K: Negatief resultaat acceptabel, kolom M: DeviationText).
        /// </summary>
        private readonly Dictionary<(Guid AssessmentId, string ChecklistId),
            (bool NegativeOutcomeAcceptable, string? DeviationText)> _decisions =
                new();   // let op: GEEN StringComparer, tuple-key

        private readonly object _lock = new();

        private readonly IAssessmentDueDiligenceDecisionRepository? _decisionRepository;

        /// <summary>
        /// Parameterloze constructor voor o.a. bestaande unit tests.
        /// Beslissingen worden dan alleen in-memory bijgehouden.
        /// </summary>
        public DueDiligenceService()
            : this(null)
        {
        }

        /// <summary>
        /// Hoofdconstructor met optionele repository voor persistente opslag.
        /// In productie wordt deze via DI aangeroepen.
        /// </summary>
        public DueDiligenceService(IAssessmentDueDiligenceDecisionRepository? decisionRepository)
        {
            _decisionRepository = decisionRepository;
        }

        /// <summary>
        /// Wordt aangeroepen door de DueDiligenceController (POST /decision)
        /// om kolom K en M voor één checklist-vraag op te slaan.
        /// </summary>
        public void UpdateNegativeOutcomeDecision(
            Guid assessmentId,
            string checklistId,
            bool negativeOutcomeAcceptable,
            string? deviationText)
        {
            if (string.IsNullOrWhiteSpace(checklistId))
            {
                throw new ArgumentException("ChecklistId is required.", nameof(checklistId));
            }

            var key = (assessmentId, checklistId);

            // Altijd in-memory bijwerken
            lock (_lock)
            {
                _decisions[key] = (negativeOutcomeAcceptable, deviationText);
            }

            // En indien beschikbaar ook persistente opslag bijwerken
            _decisionRepository?.Upsert(assessmentId, checklistId, negativeOutcomeAcceptable, deviationText);
        }

        // =====================================================================
        //  CHECKLIST-OPBOUW
        // =====================================================================

        /// <summary>
        /// Bestaande overload: compatibel met bestaande tests/aanroepen.
        /// Toepasbaarheid wordt berekend zónder ToetsVooronderzoek (alles toepasbaar).
        /// </summary>
        public IReadOnlyList<DueDiligenceChecklistRow> BuildChecklistRows(
            Guid assessmentId,
            IEnumerable<ChecklistQuestionDefinition> definitions,
            IEnumerable<AssessmentQuestionAnswer> answers,
            IEnumerable<AssessmentEvidenceItem> evidenceItems)
        {
            return BuildChecklistRows(assessmentId, definitions, answers, evidenceItems, null);
        }

        /// <summary>
        /// Nieuwe overload: zelfde gedrag, maar met extra ToetsAnswers
        /// uit ToetsVooronderzoek om IsApplicable per vraag te bepalen.
        /// </summary>
        public IReadOnlyList<DueDiligenceChecklistRow> BuildChecklistRows(
            Guid assessmentId,
            IEnumerable<ChecklistQuestionDefinition> definitions,
            IEnumerable<AssessmentQuestionAnswer> answers,
            IEnumerable<AssessmentEvidenceItem> evidenceItems,
            IDictionary<string, bool?>? toetsAnswers)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (answers == null) throw new ArgumentNullException(nameof(answers));
            if (evidenceItems == null) throw new ArgumentNullException(nameof(evidenceItems));

            var defList = definitions.ToList();
            var answerList = answers
                .Where(a => a.AssessmentId == assessmentId)
                .ToList();
            var evidenceList = evidenceItems
                .Where(e => e.AssessmentId == assessmentId)
                .ToList();

            // Laad eventuele bestaande beslissingen voor dit assessment uit de repository
            LoadDecisionsFromRepository(assessmentId);

            var rows = new List<DueDiligenceChecklistRow>();

            foreach (var def in defList)
            {
                var answer = answerList.FirstOrDefault(a =>
                    string.Equals(a.ChecklistId, def.ChecklistId, StringComparison.OrdinalIgnoreCase));

                // NIEUW: toepasbaarheid bepalen op basis van ToetsIds + ToetsAnswers
                var isApplicable = DetermineApplicability(def, toetsAnswers);

                var rawAnswer = answer?.RawAnswer;
                var answerEvaluation = answer?.AnswerEvaluation;

                // Evidence voor deze vraag
                var evidenceForQuestion = evidenceList
                    .Where(e => string.Equals(e.ChecklistId, def.ChecklistId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Alle aanwezige EvidenceId’s als "verplicht" behandelen
                var requiredEvidenceIds = evidenceForQuestion
                    .Where(e => !string.IsNullOrWhiteSpace(e.EvidenceId))
                    .Select(e => e.EvidenceId!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var evidenceSummary = ComputeEvidenceResultLabel(requiredEvidenceIds, evidenceForQuestion);

                // Beslissing kolom K/M uit cache (die gesynchroniseerd wordt met de repository)
                bool negativeOutcomeAcceptable = false;
                string? deviationText = null;

                var key = (assessmentId, def.ChecklistId);
                lock (_lock)
                {
                    if (_decisions.TryGetValue(key, out var decision))
                    {
                        negativeOutcomeAcceptable = decision.NegativeOutcomeAcceptable;
                        deviationText = decision.DeviationText;
                    }
                }

                var outcome = ComputeDueDiligenceOutcome(
                    isApplicable,
                    answerEvaluation,
                    evidenceSummary,
                    negativeOutcomeAcceptable);

                rows.Add(new DueDiligenceChecklistRow
                {
                    AssessmentId = assessmentId,
                    ChecklistId = def.ChecklistId,
                    IsApplicable = isApplicable,
                    Answer = rawAnswer,
                    AnswerEvaluation = answerEvaluation,
                    EvidenceSummary = evidenceSummary,
                    NegativeOutcomeAcceptable = negativeOutcomeAcceptable,
                    DueDiligenceOutcome = outcome,
                    DeviationText = deviationText
                });
            }

            return rows;
        }

        /// <summary>
        /// Bepaalt of een checklist-vraag van toepassing is op basis van
        /// de ToetsIds in de checklist-definitie en de ToetsAnswers (tab 6).
        /// </summary>
        private static bool DetermineApplicability(
            ChecklistQuestionDefinition definition,
            IDictionary<string, bool?>? toetsAnswers)
        {
            // Geen ToetsVooronderzoek bekend -> behoud bestaand gedrag (alles toepasbaar)
            if (toetsAnswers == null || toetsAnswers.Count == 0)
            {
                return true;
            }

            // Geen koppeling met tab 6 -> altijd toepasbaar
            if (definition.ToetsIds == null || !definition.ToetsIds.Any())
            {
                return true;
            }

            var values = new List<bool?>();

            foreach (var toetsId in definition.ToetsIds)
            {
                if (string.IsNullOrWhiteSpace(toetsId))
                    continue;

                if (toetsAnswers.TryGetValue(toetsId, out var value))
                {
                    values.Add(value);
                }
            }

            // Geen matchende toetsen gevonden -> conservatief toepasbaar houden
            if (values.Count == 0)
            {
                return true;
            }

            bool anyYes = values.Any(v => v == true);
            bool allNo = values.All(v => v == false);

            if (anyYes)
            {
                // Ten minste één gerelateerde toets is "Ja" -> vraag is van toepassing
                return true;
            }

            if (allNo)
            {
                // Alle gerelateerde toetsen zijn "Nee" -> vraag niet van toepassing
                return false;
            }

            // Mix van Nee en Onbekend -> we kiezen hier voor "Niet van toepassing"
            // zodra alle bekende antwoorden Nee zijn.
            return false;
        }

        /// <summary>
        /// Synchroniseert de in-memory cache _decisions met de repository
        /// voor één assessment (indien een repository aanwezig is).
        /// </summary>
        private void LoadDecisionsFromRepository(Guid assessmentId)
        {
            if (_decisionRepository == null)
            {
                return;
            }

            var itemsEnumerable = _decisionRepository.GetByAssessment(assessmentId);
            if (itemsEnumerable == null)
            {
                return;
            }

            var items = itemsEnumerable.ToList();
            if (!items.Any())
            {
                return;
            }

            lock (_lock)
            {
                // Verwijder eventuele oude entries voor dit assessment
                var keysToRemove = _decisions.Keys
                    .Where(k => k.AssessmentId == assessmentId)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _decisions.Remove(key);
                }

                // Voeg de beslissingen uit de repository toe aan de cache
                foreach (var item in items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.ChecklistId))
                    {
                        continue;
                    }

                    var key = (item.AssessmentId, item.ChecklistId);
                    _decisions[key] = (item.NegativeOutcomeAcceptable, item.DeviationText);
                }
            }
        }

        // =====================================================================
        //  BEWIJS-LOGICA (tab 9/11) – SAMENVATTING PER CHECKLIST-VRAAG
        // =====================================================================

        private static string ComputeEvidenceResultLabel(
            IReadOnlyCollection<string> requiredEvidenceIds,
            IReadOnlyCollection<AssessmentEvidenceItem> evidenceItems)
        {
            // Geen verplicht bewijs → direct "Geen bewijs vereist"
            if (requiredEvidenceIds == null || requiredEvidenceIds.Count == 0)
            {
                return "Geen bewijs vereist";
            }

            var evidenceList = evidenceItems?.ToList() ?? new List<AssessmentEvidenceItem>();

            // Filter op verplichte EvidenceId's
            var relevantItems = evidenceList
                .Where(e => !string.IsNullOrWhiteSpace(e.EvidenceId))
                .Where(e => requiredEvidenceIds.Contains(e.EvidenceId!, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (!relevantItems.Any())
            {
                return "Nog niet aangeleverd";
            }

            string Norm(string? status)
            {
                if (string.IsNullOrWhiteSpace(status))
                    return "Onbekend";

                var v = status.Trim().ToLowerInvariant();
                return v switch
                {
                    "goedgekeurd" => "Goedgekeurd",
                    "in beoordeling" => "In beoordeling",
                    "niet aangeleverd" => "Niet aangeleverd",
                    "afgekeurd" => "Afgekeurd",
                    _ => "Onbekend"
                };
            }

            var normalized = relevantItems
                .Select(e => Norm(e.Status))
                .ToList();

            var hasApproved = normalized.Contains("Goedgekeurd");
            var hasRejected = normalized.Contains("Afgekeurd");
            var hasInReview = normalized.Contains("In beoordeling");
            var hasUnknownOrMissing = normalized.Contains("Onbekend") || normalized.Contains("Niet aangeleverd");

            if (hasRejected)
            {
                return "Onvoldoende bewijs";
            }

            if (hasApproved && !hasInReview && !hasUnknownOrMissing)
            {
                return "Voldoende bewijs";
            }

            if (hasInReview && !hasRejected)
            {
                return "In beoordeling";
            }

            if (hasApproved || hasInReview)
            {
                return "Deels aangeleverd";
            }

            return "Nog niet aangeleverd";
        }

        // =====================================================================
        //  EINDUITKOMST DUE DILIGENCE (kolom L)
        // =====================================================================

        private static string? ComputeDueDiligenceOutcome(
            bool isApplicable,
            string? answerEvaluation,
            string? evidenceResultLabel,
            bool negativeOutcomeAcceptable)
        {
            if (!isApplicable)
            {
                return "Niet van toepassing";
            }

            string normAnswerEval = NormalizeAnswerEvaluation(answerEvaluation);
            string normEvidence = NormalizeEvidenceResultLabel(evidenceResultLabel);

            bool HasBadAnswer(string v) =>
                v == "Afgekeurd" || v == "Voldoet niet";

            bool HasGoodAnswer(string v) =>
                v == "Goedgekeurd" || v == "Voldoet";

            if (string.IsNullOrEmpty(normAnswerEval) &&
                (normEvidence == "" ||
                 normEvidence == "Nog te beoordelen" ||
                 normEvidence == "Nog niet aangeleverd" ||
                 normEvidence == "In beoordeling"))
            {
                return "Nog te beoordelen";
            }

            if (HasBadAnswer(normAnswerEval) ||
                normEvidence == "Onvoldoende bewijs")
            {
                return negativeOutcomeAcceptable
                    ? "Afwijking acceptabel"
                    : "Voldoet niet";
            }

            if (normEvidence == "In beoordeling")
            {
                return "Nog te beoordelen";
            }

            if (HasGoodAnswer(normAnswerEval) ||
                normEvidence == "Voldoende bewijs" ||
                normEvidence == "Geen bewijs vereist")
            {
                return "Voldoet";
            }

            if (normEvidence == "Deels aangeleverd")
            {
                return "Nog te beoordelen";
            }

            return "Nog te beoordelen";
        }

        private static string NormalizeAnswerEvaluation(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var v = value.Trim().ToLowerInvariant();
            return v switch
            {
                "goedgekeurd" => "Goedgekeurd",
                "deels goedgekeurd" => "Deels goedgekeurd",
                "afgekeurd" => "Afgekeurd",
                "voldoet" => "Voldoet",
                "voldoet niet" => "Voldoet niet",
                _ => value.Trim()
            };
        }

        private static string NormalizeEvidenceResultLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var v = value.Trim().ToLowerInvariant();
            return v switch
            {
                "geen bewijs vereist" => "Geen bewijs vereist",
                "nog niet aangeleverd" => "Nog niet aangeleverd",
                "deels aangeleverd" => "Deels aangeleverd",
                "in beoordeling" => "In beoordeling",
                "voldoende bewijs" => "Voldoende bewijs",
                "onvoldoende bewijs" => "Onvoldoende bewijs",
                _ => value.Trim()
            };
        }

        // =====================================================================
        //  STATIC WRAPPERS VOOR BESTAANDE UNITTESTS
        // =====================================================================

        public static string SummarizeEvidenceStatus(
            IReadOnlyCollection<string> requiredEvidenceIds)
        {
            return ComputeEvidenceResultLabel(
                requiredEvidenceIds ?? Array.Empty<string>(),
                Array.Empty<AssessmentEvidenceItem>());
        }

        public static string SummarizeEvidenceStatus(
            IReadOnlyCollection<string> requiredEvidenceIds,
            IReadOnlyCollection<AssessmentEvidenceItem> evidenceItems)
        {
            return ComputeEvidenceResultLabel(
                requiredEvidenceIds ?? Array.Empty<string>(),
                evidenceItems ?? Array.Empty<AssessmentEvidenceItem>());
        }

        public static string SummarizeEvidenceStatus(
            IReadOnlyCollection<AssessmentEvidenceItem> evidenceItems)
        {
            if (evidenceItems == null)
            {
                throw new ArgumentNullException(nameof(evidenceItems));
            }

            var list = evidenceItems.ToList();

            var requiredEvidenceIds = list
                .Where(e => !string.IsNullOrWhiteSpace(e.EvidenceId))
                .Select(e => e.EvidenceId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!requiredEvidenceIds.Any() && list.Count > 0)
            {
                requiredEvidenceIds.Add("AUTO-GENERATED");
            }

            return ComputeEvidenceResultLabel(requiredEvidenceIds, list);
        }

        public static string? EvaluateDueDiligenceOutcome(
            bool isApplicable,
            string? evidenceResultLabel,
            bool negativeOutcomeAcceptable)
        {
            return ComputeDueDiligenceOutcome(
                isApplicable,
                answerEvaluation: null,
                evidenceResultLabel: evidenceResultLabel,
                negativeOutcomeAcceptable: negativeOutcomeAcceptable);
        }

        public static string? EvaluateDueDiligenceOutcome(
            bool isApplicable,
            string? answerEvaluation,
            string? evidenceResultLabel,
            bool negativeOutcomeAcceptable)
        {
            return ComputeDueDiligenceOutcome(
                isApplicable,
                answerEvaluation,
                evidenceResultLabel,
                negativeOutcomeAcceptable);
        }

        public static string? EvaluateDueDiligenceOutcome(string? evidenceResultLabel)
        {
            return ComputeDueDiligenceOutcome(
                isApplicable: true,
                answerEvaluation: null,
                evidenceResultLabel: evidenceResultLabel,
                negativeOutcomeAcceptable: false);
        }

        /// <summary>
        /// Overload die de oude tests verwacht: neemt een AssessmentChecklistRow
        /// en vertaalt die naar de nieuwe ComputeDueDiligenceOutcome-logica.
        /// </summary>
        public static string? EvaluateDueDiligenceOutcome(AssessmentChecklistRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            return ComputeDueDiligenceOutcome(
                isApplicable: row.IsApplicable,
                answerEvaluation: row.AnswerEvaluation,
                evidenceResultLabel: null,
                negativeOutcomeAcceptable: row.NegativeOutcomeAcceptable);
        }

        // =====================================================================
        //  SAMENVATTING OVER ALLE VRAGEN (VOORTGANG + EINDOORDEEL)
        // =====================================================================

        /// <summary>
        /// Bestaande overload: voor tests en bestaande code (zonder ToetsAnswers).
        /// </summary>
        public DueDiligenceSummary BuildSummary(
            Guid assessmentId,
            IEnumerable<ChecklistQuestionDefinition> definitions,
            IEnumerable<AssessmentQuestionAnswer> answers,
            IEnumerable<AssessmentEvidenceItem> evidenceItems)
        {
            return BuildSummary(assessmentId, definitions, answers, evidenceItems, null);
        }

        /// <summary>
        /// Nieuwe overload: gebruikt dezelfde IsApplicable-logica als de checklist.
        /// </summary>
        public DueDiligenceSummary BuildSummary(
            Guid assessmentId,
            IEnumerable<ChecklistQuestionDefinition> definitions,
            IEnumerable<AssessmentQuestionAnswer> answers,
            IEnumerable<AssessmentEvidenceItem> evidenceItems,
            IDictionary<string, bool?>? toetsAnswers)
        {
            var rows = BuildChecklistRows(assessmentId, definitions, answers, evidenceItems, toetsAnswers);

            int total = rows.Count;
            int applicable = rows.Count(r => r.IsApplicable);
            int completed = rows.Count(r =>
                r.DueDiligenceOutcome != null &&
                r.DueDiligenceOutcome != "Nog te beoordelen");

            string progressStatus;
            if (completed == 0)
            {
                progressStatus = "not_started";
            }
            else if (completed < applicable)
            {
                progressStatus = "in_progress";
            }
            else
            {
                progressStatus = "completed";
            }

            string? overallOutcome = null;
            bool hasBlocking = false;

            if (rows.Any())
            {
                bool anyNotAcceptable = rows.Any(r => r.DueDiligenceOutcome == "Voldoet niet");
                bool anyAcceptableDeviation = rows.Any(r => r.DueDiligenceOutcome == "Afwijking acceptabel");
                bool allGoodOrNvt = rows
                    .Where(r => r.IsApplicable)
                    .All(r =>
                        r.DueDiligenceOutcome == "Voldoet" ||
                        r.DueDiligenceOutcome == "Niet van toepassing");

                if (anyNotAcceptable)
                {
                    overallOutcome = "Niet acceptabel";
                    hasBlocking = true;
                }
                else if (anyAcceptableDeviation)
                {
                    overallOutcome = "Afwijking acceptabel";
                }
                else if (allGoodOrNvt && applicable > 0)
                {
                    overallOutcome = "Voldoet";
                }
                else
                {
                    overallOutcome = "Nog te beoordelen";
                }
            }

            return new DueDiligenceSummary
            {
                AssessmentId = assessmentId,
                TotalQuestions = total,
                ApplicableQuestions = applicable,
                CompletedQuestions = completed,
                ProgressStatus = progressStatus,
                OverallOutcome = overallOutcome,
                HasBlockingFindings = hasBlocking
            };
        }
    }

    /// <summary>
    /// Interne representatie van één due diligence-rij vóór de API-DTO.
    /// </summary>
    public class DueDiligenceChecklistRow
    {
        public Guid AssessmentId { get; set; }
        public string ChecklistId { get; set; } = string.Empty;

        public bool IsApplicable { get; set; }

        public string? Answer { get; set; }
        public string? AnswerEvaluation { get; set; }

        public string? EvidenceSummary { get; set; }

        public bool NegativeOutcomeAcceptable { get; set; }

        public string? DueDiligenceOutcome { get; set; }

        public string? DeviationText { get; set; }
    }

    /// <summary>
    /// Samenvatting over de hele due diligence (tab 7) voor één assessment.
    /// </summary>
    public class DueDiligenceSummary
    {
        public Guid AssessmentId { get; set; }
        public int TotalQuestions { get; set; }
        public int ApplicableQuestions { get; set; }
        public int CompletedQuestions { get; set; }

        /// <summary>
        /// "not_started", "in_progress", "completed".
        /// </summary>
        public string ProgressStatus { get; set; } = "not_started";

        /// <summary>
        /// "Nog te beoordelen", "Voldoet", "Niet acceptabel", "Afwijking acceptabel".
        /// </summary>
        public string? OverallOutcome { get; set; }

        /// <summary>
        /// True als er minimaal één "Voldoet niet" in kolom L staat.
        /// </summary>
        public bool HasBlockingFindings { get; set; }
    }
}
