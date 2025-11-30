using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers;

[ApiController]
[Route("api/assessments/{assessmentId:guid}/toets-vooronderzoek")]
public class ToetsVooronderzoekController : ControllerBase
{
    private readonly ToetsVooronderzoekService _toetsVooronderzoekService;
    private readonly AssessmentService _assessmentService;

    public ToetsVooronderzoekController(
        ToetsVooronderzoekService toetsVooronderzoekService,
        AssessmentService assessmentService)
    {
        _toetsVooronderzoekService = toetsVooronderzoekService;
        _assessmentService = assessmentService;
    }

    /// <summary>
    /// Haal de toets vooronderzoek op voor dit assessment.
    /// </summary>
    [HttpGet]
    public ActionResult<ToetsVooronderzoekState> Get(Guid assessmentId)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        var state = _toetsVooronderzoekService.GetOrCreateForAssessment(assessmentId);
        return Ok(state);
    }

    public class UpdateToetsVooronderzoekRequest
    {
        /// <summary>
        /// Is een volledig onderzoek vereist? null = nog niet beoordeeld.
        /// </summary>
        public bool? RequiresFullAssessment { get; set; }

        /// <summary>
        /// Toelichting / motivatie.
        /// </summary>
        public string? Motivation { get; set; }

        /// <summary>
        /// Status, bv. "Onbekend", "Concept", "Definitief".
        /// </summary>
        public string? Status { get; set; }
    }

    /// <summary>
    /// Maak of update de toets vooronderzoek voor dit assessment.
    /// </summary>
    [HttpPut]
    public ActionResult<ToetsVooronderzoekState> Update(
        Guid assessmentId,
        [FromBody] UpdateToetsVooronderzoekRequest request)
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

        var updated = _toetsVooronderzoekService.Update(
            assessmentId,
            request.RequiresFullAssessment,
            request.Motivation,
            request.Status
        );

        return Ok(updated);
    }
}
