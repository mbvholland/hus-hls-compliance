using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers;

[ApiController]
[Route("api/assessments/{assessmentId:guid}/koppelingen")]
public class KoppelingenController : ControllerBase
{
    private readonly KoppelingenService _koppelingenService;
    private readonly AssessmentService _assessmentService;

    public KoppelingenController(KoppelingenService koppelingenService, AssessmentService assessmentService)
    {
        _koppelingenService = koppelingenService;
        _assessmentService = assessmentService;
    }

    /// <summary>
    /// Haal alle koppelingen + aggregated risiconiveau op voor dit assessment.
    /// </summary>
    [HttpGet]
    public ActionResult<KoppelingenResult> Get(Guid assessmentId)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        var result = _koppelingenService.GetOrCreateForAssessment(assessmentId);
        return Ok(result);
    }

    public class UpsertKoppelingRequest
    {
        /// <summary>
        /// Optioneel: als je een bestaande koppeling wilt updaten, kun je de Id meegeven.
        /// Laat leeg voor een nieuwe koppeling.
        /// </summary>
        public Guid? Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string DataSensitivity { get; set; } = string.Empty;

        /// <summary>
        /// Risiconiveau: bijv. "Geen", "Laag", "Middel", "Hoog", "Onbekend".
        /// </summary>
        public string RiskLevel { get; set; } = "Onbekend";
    }

    /// <summary>
    /// Voeg een nieuwe koppeling toe of update een bestaande (indien Id is meegegeven).
    /// </summary>
    [HttpPost]
    public ActionResult<KoppelingenResult> Upsert(Guid assessmentId, [FromBody] UpsertKoppelingRequest request)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        var connection = new Koppeling
        {
            Id = request.Id ?? Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            Direction = request.Direction,
            DataSensitivity = request.DataSensitivity,
            RiskLevel = request.RiskLevel
        };

        var result = _koppelingenService.AddOrUpdateConnection(assessmentId, connection);
        return Ok(result);
    }

    /// <summary>
    /// Verwijder een koppeling op basis van Id.
    /// </summary>
    [HttpDelete("{connectionId:guid}")]
    public ActionResult Delete(Guid assessmentId, Guid connectionId)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        var ok = _koppelingenService.RemoveConnection(assessmentId, connectionId);
        if (!ok)
        {
            return NotFound("Koppeling niet gevonden.");
        }

        return NoContent();
    }
}
