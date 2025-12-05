using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    [ApiController]
    [Route("api/assessments/{assessmentId}/evidence")]
    public class AssessmentEvidenceController : ControllerBase
    {
        private readonly IAssessmentEvidenceRepository _assessmentEvidenceRepository;
        private readonly IEvidenceDefinitionRepository _evidenceDefinitionRepository;

        public AssessmentEvidenceController(
            IAssessmentEvidenceRepository assessmentEvidenceRepository,
            IEvidenceDefinitionRepository evidenceDefinitionRepository)
        {
            _assessmentEvidenceRepository = assessmentEvidenceRepository;
            _evidenceDefinitionRepository = evidenceDefinitionRepository;
        }

        // ------------------------------------------------------------
        // GET /api/assessments/{assessmentId}/evidence[?checklistId=...]
        // ------------------------------------------------------------
        [HttpGet]
        public ActionResult<IEnumerable<AssessmentEvidenceDto>> GetEvidence(
            Guid assessmentId,
            [FromQuery] string? checklistId = null)
        {
            // 1) Load all evidence items for this assessment
            var items = _assessmentEvidenceRepository.GetByAssessment(assessmentId);

            // 2) Optionally filter by checklistId (if provided as query parameter)
            if (!string.IsNullOrWhiteSpace(checklistId))
            {
                items = items
                    .Where(e =>
                        string.Equals(
                            e.ChecklistId,
                            checklistId,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // 3) Map to DTOs
            var dto = items
                .Select(e => new AssessmentEvidenceDto
                {
                    AssessmentId = e.AssessmentId,
                    ChecklistId = e.ChecklistId,
                    EvidenceId = e.EvidenceId,
                    EvidenceName = e.EvidenceName,
                    Status = e.Status,
                    Comment = e.Comment
                })
                .ToList();

            return Ok(dto);
        }

        // ------------------------------------------------------------
        // POST /api/assessments/{assessmentId}/evidence
        // Body: { "request": [ ... ] }
        // ------------------------------------------------------------
        public class UpsertEvidenceRequest
        {
            public List<AssessmentEvidenceDto> Request { get; set; } = new();
        }

        [HttpPost]
        public ActionResult<IEnumerable<AssessmentEvidenceDto>> UpsertEvidence(
            Guid assessmentId,
            [FromBody] UpsertEvidenceRequest request)
        {
            if (request == null || request.Request == null || request.Request.Count == 0)
            {
                return BadRequest("Request must contain at least one evidence item.");
            }

            foreach (var dto in request.Request)
            {
                // If client omits AssessmentId in the body, fill it from route
                if (dto.AssessmentId == Guid.Empty)
                {
                    dto.AssessmentId = assessmentId;
                }

                if (dto.AssessmentId != assessmentId)
                {
                    return BadRequest("All evidence items must use the same assessmentId as the route.");
                }

                if (string.IsNullOrWhiteSpace(dto.ChecklistId))
                {
                    return BadRequest("ChecklistId is required.");
                }

                if (string.IsNullOrWhiteSpace(dto.EvidenceId))
                {
                    return BadRequest("EvidenceId is required.");
                }

                // Validate that EvidenceId exists in evidence-definitions.json
                var definition = _evidenceDefinitionRepository.GetById(dto.EvidenceId);
                if (definition == null)
                {
                    return BadRequest($"Unknown EvidenceId '{dto.EvidenceId}'.");
                }

                var entity = new AssessmentEvidenceItem
                {
                    AssessmentId = dto.AssessmentId,
                    ChecklistId = dto.ChecklistId,
                    EvidenceId = dto.EvidenceId,
                    EvidenceName = dto.EvidenceName,
                    Status = dto.Status,
                    Comment = dto.Comment
                };

                _assessmentEvidenceRepository.UpsertEvidence(entity);
            }

            // Return the refreshed list for this assessment
            var allItems = _assessmentEvidenceRepository.GetByAssessment(assessmentId);

            var result = allItems
                .Select(e => new AssessmentEvidenceDto
                {
                    AssessmentId = e.AssessmentId,
                    ChecklistId = e.ChecklistId,
                    EvidenceId = e.EvidenceId,
                    EvidenceName = e.EvidenceName,
                    Status = e.Status,
                    Comment = e.Comment
                })
                .ToList();

            return Ok(result);
        }
    }

    /// <summary>
    /// DTO that Swagger shows for AssessmentEvidence.
    /// </summary>
    public class AssessmentEvidenceDto
    {
        public Guid AssessmentId { get; set; }
        public string ChecklistId { get; set; } = string.Empty;
        public string EvidenceId { get; set; } = string.Empty;
        public string EvidenceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Comment { get; set; }
    }
}
