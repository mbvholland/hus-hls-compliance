using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers;

[ApiController]
[Route("api/assessments/{assessmentId:guid}/dpia-quickscan")]
public class DpiaQuickscanController : ControllerBase
{
    private readonly DpiaQuickscanService _dpiaQuickscanService;
    private readonly AssessmentService _assessmentService;

    public DpiaQuickscanController(
        DpiaQuickscanService dpiaQuickscanService,
        AssessmentService assessmentService)
    {
        _dpiaQuickscanService = dpiaQuickscanService;
        _assessmentService = assessmentService;
    }

    /// <summary>
    /// Haal de DPIA-quickscan op voor dit assessment
    /// (incl. vragen, antwoorden en berekende uitkomst).
    /// </summary>
    [HttpGet]
    public ActionResult<DpiaQuickscanResult> Get(Guid assessmentId)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        var result = _dpiaQuickscanService.GetOrCreateForAssessment(assessmentId);
        return Ok(result);
    }

    public class UpdateDpiaQuickscanAnswer
    {
        /// <summary>
        /// Code van de vraag (bijv. "Q1", "Q2"), moet overeenkomen met DpiaQuickscanQuestion.Code.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Antwoord, bijv. "Ja", "Nee", "Nvt". Leeg/null = geen antwoord.
        /// </summary>
        public string? Answer { get; set; }
    }

    public class UpdateDpiaQuickscanRequest
    {
        /// <summary>
        /// Antwoorden per vraagcode.
        /// </summary>
        public List<UpdateDpiaQuickscanAnswer> Answers { get; set; } = new();
    }

    /// <summary>
    /// Update de antwoorden voor de DPIA-quickscan van dit assessment.
    /// De uitkomst (DpiaRequired) wordt automatisch herberekend.
    /// </summary>
    [HttpPut]
    public ActionResult<DpiaQuickscanResult> Update(
        Guid assessmentId,
        [FromBody] UpdateDpiaQuickscanRequest request)
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

        var result = _dpiaQuickscanService.UpdateAnswers(assessmentId, answers);
        return Ok(result);
    }
}
