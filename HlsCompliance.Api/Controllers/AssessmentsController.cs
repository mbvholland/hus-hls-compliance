using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssessmentsController : ControllerBase
{
    private readonly AssessmentService _assessmentService;

    public AssessmentsController(AssessmentService assessmentService)
    {
        _assessmentService = assessmentService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<Assessment>> GetAll()
    {
        var items = _assessmentService.GetAll();
        return Ok(items);
    }

    public class CreateAssessmentRequest
    {
        public string Organisation { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string Solution { get; set; } = string.Empty;
        public string HlsVersion { get; set; } = "1.0";
    }

    public class UpdatePhaseStatusRequest
    {
        public string Phase { get; set; } = string.Empty;   // bijv. "phase1"
        public string Status { get; set; } = string.Empty;  // bijv. "in_progress" of "done"
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
}
