using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    /// <summary>
    /// DTO voor het uitwisselen van antwoorden (beoordelingen) op controlevragen (tab 8).
    /// Let op: RawAnswer mag leeg zijn; in jouw proces gebruik je alleen AnswerEvaluation.
    /// </summary>
    public class AssessmentAnswerDto
    {
        public string ChecklistId { get; set; } = string.Empty;

        /// <summary>
        /// Optioneel veld voor het ruwe antwoord van de leverancier.
        /// In jouw workflow mag dit leeg blijven; je gebruikt alleen AnswerEvaluation.
        /// </summary>
        public string? RawAnswer { get; set; }

        /// <summary>
        /// Jouw beoordeling van het antwoord:
        /// "Goedgekeurd", "Deels goedgekeurd", "Afgekeurd",
        /// of "Nog niet goedgekeurd i.a.v. toelichting".
        /// </summary>
        public string? AnswerEvaluation { get; set; }
    }

    [ApiController]
    [Route("api/assessments/{assessmentId:guid}/answers")]
    public class AssessmentAnswersController : ControllerBase
    {
        private readonly IAssessmentAnswersRepository _repository;

        public AssessmentAnswersController(IAssessmentAnswersRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// Haalt alle antwoorden (beoordelingen) op voor een assessment.
        /// Dit komt overeen met tab 8: per ChecklistId het antwoord + jouw beoordeling.
        /// </summary>
        [HttpGet]
        public ActionResult<IEnumerable<AssessmentAnswerDto>> Get(Guid assessmentId)
        {
            var items = _repository
                .GetByAssessment(assessmentId)
                .Select(a => new AssessmentAnswerDto
                {
                    ChecklistId = a.ChecklistId,
                    RawAnswer = a.RawAnswer,
                    AnswerEvaluation = a.AnswerEvaluation
                })
                .ToList();

            return Ok(items);
        }

        /// <summary>
        /// Upsert van antwoorden (beoordelingen) voor een assessment.
        /// Je hoeft alleen ChecklistId + AnswerEvaluation te vullen.
        /// RawAnswer mag leeg blijven als je de inhoudelijke antwoorden
        /// buiten de app bewaart.
        /// </summary>
        [HttpPut]
        public IActionResult Upsert(Guid assessmentId, [FromBody] IEnumerable<AssessmentAnswerDto> request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            var answers = request
                .Where(r => !string.IsNullOrWhiteSpace(r.ChecklistId))
                .Select(r => new AssessmentQuestionAnswer
                {
                    AssessmentId = assessmentId,                  // geforceerd in repo ook
                    ChecklistId = r.ChecklistId,
                    RawAnswer = r.RawAnswer,                      // mag null zijn
                    AnswerEvaluation = r.AnswerEvaluation         // hier zit jouw echte werk
                })
                .ToList();

            if (!answers.Any())
            {
                return BadRequest("No valid answers in request.");
            }

            _repository.UpsertAnswers(assessmentId, answers);

            return NoContent();
        }
    }
}
