using System;
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
    /// Haal de MDR-classificatie op voor dit assessment.
    /// Criteria die uit DPIA komen worden automatisch afgeleid.
    /// De uitkomst wordt ook teruggeschreven naar het Assessment (MdrClass + MdrStatus).
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

        // Assessment bijwerken met MDR-uitkomst
        assessment.MdrClass = state.MdrClass;
        assessment.MdrStatus = state.IsComplete
            ? "MDR geclassificeerd"
            : "Onbekend";

        return Ok(state);
    }

    public class UpdateMdrRequest
    {
        /// <summary>
        /// Ernst van de schade bij fout:
        /// "dodelijk_of_onherstelbaar", "ernstig", "niet_ernstig" of "Geen".
        /// </summary>
        public string? ErnstSchadeBijFout { get; set; }
    }

    /// <summary>
    /// Update de MDR-classificatie door de ernst van de schade bij fout in te vullen.
    /// Overige criteria worden automatisch afgeleid uit de DPIA-quickscan.
    /// De uitkomst wordt ook teruggeschreven naar het Assessment (MdrClass + MdrStatus).
    /// </summary>
    [HttpPut]
    public ActionResult<MdrClassificationState> Update(
        Guid assessmentId,
        [FromBody] UpdateMdrRequest request)
    {
        var assessment = _assessmentService.GetById(assessmentId);
        if (assessment == null)
        {
            return NotFound("Assessment not found.");
        }

        var state = _mdrService.UpdateSeverity(assessmentId, request?.ErnstSchadeBijFout);

        // Assessment bijwerken met MDR-uitkomst
        assessment.MdrClass = state.MdrClass;
        assessment.MdrStatus = state.IsComplete
            ? "MDR geclassificeerd"
            : "Onbekend";

        return Ok(state);
    }
}
