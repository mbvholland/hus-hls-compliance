using System;
using System.Collections.Generic;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssessmentsController : ControllerBase
    {
        private readonly AssessmentService _assessmentService;

        public AssessmentsController(AssessmentService assessmentService)
        {
            _assessmentService = assessmentService;
        }

        // ------------------------------------------------------------
        // DTO's
        // ------------------------------------------------------------

        public class CreateAssessmentRequest
        {
            public string Organisation { get; set; } = string.Empty;
            public string Supplier { get; set; } = string.Empty;
            public string Solution { get; set; } = string.Empty;
            public string HlsVersion { get; set; } = "1.0";
        }

        public class UpdatePhaseStatusRequest
        {
            /// <summary>
            /// Phase key, e.g. "phase1", "phase2", "phase3", "phase4a", "phase4b".
            /// </summary>
            public string Phase { get; set; } = string.Empty;

            /// <summary>
            /// New status, e.g. "not_started", "in_progress", "completed".
            /// </summary>
            public string Status { get; set; } = string.Empty;
        }

        /// <summary>
        /// Eindbeslissing voor Due Diligence (F3).
        /// </summary>
        public class DueDiligenceDecisionRequest
        {
            /// <summary>
            /// "stop" of "go_to_contract".
            /// </summary>
            public string Decision { get; set; } = string.Empty;

            /// <summary>
            /// Motivatie bij het besluit.
            /// </summary>
            public string? Motivation { get; set; }

            /// <summary>
            /// Wie het besluit heeft genomen (rol/naam).
            /// </summary>
            public string? DecisionBy { get; set; }

            /// <summary>
            /// Datum van het besluit.
            /// </summary>
            public DateTime? DecisionDate { get; set; }
        }

        // ------------------------------------------------------------
        // Endpoints
        // ------------------------------------------------------------

        [HttpGet]
        public ActionResult<IEnumerable<Assessment>> GetAll()
        {
            var items = _assessmentService.GetAll();
            return Ok(items);
        }

        [HttpPost]
        public ActionResult<Assessment> Create([FromBody] CreateAssessmentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Organisation) ||
                string.IsNullOrWhiteSpace(request.Supplier) ||
                string.IsNullOrWhiteSpace(request.Solution))
            {
                return BadRequest("Organisation, Supplier and Solution are required.");
            }

            var assessment = _assessmentService.Create(
                request.Organisation,
                request.Supplier,
                request.Solution,
                request.HlsVersion
            );

            return CreatedAtAction(nameof(GetById), new { id = assessment.Id }, assessment);
        }

        [HttpGet("{id:guid}")]
        public ActionResult<Assessment> GetById(Guid id)
        {
            var assessment = _assessmentService.GetById(id);
            if (assessment == null)
            {
                return NotFound();
            }

            return Ok(assessment);
        }

        [HttpPatch("{id:guid}/phase-status")]
        public ActionResult UpdatePhaseStatus(Guid id, [FromBody] UpdatePhaseStatusRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Phase) ||
                string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest("Phase and Status are required.");
            }

            var ok = _assessmentService.UpdatePhaseStatus(id, request.Phase, request.Status);

            if (!ok)
            {
                return NotFound("Assessment not found or invalid phase.");
            }

            return NoContent();
        }

        /// <summary>
        /// Registreer de eindbeslissing van de Due Diligence (F3: Stop / Ga door naar contractering).
        /// </summary>
        [HttpPost("{id:guid}/due-diligence-decision")]
        public ActionResult<Assessment> UpdateDueDiligenceDecision(Guid id, [FromBody] DueDiligenceDecisionRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Decision))
            {
                return BadRequest("Decision is required (use 'stop' or 'go_to_contract').");
            }

            var normalizedDecision = request.Decision.Trim().ToLowerInvariant();
            if (normalizedDecision != "stop" && normalizedDecision != "go_to_contract")
            {
                return BadRequest("Decision must be 'stop' or 'go_to_contract'.");
            }

            var ok = _assessmentService.UpdateDueDiligenceFinalDecision(
                id,
                normalizedDecision,
                request.Motivation,
                request.DecisionBy,
                request.DecisionDate ?? DateTime.UtcNow
            );

            if (!ok)
            {
                return NotFound("Assessment not found.");
            }

            var updated = _assessmentService.GetById(id);
            return Ok(updated);
        }
    }
}
