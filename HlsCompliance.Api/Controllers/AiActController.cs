using System;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers;

[ApiController]
[Route("api/assessments/{assessmentId:guid}/ai-act")]
public class AiActController : ControllerBase
{
    private readonly AiActService _aiActService;
    private readonly AssessmentService _assessmentService;

    public AiActController(
        AiActService aiActService,
        AssessmentService assessmentService)
    {
        _aiActService = aiActService;
        _assessmentService = assessmentService;
    }

    /// <summary>
    /// Haal het AI Act-profiel op voor dit assessment
    /// (A2–F2 + risicoklasse en score).
    /// Prefill vanuit DPIA en MDR wordt hierbij toegepast.
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
        assessment.AiActStatus = state.RiskLevel == "Onbekend"
            ? "Onbekend"
            : "AI Act geclassificeerd";

        return Ok(state);
    }

    public class UpdateAiActProfileRequest
    {
        /// <summary>
        /// A2: Is_AI_systeem (Ja/Nee/…).
        /// </summary>
        public string? IsAiSystem { get; set; }

        /// <summary>
        /// C2: Beslist over toegang tot essentiële zorg (triage, urgentiebepaling) (Ja/Nee/…).
        /// </summary>
        public string? DecidesOnEssentialCareTriage { get; set; }

        /// <summary>
        /// D2: Directe klinische beslissing door AI (Ja/Nee/…).
        /// </summary>
        public string? DirectClinicalDecision { get; set; }

        /// <summary>
        /// E2: Interactieve AI met gebruiker (Ja/Nee/…).
        /// </summary>
        public string? InteractiveAiWithUser { get; set; }

        /// <summary>
        /// F2: Genereert content voor gebruiker (Ja/Nee/…).
        /// </summary>
        public string? GeneratesContentForUser { get; set; }
    }

    /// <summary>
    /// Update het AI Act-profiel (A2, C2, D2, E2, F2) voor dit assessment.
    /// B2 (IsHighRiskMedicalDevice) wordt altijd afgeleid uit de MDR-klasse.
    /// Null-velden in de body worden genegeerd (bestaande waarde blijft staan).
    /// </summary>
    [HttpPut]
    public ActionResult<AiActProfileState> Update(
        Guid assessmentId,
        [FromBody] UpdateAiActProfileRequest request)
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
        assessment.AiActStatus = state.RiskLevel == "Onbekend"
            ? "Onbekend"
            : "AI Act geclassificeerd";

        return Ok(state);
    }
}
