using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Service die de logica van tab 7 (due diligence) afhandelt.
    /// In deze versie:
    /// - koppelen we definities + antwoorden,
    /// - berekenen we Toepasselijk? (kolom F) o.b.v. ToetsVooronderzoek,
    /// - vullen we Antwoord (kolom G) en ControlevraagResultaat (kolom I),
    /// - aggregeren we bewijslast naar BewijsResultaat (kolom J),
    /// - bewaren we beslissingen over "Negatief resultaat acceptabel?" en Afwijkingstekst (kolom K en M)
    ///   PERSISTENT in een JSON-bestand,
    /// - berekenen we Resultaat due diligence (kolom L).
    ///
    /// Nieuw:
    /// - antwoorden (tab 8) komen uit IAssessmentAnswersRepository,
    /// - bewijslast (tab 11) komt uit IAssessmentEvidenceRepository.
    /// </summary>
    public class DueDiligenceService
    {
        private readonly AssessmentService _assessmentService;
        private readonly ToetsVooronderzoekService _toetsVooronderzoekService;
        private readonly IAssessmentAnswersRepository _answersRepository;
        private readonly IAssessmentEvidenceRepository _evidenceRepository;

        // In-memory opslag van beslissingen per Assessment + ChecklistID
        // voor kolom K (NegativeOutcomeAcceptable) en kolom M (DeviationText).
        private readonly Dictionary<string, ChecklistDecisionState> _decisionStates =
            new Dictionary<string, ChecklistDecisionState>(StringComparer.OrdinalIgnoreCase);

        // Bestand voor persistente opslag
        private const string DecisionsFileName = "Data/due-diligence-decisions.json";
        private readonly object _syncRoot = new object();

        public DueDiligenceService(
            AssessmentService assessmentService,
            ToetsVooronderzoekService toetsVooronderzoekService,
            IAssessmentAnswersRepository answersRepository,
            IAssessmentEvidenceRepository evidenceRepository)
        {
            _assessmentService = assessmentService ?? throw new ArgumentNullException(nameof(assessmentService));
            _toetsVooronderzoekService = toetsVooronderzoekService ?? throw new ArgumentNullException(nameof(toetsVooronderzoekService));
            _answersRepository = answersRepository ?? throw new ArgumentNullException(nameof(answersRepository));
            _evidenceRepository = evidenceRepository ?? throw new ArgumentNullException(nameof(evidenceRepository));

            // Bij het opstarten van de service proberen we bestaande beslissingen te laden.
            LoadDecisionStatesFromDisk();
        }

        /// <summary>
        /// Publiek entrypoint:
        /// - haalt antwoorden (tab 8) op via IAssessmentAnswersRepository,
        /// - haalt bewijslast (tab 11) op via IAssessmentEvidenceRepository,
        /// - combineert dat met definities en ToetsVooronderzoek om tab 7-rijen te bouwen.
        /// </summary>
        public List<AssessmentChecklistRow> BuildChecklistRows(
            Guid assessmentId,
            IEnumerable<ChecklistQuestionDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            var answers = _answersRepository.GetByAssessment(assessmentId);
            var evidenceItems = _evidenceRepository.GetByAssessment(assessmentId);

            return BuildChecklistRowsInternal(assessmentId, definitions, answers, evidenceItems);
        }

        /// <summary>
        /// Interne implementatie die de "tab 7-rijen" voor een assessment opbouwt op basis van:
        /// - de checklist-definities (statisch),
        /// - de gegeven antwoorden (tab 8-laag),
        /// - de bewijslast-items (tab 11-laag),
        /// - de beslissingen over kolom K/M,
        /// - het ToetsVooronderzoek-resultaat.
        /// </summary>
        private List<AssessmentChecklistRow> BuildChecklistRowsInternal(
            Guid assessmentId,
            IEnumerable<ChecklistQuestionDefinition> definitions,
            IEnumerable<AssessmentQuestionAnswer> answers,
            IEnumerable<AssessmentEvidenceItem> evidenceItems)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (answers == null) throw new ArgumentNullException(nameof(answers));
            if (evidenceItems == null) throw new ArgumentNullException(nameof(evidenceItems));

            var assessment = _assessmentService.GetById(assessmentId)
                              ?? throw new InvalidOperationException($"Assessment {assessmentId} not found.");

            // ToetsVooronderzoek-resultaat voor dit assessment (tab 6 in de excel).
            var toetsResult = _toetsVooronderzoekService.Get(assessmentId);

            var answerLookup = answers
                .Where(a => a.AssessmentId == assessmentId)
                .GroupBy(a => a.ChecklistId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var evidenceLookup = evidenceItems
                .Where(e => e.AssessmentId == assessmentId)
                .GroupBy(e => e.ChecklistId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var rows = new List<AssessmentChecklistRow>();

            foreach (var def in definitions)
            {
                answerLookup.TryGetValue(def.ChecklistId, out var answer);
                evidenceLookup.TryGetValue(def.ChecklistId, out var evidenceForQuestion);

                var isApplicable = EvaluateApplicability(assessment, def, toetsResult);
                var evidenceSummary = SummarizeEvidenceStatus(evidenceForQuestion);

                // Haal eventuele eerder opgeslagen beslissing voor kolom K/M op
                var decision = GetDecisionState(assessmentId, def.ChecklistId);

                var row = new AssessmentChecklistRow
                {
                    AssessmentId = assessmentId,
                    ChecklistId = def.ChecklistId,

                    // Kolom F: Toepasselijk?
                    IsApplicable = isApplicable,

                    // Kolom G: Antwoord
                    Answer = answer?.RawAnswer,

                    // Kolom I: ControlevraagResultaat
                    AnswerEvaluation = answer?.AnswerEvaluation,

                    // Kolom J: BewijsResultaat (samenvatting van bewijslast)
                    EvidenceSummary = evidenceSummary,

                    // Kolom K: Negatief resultaat acceptabel?
                    NegativeOutcomeAcceptable = decision?.NegativeOutcomeAcceptable ?? false,

                    // Kolom L: Resultaat due diligence (wordt hieronder berekend)
                    DueDiligenceOutcome = null,

                    // Kolom M: Afwijkingstekst (contract)
                    DeviationText = decision?.DeviationText
                };

                // Kolom L berekenen op basis van F (IsApplicable), I (AnswerEvaluation),
                // J (EvidenceSummary) en K (NegativeOutcomeAcceptable).
                row.DueDiligenceOutcome = EvaluateDueDiligenceOutcome(row);

                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// Update kolom K en M voor één vraag:
        /// - K: Negatief resultaat acceptabel? (true/false)
        /// - M: Afwijkingstekst (contract)
        ///
        /// Wijzigingen worden PERSISTENT opgeslagen in een JSON-bestand.
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

            lock (_syncRoot)
            {
                var key = BuildDecisionKey(assessmentId, checklistId);

                _decisionStates[key] = new ChecklistDecisionState
                {
                    AssessmentId = assessmentId,
                    ChecklistId = checklistId,
                    NegativeOutcomeAcceptable = negativeOutcomeAcceptable,
                    DeviationText = deviationText
                };

                SaveDecisionStatesToDisk();
            }
        }

        /// <summary>
        /// Bepaalt Toepasselijk? (kolom F) voor één vraag, op basis van:
        /// - de ToetsIDs die aan de vraag gekoppeld zijn (def.ToetsIds),
        /// - de uitkomsten per ToetsID in ToetsVooronderzoekResult.ToetsAnswers.
        ///
        /// Eerste versie:
        /// - Als geen ToetsIDs zijn gekoppeld -> default true (vraag is altijd van toepassing).
        /// - Als minstens één gekoppelde Toets "Ja" is -> true.
        /// - Als alle bekende ToetsIDs "Nee" zijn -> false.
        /// - Als alles onbekend/ontbrekend is -> false (conservatief niet-toepasselijk voor nu).
        /// </summary>
        private static bool EvaluateApplicability(
            Assessment assessment,
            ChecklistQuestionDefinition def,
            ToetsVooronderzoekResult toetsResult)
        {
            if (def.ToetsIds == null || def.ToetsIds.Length == 0)
            {
                return true;
            }

            var anyTrue = false;
            var anyFalse = false;

            foreach (var rawId in def.ToetsIds)
            {
                if (string.IsNullOrWhiteSpace(rawId))
                    continue;

                var toetsId = rawId.Trim();

                bool? value = null;

                if (toetsResult.ToetsAnswers != null &&
                    toetsResult.ToetsAnswers.TryGetValue(toetsId, out var stored))
                {
                    value = stored;
                }
                else
                {
                    var q = toetsResult.Questions.FirstOrDefault(
                        x => x.ToetsId.Equals(toetsId, StringComparison.OrdinalIgnoreCase));
                    value = q?.Answer;
                }

                if (!value.HasValue)
                {
                    continue;
                }

                if (value.Value)
                {
                    anyTrue = true;
                }
                else
                {
                    anyFalse = true;
                }
            }

            if (anyTrue)
            {
                return true;
            }

            if (anyFalse)
            {
                return false;
            }

            // Alles onbekend of alleen lege ToetsIDs -> niet toepasbaar.
            return false;
        }

        /// <summary>
        /// Aggegreert de statussen van alle bewijslast-items voor één vraag naar
        /// een samenvattende tekst in kolom J.
        ///
        /// Publiek gemaakt zodat we deze logica gericht kunnen unit-testen.
        /// </summary>
        public static string? SummarizeEvidenceStatus(IReadOnlyCollection<AssessmentEvidenceItem>? evidenceItems)
        {
            if (evidenceItems == null || evidenceItems.Count == 0)
            {
                // Geen bewijslastregels gekoppeld aan deze vraag.
                return "Geen bewijs vereist";
            }

            var anyAfgekeurd = false;
            var anyInBeoordeling = false;
            var anyNietAangeleverd = false;
            var anyGoedgekeurd = false;

            foreach (var item in evidenceItems)
            {
                var status = (item.Status ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(status))
                {
                    // Lege status -> behandelen als "Niet aangeleverd"
                    anyNietAangeleverd = true;
                    continue;
                }

                if (status.Equals("Afgekeurd", StringComparison.OrdinalIgnoreCase))
                {
                    anyAfgekeurd = true;
                }
                else if (status.Equals("In beoordeling", StringComparison.OrdinalIgnoreCase))
                {
                    anyInBeoordeling = true;
                }
                else if (status.Equals("Niet aangeleverd", StringComparison.OrdinalIgnoreCase))
                {
                    anyNietAangeleverd = true;
                }
                else if (status.Equals("Goedgekeurd", StringComparison.OrdinalIgnoreCase))
                {
                    anyGoedgekeurd = true;
                }
                else
                {
                    // Onbekende status: behandelen als "In beoordeling"
                    anyInBeoordeling = true;
                }
            }

            // Excel-logica: elke Afgekeurd domineert -> "Onvoldoende (afgekeurd)"
            if (anyAfgekeurd)
            {
                return "Onvoldoende (afgekeurd)";
            }

            // Daarna In beoordeling
            if (anyInBeoordeling)
            {
                return "In beoordeling";
            }

            // Daarna Niet aangeleverd (bijv. mix van Goedgekeurd + Niet aangeleverd)
            if (anyNietAangeleverd)
            {
                return "Niet aangeleverd";
            }

            // Als we hier zijn en er zijn items, en niemand is afgekeurd / in beoordeling /
            // niet aangeleverd, dan zijn ze allemaal goedgekeurd.
            if (anyGoedgekeurd)
            {
                return "Compleet (alles goedgekeurd)";
            }

            // Fallback: er zijn items, maar geen herkenbare status -> behandelen als "Niet aangeleverd".
            return "Niet aangeleverd";
        }

        /// <summary>
        /// Berekent Resultaat due diligence (kolom L) volgens de Excel-LET-logica.
        ///
        /// Publiek gemaakt zodat we deze logica gericht kunnen unit-testen.
        /// </summary>
        public static string? EvaluateDueDiligenceOutcome(AssessmentChecklistRow row)
        {
            // F (Toepasselijk?) is false -> in Excel wordt L dan leeg.
            if (!row.IsApplicable)
            {
                return null;
            }

            var ans = row.AnswerEvaluation ?? string.Empty;
            var ev = row.EvidenceSummary ?? string.Empty;
            var acc = row.NegativeOutcomeAcceptable;

            var ansPos =
                ans.Equals("Goedgekeurd", StringComparison.OrdinalIgnoreCase) ||
                ans.Equals("Deels goedgekeurd", StringComparison.OrdinalIgnoreCase);

            var ansNeg =
                ans.Equals("Afgekeurd", StringComparison.OrdinalIgnoreCase);

            var evOk =
                ev.Equals("Compleet (alles goedgekeurd)", StringComparison.OrdinalIgnoreCase) ||
                ev.Equals("Geen bewijs vereist", StringComparison.OrdinalIgnoreCase);

            var evBad =
                ev.Equals("Onvoldoende (afgekeurd)", StringComparison.OrdinalIgnoreCase);

            // OR(_ansNeg; _evBad)
            if (ansNeg || evBad)
            {
                // IF(_acc="Ja";"Afwijking acceptabel";"Niet acceptabel");
                if (acc)
                {
                    return "Afwijking acceptabel";
                }

                return "Niet acceptabel";
            }

            // IF(AND(_ansPos;_evOK);"OK";"Nog te beoordelen")
            if (ansPos && evOk)
            {
                return "OK";
            }

            return "Nog te beoordelen";
        }

        // -------------------------------
        // Persistente opslag voor K/M
        // -------------------------------

        private ChecklistDecisionState? GetDecisionState(Guid assessmentId, string checklistId)
        {
            var key = BuildDecisionKey(assessmentId, checklistId);

            lock (_syncRoot)
            {
                return _decisionStates.TryGetValue(key, out var state) ? state : null;
            }
        }

        private static string BuildDecisionKey(Guid assessmentId, string checklistId)
        {
            return $"{assessmentId:N}|{checklistId}";
        }

        private void LoadDecisionStatesFromDisk()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, DecisionsFileName);

                if (!File.Exists(filePath))
                {
                    return;
                }

                var json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var records = JsonSerializer.Deserialize<List<ChecklistDecisionRecord>>(json, options);
                if (records == null)
                {
                    return;
                }

                lock (_syncRoot)
                {
                    _decisionStates.Clear();

                    foreach (var r in records)
                    {
                        var key = BuildDecisionKey(r.AssessmentId, r.ChecklistId ?? string.Empty);

                        _decisionStates[key] = new ChecklistDecisionState
                        {
                            AssessmentId = r.AssessmentId,
                            ChecklistId = r.ChecklistId ?? string.Empty,
                            NegativeOutcomeAcceptable = r.NegativeOutcomeAcceptable,
                            DeviationText = r.DeviationText
                        };
                    }
                }
            }
            catch
            {
                // Fouten bij lezen/parse negeren; we starten dan met een lege set.
                // In een latere versie kun je hier logging toevoegen.
            }
        }

        private void SaveDecisionStatesToDisk()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, DecisionsFileName);

                List<ChecklistDecisionRecord> records;

                lock (_syncRoot)
                {
                    records = new List<ChecklistDecisionRecord>();

                    foreach (var kvp in _decisionStates)
                    {
                        var s = kvp.Value;
                        records.Add(new ChecklistDecisionRecord
                        {
                            AssessmentId = s.AssessmentId,
                            ChecklistId = s.ChecklistId,
                            NegativeOutcomeAcceptable = s.NegativeOutcomeAcceptable,
                            DeviationText = s.DeviationText
                        });
                    }
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(records, options);

                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Fouten bij schrijven negeren; beslissingen blijven in memory.
                // Later kun je hier logging toevoegen.
            }
        }

        private class ChecklistDecisionState
        {
            public Guid AssessmentId { get; set; }
            public string ChecklistId { get; set; } = string.Empty;
            public bool NegativeOutcomeAcceptable { get; set; }
            public string? DeviationText { get; set; }
        }

        private class ChecklistDecisionRecord
        {
            public Guid AssessmentId { get; set; }
            public string? ChecklistId { get; set; }
            public bool NegativeOutcomeAcceptable { get; set; }
            public string? DeviationText { get; set; }
        }
    }
}
