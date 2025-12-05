using System;
using System.Collections.Generic;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    [ApiController]
    [Route("api/assessments/{assessmentId:guid}/evidence")]
    public class AssessmentEvidenceController : ControllerBase
    {
        private readonly IAssessmentEvidenceRepository _evidenceRepository;
        private readonly IChecklistEvidenceLinkRepository _linkRepository;
        private readonly IEvidenceDefinitionRepository _evidenceDefinitionRepository;

        public AssessmentEvidenceController(
            IAssessmentEvidenceRepository evidenceRepository,
            IChecklistEvidenceLinkRepository linkRepository,
            IEvidenceDefinitionRepository evidenceDefinitionRepository)
        {
            _evidenceRepository = evidenceRepository;
            _linkRepository = linkRepository;
            _evidenceDefinitionRepository = evidenceDefinitionRepository;
        }

        /// <summary>
        /// Haal alle bewijslast-items op voor een assessment.
        /// Dit zijn de records uit tab 11 (AssessmentEvidenceItem),
        /// zoals opgeslagen in de JSON-bestanden.
        /// </summary>
        [HttpGet]
        public ActionResult<IEnumerable<AssessmentEvidenceItem>> Get(Guid assessmentId)
        {
            var items = _evidenceRepository.GetByAssessment(assessmentId);

            // GEEN hard-gecodeerde voorbeelddata, gewoon teruggeven wat er is.
            return Ok(items);
        }

        public class UpsertEvidenceRequest
        {
            /// <summary>
            /// ChecklistId uit tab 7/8/11 (kolom A).
            /// </summary>
            public string ChecklistId { get; set; } = string.Empty;

            /// <summary>
            /// EvidenceId zoals in tab 10/11.
            /// </summary>
            public string EvidenceId { get; set; } = string.Empty;

            /// <summary>
            /// Naam / beschrijving van het bewijs.
            /// </summary>
            public string? EvidenceName { get; set; }

            /// <summary>
            /// Status: "Goedgekeurd", "In beoordeling", "Niet aangeleverd", "Afgekeurd", ...
            /// </summary>
            public string? Status { get; set; }

            /// <summary>
            /// Optionele toelichting.
            /// </summary>
            public string? Comment { get; set; }
        }

        /// <summary>
        /// Voeg een bewijslast-item toe of update een bestaand item
        /// (combinatie AssessmentId + ChecklistId + EvidenceId).
        /// Werkt als tab 11: per EvidenceId kun je status, naam en comment bijwerken.
        /// </summary>
        [HttpPost]
        public ActionResult<IEnumerable<AssessmentEvidenceItem>> Upsert(
            Guid assessmentId,
            [FromBody] UpsertEvidenceRequest request)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.ChecklistId))
            {
                return BadRequest("ChecklistId is required.");
            }

            if (string.IsNullOrWhiteSpace(request.EvidenceId))
            {
                return BadRequest("EvidenceId is required.");
            }

            // Eventueel: naam prefille’n uit evidence-definities (tab 10)
            string? effectiveName = request.EvidenceName;
            var def = _evidenceDefinitionRepository.GetById(request.EvidenceId);
            if (string.IsNullOrWhiteSpace(effectiveName) &&
                def != null &&
                !string.IsNullOrWhiteSpace(def.Name))
            {
                effectiveName = def.Name;
            }

            var item = new AssessmentEvidenceItem
            {
                AssessmentId = assessmentId,
                ChecklistId = request.ChecklistId,
                EvidenceId = request.EvidenceId,
                EvidenceName = effectiveName ?? request.EvidenceName,
                Status = request.Status,
                Comment = request.Comment
            };

            _evidenceRepository.UpsertEvidence(item);

            // Geef na de upsert de volledige lijst voor dit assessment terug
            var allItems = _evidenceRepository.GetByAssessment(assessmentId);
            return Ok(allItems);
        }
    }
}
