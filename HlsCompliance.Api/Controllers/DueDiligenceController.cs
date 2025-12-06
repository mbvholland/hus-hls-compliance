using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    /// <summary>
    /// API-controller voor de due diligence-checklist (tab 7) en voortgangsrapport.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DueDiligenceController : ControllerBase
    {
        private readonly DueDiligenceService _dueDiligenceService;
        private readonly IChecklistDefinitionRepository _definitionRepository;
        private readonly IAssessmentAnswersRepository _answersRepository;
        private readonly IAssessmentEvidenceRepository _evidenceRepository;
        private readonly AssessmentService _assessmentService;

        public DueDiligenceController(
            DueDiligenceService dueDiligenceService,
            IChecklistDefinitionRepository definitionRepository,
            IAssessmentAnswersRepository answersRepository,
            IAssessmentEvidenceRepository evidenceRepository,
            AssessmentService assessmentService)
        {
            _dueDiligenceService = dueDiligenceService ?? throw new ArgumentNullException(nameof(dueDiligenceService));
            _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
            _answersRepository = answersRepository ?? throw new ArgumentNullException(nameof(answersRepository));
            _evidenceRepository = evidenceRepository ?? throw new ArgumentNullException(nameof(evidenceRepository));
            _assessmentService = assessmentService ?? throw new ArgumentNullException(nameof(assessmentService));
        }

        // --------------------------------------------------------------------
        // 1) Checklist voor één assessment
        // --------------------------------------------------------------------

        /// <summary>
        /// Haalt de due diligence-checklist op voor een assessment.
        /// - definities: uit IChecklistDefinitionRepository,
        /// - antwoorden: uit IAssessmentAnswersRepository,
        /// - bewijslast: uit IAssessmentEvidenceRepository,
        /// - logica voor Toepasselijk?, BewijsResultaat en Resultaat due diligence is actief.
        /// </summary>
        [HttpGet("{assessmentId:guid}/checklist")]
        public ActionResult<List<DueDiligenceChecklistRowDto>> GetChecklist(Guid assessmentId)
        {
            var definitions = _definitionRepository.GetAll();
            var answers = _answersRepository.GetByAssessment(assessmentId);
            var evidence = _evidenceRepository.GetByAssessment(assessmentId);

            var rows = _dueDiligenceService.BuildChecklistRows(
                assessmentId,
                definitions,
                answers,
                evidence);

            var dtoList = (from row in rows
                           join def in definitions on row.ChecklistId equals def.ChecklistId
                               into defJoin
                           from def in defJoin.DefaultIfEmpty()
                           select new DueDiligenceChecklistRowDto
                           {
                               AssessmentId = row.AssessmentId,
                               ChecklistId = row.ChecklistId,

                               Category = def?.Category ?? string.Empty,
                               Question = def?.Question ?? string.Empty,
                               NormReference = def?.NormReference ?? string.Empty,
                               RiskLevel = def?.RiskLevel ?? string.Empty,

                               IsApplicable = row.IsApplicable,
                               Answer = row.Answer,
                               AnswerEvaluation = row.AnswerEvaluation,
                               EvidenceSummary = row.EvidenceSummary,
                               NegativeOutcomeAcceptable = row.NegativeOutcomeAcceptable,
                               DueDiligenceOutcome = row.DueDiligenceOutcome,
                               DeviationText = row.DeviationText
                           })
                           .ToList();

            return Ok(dtoList);
        }

        // --------------------------------------------------------------------
        // 2) Kolom K/M: Negatief resultaat acceptabel + afwijkingstekst
        // --------------------------------------------------------------------

        /// <summary>
        /// Update kolom K (Negatief resultaat acceptabel?) en kolom M (Afwijkingstekst) voor één checklist-vraag.
        /// </summary>
        [HttpPost("{assessmentId:guid}/decision")]
        public IActionResult UpdateDecision(Guid assessmentId, [FromBody] UpdateDecisionRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.ChecklistId))
            {
                return BadRequest("ChecklistId is required.");
            }

            _dueDiligenceService.UpdateNegativeOutcomeDecision(
                assessmentId,
                request.ChecklistId,
                request.NegativeOutcomeAcceptable,
                request.DeviationText);

            return NoContent();
        }

        // --------------------------------------------------------------------
        // 3) Voortgangsrapport Due Diligence (incl. eindbeslissing uit Assessment)
        // --------------------------------------------------------------------

        /// <summary>
        /// Voortgangsrapport voor de Due Diligence van één assessment:
        /// - aantallen vragen per status/uitkomst,
        /// - voortgang (hoeveel beoordeeld, hoeveel nog te doen),
        /// - geregistreerde eindbeslissing (stop / go_to_contract) uit Assessment.
        /// </summary>
        [HttpGet("{assessmentId:guid}/report")]
        public ActionResult<DueDiligenceReportDto> GetReport(Guid assessmentId)
        {
            var assessment = _assessmentService.GetById(assessmentId);
            if (assessment == null)
            {
                return NotFound("Assessment not found.");
            }

            var definitions = _definitionRepository.GetAll();
            var answers = _answersRepository.GetByAssessment(assessmentId);
            var evidence = _evidenceRepository.GetByAssessment(assessmentId);

            var rows = _dueDiligenceService.BuildChecklistRows(
                assessmentId,
                definitions,
                answers,
                evidence);

            var applicableRows = rows.Where(r => r.IsApplicable).ToList();
            var notApplicableRows = rows.Where(r => !r.IsApplicable).ToList();

            int totalQuestions = rows.Count;
            int applicableQuestions = applicableRows.Count;
            int notApplicableQuestions = notApplicableRows.Count;

            int compliesCount = applicableRows.Count(r => r.DueDiligenceOutcome == "Voldoet");
            int notCompliesCount = applicableRows.Count(r => r.DueDiligenceOutcome == "Voldoet niet");
            int deviationAcceptableCount = applicableRows.Count(r => r.DueDiligenceOutcome == "Afwijking acceptabel");
            int toBeAssessedCount = applicableRows.Count(r =>
                string.IsNullOrWhiteSpace(r.DueDiligenceOutcome) ||
                r.DueDiligenceOutcome == "Nog te beoordelen");

            int completedQuestions = applicableRows.Count - toBeAssessedCount;

            string overallStatus;
            if (notCompliesCount > 0)
            {
                overallStatus = "Due diligence bevat niet-acceptabele afwijkingen.";
            }
            else if (toBeAssessedCount > 0)
            {
                overallStatus = "Due diligence is nog niet volledig beoordeeld.";
            }
            else
            {
                overallStatus = "Due diligence is volledig beoordeeld.";
            }

            var dto = new DueDiligenceReportDto
            {
                AssessmentId = assessment.Id,
                Organisation = assessment.Organisation,
                Supplier = assessment.Supplier,
                Solution = assessment.Solution,

                TotalQuestions = totalQuestions,
                ApplicableQuestions = applicableQuestions,
                NotApplicableQuestions = notApplicableQuestions,
                CompletedQuestions = completedQuestions,

                CompliesCount = compliesCount,
                NotCompliesCount = notCompliesCount,
                DeviationAcceptableCount = deviationAcceptableCount,
                ToBeAssessedCount = toBeAssessedCount,

                OverallStatus = overallStatus,

                FinalDecision = assessment.DueDiligenceFinalDecision,
                FinalDecisionMotivation = assessment.DueDiligenceFinalDecisionMotivation,
                FinalDecisionBy = assessment.DueDiligenceFinalDecisionBy,
                FinalDecisionDate = assessment.DueDiligenceFinalDecisionDate,

                LastUpdatedAt = assessment.UpdatedAt
            };

            return Ok(dto);
        }

        // --------------------------------------------------------------------
        // DTO's
        // --------------------------------------------------------------------

        public class DueDiligenceChecklistRowDto
        {
            public Guid AssessmentId { get; set; }
            public string ChecklistId { get; set; } = string.Empty;

            public string Category { get; set; } = string.Empty;
            public string Question { get; set; } = string.Empty;
            public string NormReference { get; set; } = string.Empty;
            public string RiskLevel { get; set; } = string.Empty;

            public bool IsApplicable { get; set; }

            public string? Answer { get; set; }
            public string? AnswerEvaluation { get; set; }

            public string? EvidenceSummary { get; set; }

            public bool NegativeOutcomeAcceptable { get; set; }

            public string? DueDiligenceOutcome { get; set; }

            public string? DeviationText { get; set; }
        }

        public class UpdateDecisionRequest
        {
            public string ChecklistId { get; set; } = string.Empty;
            public bool NegativeOutcomeAcceptable { get; set; }
            public string? DeviationText { get; set; }
        }

        public class DueDiligenceReportDto
        {
            public Guid AssessmentId { get; set; }
            public string Organisation { get; set; } = string.Empty;
            public string Supplier { get; set; } = string.Empty;
            public string Solution { get; set; } = string.Empty;

            public int TotalQuestions { get; set; }
            public int ApplicableQuestions { get; set; }
            public int NotApplicableQuestions { get; set; }
            public int CompletedQuestions { get; set; }

            public int CompliesCount { get; set; }
            public int NotCompliesCount { get; set; }
            public int DeviationAcceptableCount { get; set; }
            public int ToBeAssessedCount { get; set; }

            public string OverallStatus { get; set; } = string.Empty;

            public string? FinalDecision { get; set; }
            public string? FinalDecisionMotivation { get; set; }
            public string? FinalDecisionBy { get; set; }
            public DateTime? FinalDecisionDate { get; set; }

            public DateTime? LastUpdatedAt { get; set; }
        }
    }
}
