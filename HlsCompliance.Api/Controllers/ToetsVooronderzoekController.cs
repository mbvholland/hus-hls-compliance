using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    [ApiController]
    [Route("api/assessments/{assessmentId:guid}/toets-vooronderzoek")]
    public class ToetsVooronderzoekController : ControllerBase
    {
        private readonly ToetsVooronderzoekService _service;

        public ToetsVooronderzoekController(ToetsVooronderzoekService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Haal het volledige ToetsVooronderzoek-resultaat op voor een assessment.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ToetsVooronderzoekResult), 200)]
        public ActionResult<ToetsVooronderzoekResult> Get(Guid assessmentId)
        {
            var result = _service.Get(assessmentId);
            return Ok(result);
        }

        /// <summary>
        /// Update handmatige J/N-antwoorden (bijv. LHV-acceptatie) voor ToetsVooronderzoek.
        /// Afgeleide vragen worden genegeerd en altijd herberekend door de service.
        /// </summary>
        [HttpPut]
        [ProducesResponseType(typeof(ToetsVooronderzoekResult), 200)]
        [ProducesResponseType(400)]
        public ActionResult<ToetsVooronderzoekResult> Put(
            Guid assessmentId,
            [FromBody] ToetsVooronderzoekUpdateRequest request)
        {
            if (request == null || request.Answers == null)
            {
                return BadRequest("No answers supplied.");
            }

            var updates = request.Answers
                .Select(a => (a.ToetsId, a.Answer));

            var result = _service.UpdateManualAnswers(assessmentId, updates);
            return Ok(result);
        }
    }

    /// <summary>
    /// DTO voor het bijwerken van handmatige ToetsVooronderzoek-antwoorden.
    /// </summary>
    public class ToetsVooronderzoekUpdateRequest
    {
        public List<ToetsVooronderzoekManualAnswerDto> Answers { get; set; } = new();
    }

    public class ToetsVooronderzoekManualAnswerDto
    {
        public string ToetsId { get; set; } = default!;

        /// <summary>
        /// J/N (true = Ja, false = Nee, null = leegmaken).
        /// </summary>
        public bool? Answer { get; set; }
    }
}
