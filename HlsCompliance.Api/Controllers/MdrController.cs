using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers;

[ApiController]
[Route("api/assessments/{assessmentId:guid}/mdr")]
public class MdrController : ControllerBase
{
    private readonly MdrService _mdrService;
    private readonly AssessmentService _assessmentService;

    public MdrController(MdrService mdrService, AssessmentService assessmentService)
    {
        _mdrService = mdrService;
        _assessmentService = assessmentService;
    }

    /// <summary>
    /// Haal de MDR-classificatie op voor dit assessment (maakt een lege state aan als die nog niet bestaat).
    /// </summary>
    [HttpGet]
    public ActionResult<MdrClassificationState> Get(Guid assessmentId)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        var state = _mdrService.GetOrCreateForAssessment(assessmentId);
        return Ok(state);
    }

    public class UpdateMdrRequest
    {
        /// <summary>
        /// "Ja" / "Nee" of leeg.
        /// </summary>
        public string? A2_IsMedicalDevice { get; set; }

        /// <summary>
        /// "Ja" / "Nee" of leeg.
        /// </summary>
        public string? B2_ExceptionOrExclusion { get; set; }

        /// <summary>
        /// "Ja" / "Nee" of leeg.
        /// </summary>
        public string? C2_InvasiveOrImplantable { get; set; }

        /// <summary>
        /// "Ja" / "Nee" of leeg.
        /// </summary>
        public string? D2_AdditionalRiskFactor { get; set; }

        /// <summary>
        /// "dodelijk_of_onherstelbaar", "ernstig", "niet_ernstig" of leeg.
        /// </summary>
        public string? E2_Severity { get; set; }
    }

    /// <summary>
    /// Werk de MDR-antwoorden bij en herbereken de classificatie.
    /// </summary>
    [HttpPut]
    public ActionResult<MdrClassificationState> Update(Guid assessmentId, [FromBody] UpdateMdrRequest request)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        var state = _mdrService.UpdateAnswers(
            assessmentId,
            request.A2_IsMedicalDevice,
            request.B2_ExceptionOrExclusion,
            request.C2_InvasiveOrImplantable,
            request.D2_AdditionalRiskFactor,
            request.E2_Severity
        );

        return Ok(state);
    }
}
