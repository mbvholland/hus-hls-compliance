using System;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    /// <summary>
    /// API-controller voor de AI Act-beslisboom (tab "4. AI Act Beslisboom").
    /// </summary>
    [ApiController]
    [Route("api/assessments/{assessmentId:guid}/ai-act")]
    public class AiActController : ControllerBase
    {
        private readonly AiActService _aiActService;
        private readonly AssessmentService _assessmentService;

        public AiActController(AiActService aiActService, AssessmentService assessmentService)
        {
            _aiActService = aiActService ?? throw new ArgumentNullException(nameof(aiActService));
            _assessmentService = assessmentService ?? throw new ArgumentNullException(nameof(assessmentService));
        }

        /// <summary>
        /// Haal het AI Act-profiel op voor dit assessment.
        /// Prefills uit DPIA en MDR worden automatisch toegepast.
        /// De uitkomst wordt ook teruggeschreven naar het Assessment (AiActRiskLevel + AiActStatus).
        /// </summary>
        [HttpGet]
        public ActionResult<AiActProfileState> Get(Guid assessmentId)
        {
            var assessment = _assessmentService.GetById(assessmentId);
            if (assessment == null)
            {
                return NotFound("Assessment not found.");
            }

            var state = _aiActService.GetOrCreateForAssessment(assessmentId);

            // Assessment bijwerken met AI Act-uitkomst
            assessment.AiActRiskLevel = state.RiskLevel;
            assessment.AiActStatus = state.IsComplete
                ? "AI Act geclassificeerd"
                : "Onbekend";

            return Ok(state);
        }

        public class UpdateAiActRequest
        {
            /// <summary>
            /// A2: Is_AI_systeem (Ja/Nee/…)
            /// </summary>
            public string? IsAiSystem { get; set; }

            /// <summary>
            /// C2: Beslist_over_toegang_tot_essentiele_zorg (Ja/Nee/…)
            /// </summary>
            public string? DecidesOnEssentialCareTriage { get; set; }

            /// <summary>
            /// D2: Directe_klinische_beslissing_AI (Ja/Nee/…)
            /// </summary>
            public string? DirectClinicalDecision { get; set; }

            /// <summary>
            /// E2: Interactieve_AI_met_gebruiker (Ja/Nee/…)
            /// </summary>
            public string? InteractiveAiWithUser { get; set; }

            /// <summary>
            /// F2: Genereert_content_voor_gebruiker (Ja/Nee/…)
            /// </summary>
            public string? GeneratesContentForUser { get; set; }
        }

        /// <summary>
        /// Update het AI Act-profiel voor dit assessment.
        /// Alleen niet-null velden in de request worden aangepast.
        /// B2 (hoog-risico medisch hulpmiddel) wordt altijd automatisch uit MDR afgeleid.
        /// De uitkomst wordt ook teruggeschreven naar het Assessment (AiActRiskLevel + AiActStatus).
        /// </summary>
        [HttpPut]
        public ActionResult<AiActProfileState> Update(
            Guid assessmentId,
            [FromBody] UpdateAiActRequest request)
        {
            var assessment = _assessmentService.GetById(assessmentId);
            if (assessment == null)
            {
                return NotFound("Assessment not found.");
            }

            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            var state = _aiActService.UpdateProfile(
                assessmentId,
                request.IsAiSystem,
                request.DecidesOnEssentialCareTriage,
                request.DirectClinicalDecision,
                request.InteractiveAiWithUser,
                request.GeneratesContentForUser);

            // Assessment bijwerken met AI Act-uitkomst
            assessment.AiActRiskLevel = state.RiskLevel;
            assessment.AiActStatus = state.IsComplete
                ? "AI Act geclassificeerd"
                : "Onbekend";

            return Ok(state);
        }
    }
}

