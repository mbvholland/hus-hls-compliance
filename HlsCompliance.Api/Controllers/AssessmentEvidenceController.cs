using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    /// <summary>
    /// API voor bewijslast per assessment (spiegelt tab 11-concept).
    /// </summary>
    [ApiController]
    [Route("api/assessments/{assessmentId:guid}/evidence")]
    public class AssessmentEvidenceController : ControllerBase
    {
        private readonly IAssessmentEvidenceRepository _evidenceRepository;

        public AssessmentEvidenceController(IAssessmentEvidenceRepository evidenceRepository)
        {
            _evidenceRepository = evidenceRepository ?? throw new ArgumentNullException(nameof(evidenceRepository));
        }

        /// <summary>
        /// Haal alle bewijslast-items op voor een assessment.
        /// Dit zijn de per-assessment, per-ChecklistID, per-BewijsID records
        /// (zoals tab 11 in Excel).
        /// </summary>
        [HttpGet]
        public ActionResult<List<AssessmentEvidenceItem>> Get(Guid assessmentId)
        {
            var items = _evidenceRepository
                .GetByAssessment(assessmentId)
                .ToList();

            return Ok(items);
        }

        /// <summary>
        /// Body voor het updaten van evidence voor één assessment.
        /// </summary>
        public class UpsertEvidenceRequest
        {
            public List<EvidenceItemDto> Items { get; set; } = new();
        }

        /// <summary>
        /// Eén regel uit tab 11: ChecklistId + BewijsId + status/toelichting.
        /// </summary>
        public class EvidenceItemDto
        {
            public string ChecklistId { get; set; } = string.Empty;
            public string EvidenceId { get; set; } = string.Empty;
            public string? EvidenceName { get; set; }
            public string? Status { get; set; }
            public string? Comment { get; set; }
        }

        /// <summary>
        /// Vervang alle bewijslast-items voor dit assessment door de opgegeven set.
        /// Dit werkt net als tab 11 overschrijven: de nieuwe set wordt de waarheid.
        /// </summary>
        [HttpPut]
        public IActionResult Upsert(Guid assessmentId, [FromBody] UpsertEvidenceRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                // Lege lijst mag: betekent "verwijder alle evidence voor dit assessment".
                _evidenceRepository.Upsert(assessmentId, Enumerable.Empty<AssessmentEvidenceItem>());
                return NoContent();
            }

            var items = request.Items
                .Where(dto =>
                    !string.IsNullOrWhiteSpace(dto.ChecklistId) &&
                    !string.IsNullOrWhiteSpace(dto.EvidenceId))
                .Select(dto => new AssessmentEvidenceItem
                {
                    AssessmentId = assessmentId,
                    ChecklistId = dto.ChecklistId.Trim(),
                    EvidenceId = dto.EvidenceId.Trim(),
                    EvidenceName = string.IsNullOrWhiteSpace(dto.EvidenceName)
                        ? null
                        : dto.EvidenceName.Trim(),
                    Status = string.IsNullOrWhiteSpace(dto.Status)
                        ? null
                        : dto.Status.Trim(),
                    Comment = string.IsNullOrWhiteSpace(dto.Comment)
                        ? null
                        : dto.Comment.Trim()
                });

            // LET OP: hier gebruiken we de interface-methode Upsert (zonder 'Evidence')
            _evidenceRepository.Upsert(assessmentId, items);

            return NoContent();
        }
    }
}
