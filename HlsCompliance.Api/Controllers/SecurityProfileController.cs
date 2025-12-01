using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Haal het securityprofiel van de leverancier op voor dit assessment
    /// (inclusief alle 8 vragen, afgeleide DPIA-antwoorden en risicoscore).
    /// </summary>
    [HttpGet]
    public ActionResult<SecurityProfileResult> Get(Guid assessmentId)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        var result = _securityProfileService.GetOrCreateForAssessment(assessmentId);

        // Assessment bijwerken met risicoscore + status
        assessment.SecurityProfileRiskScore = result.RiskScore;
        assessment.SecurityProfileStatus = result.IsComplete
            ? "Securityprofiel beoordeeld"
            : "Onvolledig";

        return Ok(result);
    }

    public class UpdateSecurityProfileAnswer
    {
        /// <summary>
        /// Code van de vraag (bijv. "Q2" t/m "Q8").
        /// Voor afgeleide vragen (Q1, Q5) wordt input genegeerd.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Antwoord: "Ja" of "Nee". Leeg/null = geen antwoord.
        /// </summary>
        public string? Answer { get; set; }
    }

    public class UpdateSecurityProfileRequest
    {
        /// <summary>
        /// Antwoorden per vraagcode.
        /// Let op: Q1 en Q5 zijn afgeleid uit DPIA en worden genegeerd.
        /// </summary>
        public List<UpdateSecurityProfileAnswer> Answers { get; set; } = new();
    }

    /// <summary>
    /// Update de antwoorden voor het securityprofiel van deze leverancier.
    /// Afgeleide velden (Q1 en Q5) blijven gekoppeld aan DPIA en kunnen niet direct
    /// via deze endpoint worden overschreven.
    /// </summary>
    [HttpPut]
    public ActionResult<SecurityProfileResult> Update(
        Guid assessmentId,
        [FromBody] UpdateSecurityProfileRequest request)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        if (request == null || request.Answers == null)
        {
            return BadRequest("Request body with 'answers' is required.");
        }

        var answers = request.Answers
            .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Code))
            .Select(a => (a.Code, a.Answer));

        var result = _securityProfileService.UpdateAnswers(assessmentId, answers);

        // Assessment bijwerken met risicoscore + status
        assessment.SecurityProfileRiskScore = result.RiskScore;
        assessment.SecurityProfileStatus = result.IsComplete
            ? "Securityprofiel beoordeeld"
            : "Onvolledig";

        return Ok(result);
    }
}
