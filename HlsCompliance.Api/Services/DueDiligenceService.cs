using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Service die de logica van tab 7 (due diligence) gaat afhandelen.
    /// In deze versie:
    /// - koppelen we definities + antwoorden,
    /// - vullen we kolom G (Answer) en I (AnswerEvaluation),
    /// - berekenen we een eerste versie van kolom F (Toepasselijk?) op basis van ToetsVooronderzoek.
    /// </summary>
    public class DueDiligenceService
    {
        private readonly AssessmentService _assessmentService;
        private readonly ToetsVooronderzoekService _toetsVooronderzoekService;

        public DueDiligenceService(
            AssessmentService assessmentService,
            ToetsVooronderzoekService toetsVooronderzoekService)
        {
            _assessmentService = assessmentService ?? throw new ArgumentNullException(nameof(assessmentService));
            _toetsVooronderzoekService = toetsVooronderzoekService ?? throw new ArgumentNullException(nameof(toetsVooronderzoekService));
        }

        /// <summary>
        /// Bouwt de "tab 7-rijen" voor een assessment op basis van:
        /// - de checklist-definities (statisch),
        /// - de gegeven antwoorden (tab 8-laag),
        /// - het ToetsVooronderzoek-resultaat.
        ///
        /// In deze versie:
        /// - IsApplicable (kolom F) wordt bepaald o.b.v. ToetsVooronderzoek + ToetsIDs per vraag,
        /// - Answer (kolom G) en AnswerEvaluation (kolom I) worden gevuld,
        /// - EvidenceSummary / DueDiligenceOutcome komen later.
        /// </summary>
        public List<AssessmentChecklistRow> BuildChecklistRows(
            Guid assessmentId,
            IEnumerable<ChecklistQuestionDefinition> definitions,
            IEnumerable<AssessmentQuestionAnswer> answers)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (answers == null) throw new ArgumentNullException(nameof(answers));

            var assessment = _assessmentService.GetById(assessmentId)
                              ?? throw new InvalidOperationException($"Assessment {assessmentId} not found.");

            // ToetsVooronderzoek-resultaat voor dit assessment (tab 6 in de excel).
            var toetsResult = _toetsVooronderzoekService.Get(assessmentId);

            var answerLookup = answers
                .Where(a => a.AssessmentId == assessmentId)
                .GroupBy(a => a.ChecklistId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var rows = new List<AssessmentChecklistRow>();

            foreach (var def in definitions)
            {
                answerLookup.TryGetValue(def.ChecklistId, out var answer);

                var isApplicable = EvaluateApplicability(assessment, def, toetsResult);

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

                    // Kolom J/K/L/M vullen we in volgende stappen
                    EvidenceSummary = null,
                    NegativeOutcomeAcceptable = false,
                    DueDiligenceOutcome = null,
                    DeviationText = null
                };

                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// Bepaalt Toepasselijk? (kolom F) voor één vraag, op basis van:
        /// - de ToetsIDs die aan de vraag gekoppeld zijn (def.ToetsIds),
        /// - de uitkomsten per ToetsID in ToetsVooronderzoekResult.ToetsAnswers.
        ///
        /// Eerste versie:
        /// - Als geen ToetsIDs zijn gekoppeld -> default true (vraag is altijd van toepassing).
        /// - Als minstens één gekoppelde Toets "Ja" is -> true.
        /// - Als alle bekende antwoorden "Nee" zijn -> false.
        /// - Als alles onbekend/ontbrekend is -> false (conservatief niet-toepasselijk voor nu).
        ///
        /// Later kunnen we hier:
        /// - overall risicoklasse (Assessment.OverallRiskLevel),
        /// - BoZ/LHV-dekking,
        /// - specifieke AVG/Algemeen-uitzonderingen
        /// aan toevoegen om 1-op-1 met Excel kolom F te worden.
        /// </summary>
        private static bool EvaluateApplicability(
            Assessment assessment,
            ChecklistQuestionDefinition def,
            ToetsVooronderzoekResult toetsResult)
        {
            // Als er geen ToetsIDs zijn opgegeven bij deze vraag, nemen we aan dat hij altijd van toepassing is.
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
                    // fallback: zoek in Questions als ToetsAnswers nog niet gevuld zou zijn
                    var q = toetsResult.Questions.FirstOrDefault(
                        x => x.ToetsId.Equals(toetsId, StringComparison.OrdinalIgnoreCase));
                    value = q?.Answer;
                }

                if (!value.HasValue)
                {
                    // Onbekend; telt voor nu niet mee in true/false
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

            // Alles onbekend of er zijn alleen lege ToetsIDs:
            // voor nu: niet van toepassing.
            // TODO: in volgende iteratie kunnen we hier fijnslijpen o.b.v. Excel-logica.
            return false;
        }
    }
}
