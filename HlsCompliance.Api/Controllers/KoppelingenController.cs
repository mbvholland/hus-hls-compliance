using System;
using System.Linq;
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
    /// Schrijft het overall risiconiveau ook terug naar het Assessment
    /// (ConnectionsOverallRisk + ConnectionsRiskStatus).
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

        // Assessment bijwerken met koppelingen-uitkomst
        assessment.ConnectionsOverallRisk = result.OverallRiskLevel;
        assessment.ConnectionsRiskStatus = result.Connections.Any()
            ? "Koppelingen beoordeeld"
            : (result.OverallRiskLevel == "Geen"
                ? "Geen koppelingen volgens DPIA"
                : "Geen koppelingen geregistreerd");

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

        /// <summary>
        /// Gevoeligheid volgens HLS tab "2. Koppeling-Beslisboom".
        /// Mogelijke waarden:
        /// - "Geen"
        /// - "Laag"
        /// - "Geaggregeerd/geanonimiseerd/pseudoniem"
        /// - "Identificeerbaar medisch of persoon"
        /// Andere waarden worden als "Onbekend" behandeld.
        /// </summary>
        public string DataSensitivity { get; set; } = string.Empty;

        /// <summary>
        /// (Wordt genegeerd) Risiconiveau wordt automatisch bepaald op basis van DataSensitivity.
        /// Deze property blijft alleen voor achterwaartse compatibiliteit.
        /// </summary>
        public string? RiskLevel { get; set; }
    }

    /// <summary>
    /// Voeg een nieuwe koppeling toe of update een bestaande (indien Id is meegegeven).
    /// RiskLevel wordt automatisch berekend uit DataSensitivity.
    /// Schrijft het overall risiconiveau ook terug naar het Assessment
    /// (ConnectionsOverallRisk + ConnectionsRiskStatus).
    /// </summary>
    [HttpPost]
    public ActionResult<KoppelingenResult> Upsert(Guid assessmentId, [FromBody] UpsertKoppelingRequest request)
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
            DataSensitivity = request.DataSensitivity ?? string.Empty,
            // RiskLevel niet uit request overnemen; wordt in de service berekend
        };

        var result = _koppelingenService.AddOrUpdateConnection(assessmentId, connection);

        // Assessment bijwerken met koppelingen-uitkomst
        assessment.ConnectionsOverallRisk = result.OverallRiskLevel;
        assessment.ConnectionsRiskStatus = result.Connections.Any()
            ? "Koppelingen beoordeeld"
            : (result.OverallRiskLevel == "Geen"
                ? "Geen koppelingen volgens DPIA"
                : "Geen koppelingen geregistreerd");

        return Ok(result);
    }

    /// <summary>
    /// Verwijder een koppeling op basis van Id.
    /// Schrijft het overall risiconiveau ook terug naar het Assessment
    /// (ConnectionsOverallRisk + ConnectionsRiskStatus).
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

        var resultAfter = _koppelingenService.GetOrCreateForAssessment(assessmentId);

        // Assessment bijwerken met koppelingen-uitkomst
        assessment.ConnectionsOverallRisk = resultAfter.OverallRiskLevel;
        assessment.ConnectionsRiskStatus = resultAfter.Connections.Any()
            ? "Koppelingen beoordeeld"
            : (resultAfter.OverallRiskLevel == "Geen"
                ? "Geen koppelingen volgens DPIA"
                : "Geen koppelingen geregistreerd");

        return NoContent();
    }
}
