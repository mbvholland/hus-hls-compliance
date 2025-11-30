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

    public AiActController(AiActService aiActService, AssessmentService assessmentService)
    {
        _aiActService = aiActService;
        _assessmentService = assessmentService;
    }

    /// <summary>
    /// Haal het AI Act-profiel op voor dit assessment (maakt een lege state aan als die nog niet bestaat).
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
        return Ok(state);
    }

    public class UpdateAiActRequest
    {
        /// <summary>
        /// "Ja" / "Nee" of leeg.
        /// </summary>
        public string? A2_IsAiSystem { get; set; }

        /// <summary>
        /// "Ja" / "Nee" of leeg.
        /// </summary>
        public string? B2_IsGeneralPurpose { get; set; }

        /// <summary>
        /// "Ja" / "Nee" of leeg.
        /// </summary>
        public string? C2_HighRiskUseCase { get; set; }

        /// <summary>
        /// "Ja" / "Nee" of leeg.
        /// </summary>
        public string? D2_ProhibitedPractice { get; set; }

        /// <summary>
        /// "laag", "beperkt", "hoog" of leeg.
        /// </summary>
        public string? E2_ImpactLevel { get; set; }
    }

    /// <summary>
    /// Werk de AI Act-antwoorden bij en herbereken het risicoprofiel.
    /// </summary>
    [HttpPut]
    public ActionResult<AiActProfileState> Update(Guid assessmentId, [FromBody] UpdateAiActRequest request)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        var state = _aiActService.UpdateAnswers(
            assessmentId,
            request.A2_IsAiSystem,
            request.B2_IsGeneralPurpose,
            request.C2_HighRiskUseCase,
            request.D2_ProhibitedPractice,
            request.E2_ImpactLevel
        );

        return Ok(state);
    }
}
