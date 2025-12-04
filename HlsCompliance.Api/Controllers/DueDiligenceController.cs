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
        /// - antwoorden: uit JSON (answers.json),
        /// - bewijslast: uit JSON (evidence.json),
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

        /// <summary>
        /// Update antwoorden op controlevragen (tab 8) voor een assessment.
        /// </summary>
        [HttpPut("{assessmentId:guid}/answers")]
        public IActionResult UpdateAnswers(Guid assessmentId, [FromBody] AnswersUpdateRequest request)
        {
            if (request == null || request.Answers == null)
            {
                return BadRequest("Answers collection is required.");
            }

            var items = request.Answers
                .Where(a => !string.IsNullOrWhiteSpace(a.ChecklistId))
                .Select(a => new AssessmentQuestionAnswer
                {
                    AssessmentId = assessmentId,
                    ChecklistId = a.ChecklistId,
                    RawAnswer = a.RawAnswer,
                    AnswerEvaluation = a.AnswerEvaluation
                })
                .ToList();

            _answersRepository.UpsertAnswers(assessmentId, items);

            return NoContent();
        }

        /// <summary>
        /// Update bewijslast-items (tab 11) voor een assessment.
        /// </summary>
        [HttpPut("{assessmentId:guid}/evidence")]
        public IActionResult UpdateEvidence(Guid assessmentId, [FromBody] EvidenceUpdateRequest request)
        {
            if (request == null || request.Items == null)
            {
                return BadRequest("Items collection is required.");
            }

            var items = request.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.ChecklistId) &&
                            !string.IsNullOrWhiteSpace(i.EvidenceId))
                .Select(i => new AssessmentEvidenceItem
                {
                    AssessmentId = assessmentId,
                    ChecklistId = i.ChecklistId,
                    EvidenceId = i.EvidenceId,
                    EvidenceName = i.EvidenceName,
                    Status = i.Status,
                    Comment = i.Comment
                })
                .ToList();

            _evidenceRepository.UpsertEvidence(assessmentId, items);

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

        public class AnswerUpdateItemDto
        {
            public string ChecklistId { get; set; } = string.Empty;
            public string? RawAnswer { get; set; }
            public string? AnswerEvaluation { get; set; }
        }

        public class AnswersUpdateRequest
        {
            public List<AnswerUpdateItemDto> Answers { get; set; } = new();
        }

        public class EvidenceUpdateItemDto
        {
            public string ChecklistId { get; set; } = string.Empty;
            public string EvidenceId { get; set; } = string.Empty;
            public string? EvidenceName { get; set; }
            public string? Status { get; set; }
            public string? Comment { get; set; }
        }

        public class EvidenceUpdateRequest
        {
            public List<EvidenceUpdateItemDto> Items { get; set; } = new();
        }
    }
}
