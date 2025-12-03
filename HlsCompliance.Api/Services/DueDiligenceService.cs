using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Service die de logica van tab 7 (due diligence) gaat afhandelen.
    /// In deze eerste versie koppelen we alleen definities + antwoorden
    /// en vullen we kolom G (Answer) en I (AnswerEvaluation).
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
        /// - de gegeven antwoorden (tab 8-laag).
        ///
        /// In deze eerste versie:
        /// - IsApplicable wordt voorlopig op true gezet (Toepasselijk? doen we in een volgende stap),
        /// - Answer (kolom G) en AnswerEvaluation (kolom I) worden gevuld.
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

            // Haal ToetsVooronderzoek op zodat we later Toepasselijk?-logica kunnen toevoegen.
            var toetsResult = _toetsVooronderzoekService.Get(assessmentId);

            var answerLookup = answers
                .Where(a => a.AssessmentId == assessmentId)
                .GroupBy(a => a.ChecklistId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var rows = new List<AssessmentChecklistRow>();

            foreach (var def in definitions)
            {
                answerLookup.TryGetValue(def.ChecklistId, out var answer);

                var row = new AssessmentChecklistRow
                {
                    AssessmentId = assessmentId,
                    ChecklistId = def.ChecklistId,

                    // Kolom F: Toepasselijk? -> voorlopig altijd true (we bouwen de logica later in)
                    IsApplicable = true,

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
    }
}
