using System;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    [ApiController]
    [Route("api/assessments/{assessmentId:guid}/algemeen")]
    public class AlgemeenController : ControllerBase
    {
        private readonly AlgemeenService _algemeenService;

        public AlgemeenController(AlgemeenService algemeenService)
        {
            _algemeenService = algemeenService ?? throw new ArgumentNullException(nameof(algemeenService));
        }

        /// <summary>
        /// Haal de algemene informatie (tab 0. Algemeen) voor dit assessment op.
        /// C10/C11/B10 worden automatisch herberekend.
        /// </summary>
        [HttpGet]
        public ActionResult<AlgemeenInfoResult> Get(Guid assessmentId)
        {
            var result = _algemeenService.Get(assessmentId);
            return Ok(result);
        }

        /// <summary>
        /// Werk de algemene informatie (tab 0. Algemeen) bij.
        /// Alleen niet-null velden uit het request worden toegepast.
        /// De overall risico-score wordt automatisch herberekend.
        /// </summary>
        [HttpPut]
        public ActionResult<AlgemeenInfoResult> Update(Guid assessmentId, [FromBody] AlgemeenUpdateRequest request)
        {
            var result = _algemeenService.Update(assessmentId, request);
            return Ok(result);
        }
    }
}
