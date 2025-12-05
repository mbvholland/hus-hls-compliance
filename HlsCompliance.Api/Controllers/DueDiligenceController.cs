using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    /// <summary>
    /// API-controller voor de due diligence-checklist (tab 7).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DueDiligenceController : ControllerBase
    {
        private readonly DueDiligenceService _dueDiligenceService;
        private readonly IChecklistDefinitionRepository _definitionRepository;
        private readonly IAssessmentAnswersRepository _answersRepository;
        private readonly IAssessmentEvidenceRepository _evidenceRepository;

        public DueDiligenceController(
            DueDiligenceService dueDiligenceService,
            IChecklistDefinitionRepository definitionRepository,
            IAssessmentAnswersRepository answersRepository,
            IAssessmentEvidenceRepository evidenceRepository)
        {
            _dueDiligenceService = dueDiligenceService ?? throw new ArgumentNullException(nameof(dueDiligenceService));
            _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
            _answersRepository = answersRepository ?? throw new ArgumentNullException(nameof(answersRepository));
            _evidenceRepository = evidenceRepository ?? throw new ArgumentNullException(nameof(evidenceRepository));
        }

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

        // ----------------------
        // API DTO's
        // ----------------------

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
    }
}
