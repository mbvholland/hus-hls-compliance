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
    /// Haal de huidige DPIA-quickscan op voor een assessment.
    /// Maakt een nieuwe quickscan aan als er nog geen bestaat.
    /// </summary>
    [HttpGet]
    public ActionResult<DpiaQuickscanResult> Get(Guid assessmentId)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        var quickscan = _dpiaQuickscanService.GetOrCreateForAssessment(assessmentId);
        return Ok(quickscan);
    }

    public class UpdateDpiaQuickscanRequest
    {
        // Voorbeeld JSON:
        // {
        //   "answers": {
        //     "1": "Ja",
        //     "2": "Nee",
        //     "3": null
        //   }
        // }
        public Dictionary<int, string?> Answers { get; set; } = new();
    }

    /// <summary>
    /// Werk antwoorden in de DPIA-quickscan bij en herbereken de uitkomst.
    /// </summary>
    [HttpPut]
    public ActionResult<DpiaQuickscanResult> Update(Guid assessmentId, [FromBody] UpdateDpiaQuickscanRequest request)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        if (request.Answers == null || request.Answers.Count == 0)
        {
            return BadRequest("No answers provided.");
        }

        var quickscan = _dpiaQuickscanService.UpdateAnswers(assessmentId, request.Answers);
        return Ok(quickscan);
    }
}
