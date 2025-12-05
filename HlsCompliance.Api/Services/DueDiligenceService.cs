using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Kernlogica voor de due diligence-checklist (tab 7/8/11):
    /// - Bepaalt per checklist-vraag: toepasbaarheid, antwoord, bewijssamenvatting en einduitkomst.
    /// - Houdt in-memory beslissingen bij voor kolom K/M (Negatief resultaat acceptabel + afwijkingstekst).
    /// - Biedt statische helpers die in de unit tests worden gebruikt.
    /// </summary>
    public class DueDiligenceService
    {
        /// <summary>
        /// In-memory opslag van beslissingen per assessment + ChecklistId
        /// (kolom K: Negatief resultaat acceptabel, kolom M: DeviationText).
        /// NB: nog niet persistent; gaat verloren bij herstart van de API.
        /// </summary>
        private readonly Dictionary<(Guid AssessmentId, string ChecklistId),
            (bool NegativeOutcomeAcceptable, string? DeviationText)> _decisions =
                new();   // let op: GEEN StringComparer, tuple-key

        private readonly object _lock = new();

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

            lock (_lock)
            {
                _decisions[key] = (negativeOutcomeAcceptable, deviationText);
            }
        }

        /// <summary>
        /// Bouwt de checklist-rijen voor één assessment op basis van:
        /// - statische definities (tab 7),
        /// - antwoorden (tab 8),
        /// - bewijslast (tab 11).
        /// </summary>
        public IReadOnlyList<DueDiligenceChecklistRow> BuildChecklistRows(
            Guid assessmentId,
            IEnumerable<ChecklistQuestionDefinition> definitions,
            IEnumerable<AssessmentQuestionAnswer> answers,
            IEnumerable<AssessmentEvidenceItem> evidenceItems)
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

            var rows = new List<DueDiligenceChecklistRow>();

            foreach (var def in defList)
            {
                var answer = answerList.FirstOrDefault(a =>
                    string.Equals(a.ChecklistId, def.ChecklistId, StringComparison.OrdinalIgnoreCase));

                // Voor nu: alle vragen zijn toepasbaar.
                var isApplicable = true;

                var rawAnswer = answer?.RawAnswer;
                var answerEvaluation = answer?.AnswerEvaluation;

                // Evidence voor deze vraag
                var evidenceForQuestion = evidenceList
                    .Where(e => string.Equals(e.ChecklistId, def.ChecklistId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Alle aanwezige EvidenceId’s als "verplicht" behandelen
                var requiredEvidenceIds = evidenceForQuestion
                    .Where(e => !string.IsNullOrWhiteSpace(e.EvidenceId))
                    .Select(e => e.EvidenceId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var evidenceSummary = ComputeEvidenceResultLabel(requiredEvidenceIds, evidenceForQuestion);

                // Beslissing kolom K/M uit in-memory store
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

    // AssessmentChecklistRow heeft geen expliciete evidence-label property meer.
    // Voor deze overload gebruiken we alleen:
    // - IsApplicable
    // - AnswerEvaluation
    // - NegativeOutcomeAcceptable
    // EvidenceResultLabel laten we hier leeg (null).
    return ComputeDueDiligenceOutcome(
        isApplicable: row.IsApplicable,
        answerEvaluation: row.AnswerEvaluation,
        evidenceResultLabel: null,
        negativeOutcomeAcceptable: row.NegativeOutcomeAcceptable);
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
}
