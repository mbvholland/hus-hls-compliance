using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers;

[ApiController]
[Route("api/assessments/{assessmentId:guid}/security-profile")]
public class SecurityProfileController : ControllerBase
{
    private readonly SecurityProfileService _securityProfileService;
    private readonly AssessmentService _assessmentService;

    public SecurityProfileController(
        SecurityProfileService securityProfileService,
        AssessmentService assessmentService)
    {
        _securityProfileService = securityProfileService;
        _assessmentService = assessmentService;
    }

    /// <summary>
    /// Haal het securityprofiel op voor dit assessment.
    /// </summary>
    [HttpGet]
    public ActionResult<SecurityProfileState> Get(Guid assessmentId)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        var state = _securityProfileService.GetOrCreateForAssessment(assessmentId);
        return Ok(state);
    }

    public class UpdateSecurityProfileRequest
    {
        /// <summary>
        /// (Optioneel) Versie of variant van het securityprofiel.
        /// </summary>
        public string? ProfileVersion { get; set; }

        /// <summary>
        /// Overall niveau van het profiel, bv. "Onbekend", "Laag", "Middel", "Hoog".
        /// </summary>
        public string? OverallSecurityLevel { get; set; }

        /// <summary>
        /// Scores per blok/domein. Key = bloknaam, Value = niveau.
        /// </summary>
        public Dictionary<string, string>? BlockScores { get; set; }
    }

    /// <summary>
    /// Maak of update het securityprofiel voor dit assessment.
    /// </summary>
    [HttpPut]
    public ActionResult<SecurityProfileState> Update(
        Guid assessmentId,
        [FromBody] UpdateSecurityProfileRequest request)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        if (request == null)
        {
            return BadRequest("Body is required.");
        }

        var updated = _securityProfileService.UpdateProfile(
            assessmentId,
            request.ProfileVersion,
            request.OverallSecurityLevel,
            request.BlockScores
        );

        return Ok(updated);
    }
}
