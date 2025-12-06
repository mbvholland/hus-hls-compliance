using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssessmentsController : ControllerBase
    {
        private readonly AssessmentService _assessmentService;

        public AssessmentsController(AssessmentService assessmentService)
        {
            _assessmentService = assessmentService;
        }

        // ------------------------------------------------------------
        // DTO's
        // ------------------------------------------------------------

        public class CreateAssessmentRequest
        {
            public string Organisation { get; set; } = string.Empty;
            public string Supplier { get; set; } = string.Empty;
            public string Solution { get; set; } = string.Empty;
            public string HlsVersion { get; set; } = "1.0";
        }

        public class UpdatePhaseStatusRequest
        {
            /// <summary>
            /// Phase key, e.g. "phase1", "phase2", "phase3", "phase4a", "phase4b".
            /// </summary>
            public string Phase { get; set; } = string.Empty;

            /// <summary>
            /// New status, e.g. "not_started", "in_progress", "completed".
            /// </summary>
            public string Status { get; set; } = string.Empty;
        }

        public class DueDiligenceDecisionRequest
        {
            /// <summary>
            /// "stop" of "go_to_contract".
            /// </summary>
            public string Decision { get; set; } = string.Empty;

            /// <summary>
            /// Optionele motivatie voor het besluit (verplicht bij "stop").
            /// </summary>
            public string? Motivation { get; set; }

            /// <summary>
            /// Wie het besluit neemt (bijv. "CISO", "Inkoopcommissie").
            /// </summary>
            public string? DecisionBy { get; set; }

            /// <summary>
            /// Datum van het besluit.
            /// </summary>
            public DateTime? DecisionDate { get; set; }
        }

        // ------------------------------------------------------------
        // Endpoints
        // ------------------------------------------------------------

        /// <summary>
        /// Haal alle assessments op.
        /// </summary>
        [HttpGet]
        public ActionResult<IEnumerable<Assessment>> GetAll()
        {
            var items = _assessmentService.GetAll();
            return Ok(items);
        }

        /// <summary>
        /// Maak een nieuw assessment aan.
        /// </summary>
        [HttpPost]
        public ActionResult<Assessment> Create([FromBody] CreateAssessmentRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

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

        /// <summary>
        /// Haal één assessment op.
        /// </summary>
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

        /// <summary>
        /// Maak op basis van een bestaand assessment (id) een nieuwe versie
        /// met een nieuw AssessmentId, verhoogde AssessmentVersion (Vn -> Vn+1),
        /// nieuwe DueDiligenceDate en gekloonde Due Diligence-data
        /// (tab 8/11 + kolom K/M + ToetsVooronderzoek-handmatige antwoorden).
        /// </summary>
        [HttpPost("{id:guid}/new-version")]
        public ActionResult<Assessment> CreateNewVersion(Guid id)
        {
            // Stap 1: nieuwe Assessment op basis van bestaande meta + versienummering.
            var newAssessment = _assessmentService.CreateNewVersionFromExisting(id);
            if (newAssessment == null)
            {
                return NotFound("Assessment not found.");
            }

            // Stap 2: clone alle relevante Due Diligence-data naar de nieuwe AssessmentId.
            CloneAnswers(id, newAssessment.Id);
            CloneEvidence(id, newAssessment.Id);
            CloneDueDiligenceDecisions(id, newAssessment.Id);
            CloneToetsVooronderzoekManualAnswers(id, newAssessment.Id);

            return Ok(newAssessment);
        }

        /// <summary>
        /// Update de status van een fase (fase 1–4a/4b).
        /// </summary>
        [HttpPatch("{id:guid}/phase-status")]
        public ActionResult UpdatePhaseStatus(Guid id, [FromBody] UpdatePhaseStatusRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

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

        /// <summary>
        /// Registreer de eindbeslissing van de Due Diligence (F3: Stop / Ga door naar contractering).
        /// </summary>
        [HttpPost("{id:guid}/due-diligence-decision")]
        public ActionResult<Assessment> UpdateDueDiligenceDecision(Guid id, [FromBody] DueDiligenceDecisionRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Decision))
            {
                return BadRequest("Decision is required (use 'stop' or 'go_to_contract').");
            }

            var normalizedDecision = request.Decision.Trim().ToLowerInvariant();
            if (normalizedDecision != "stop" && normalizedDecision != "go_to_contract")
            {
                return BadRequest("Decision must be 'stop' or 'go_to_contract'.");
            }

            // Governance-regel: bij 'stop' is motivatie verplicht.
            if (normalizedDecision == "stop" && string.IsNullOrWhiteSpace(request.Motivation))
            {
                return BadRequest("Motivation is required when decision is 'stop'.");
            }

            var ok = _assessmentService.UpdateDueDiligenceFinalDecision(
                id,
                normalizedDecision,
                request.Motivation,
                request.DecisionBy,
                request.DecisionDate ?? DateTime.UtcNow
            );

            if (!ok)
            {
                return NotFound("Assessment not found.");
            }

            var updated = _assessmentService.GetById(id);
            return Ok(updated);
        }

        // ------------------------------------------------------------
        // Helpers voor versie-klonen (tab 8/11 + kolom K/M + ToetsVooronderzoek)
        // ------------------------------------------------------------

        private void CloneAnswers(Guid sourceAssessmentId, Guid targetAssessmentId)
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, "Data", "answers.json");

                if (!System.IO.File.Exists(filePath))
                {
                    return;
                }

                var json = System.IO.File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var list = JsonSerializer.Deserialize<List<AnswerRecord>>(json, options)
                           ?? new List<AnswerRecord>();

                var sourceItems = list
                    .Where(x => x.AssessmentId == sourceAssessmentId)
                    .ToList();

                if (!sourceItems.Any())
                {
                    return;
                }

                // Verwijder eventuele bestaande antwoorden voor het doelassessment.
                list.RemoveAll(x => x.AssessmentId == targetAssessmentId);

                foreach (var src in sourceItems)
                {
                    list.Add(new AnswerRecord
                    {
                        AssessmentId = targetAssessmentId,
                        ChecklistId = src.ChecklistId,
                        RawAnswer = src.RawAnswer,
                        AnswerEvaluation = src.AnswerEvaluation
                    });
                }

                var saveOptions = new JsonSerializerOptions { WriteIndented = true };
                var updatedJson = JsonSerializer.Serialize(list, saveOptions);
                System.IO.File.WriteAllText(filePath, updatedJson);
            }
            catch
            {
                // Eventuele fouten bij klonen van antwoorden mogen het aanmaken van een nieuwe versie niet blokkeren.
            }
        }

        private void CloneEvidence(Guid sourceAssessmentId, Guid targetAssessmentId)
        {
            try
            {
                var basePath = AppContext.BaseDirectory;
                var dataDir = Path.Combine(basePath, "Data");
                var filePath = Path.Combine(dataDir, "assessment_evidence.json");

                if (!System.IO.File.Exists(filePath))
                {
                    return;
                }

                var json = System.IO.File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var list = JsonSerializer.Deserialize<List<EvidenceRecord>>(json, options)
                           ?? new List<EvidenceRecord>();

                var sourceItems = list
                    .Where(x => x.AssessmentId == sourceAssessmentId)
                    .ToList();

                if (!sourceItems.Any())
                {
                    return;
                }

                // Verwijder bestaande bewijsitems voor het doelassessment.
                list.RemoveAll(x => x.AssessmentId == targetAssessmentId);

                foreach (var src in sourceItems)
                {
                    list.Add(new EvidenceRecord
                    {
                        AssessmentId = targetAssessmentId,
                        ChecklistId = src.ChecklistId,
                        EvidenceId = src.EvidenceId,
                        EvidenceName = src.EvidenceName,
                        Status = src.Status,
                        Comment = src.Comment
                    });
                }

                var saveOptions = new JsonSerializerOptions { WriteIndented = true };
                var updatedJson = JsonSerializer.Serialize(list, saveOptions);
                System.IO.File.WriteAllText(filePath, updatedJson);
            }
            catch
            {
                // Eventuele fouten bij klonen van evidence mogen het aanmaken van een nieuwe versie niet blokkeren.
            }
        }

        private void CloneDueDiligenceDecisions(Guid sourceAssessmentId, Guid targetAssessmentId)
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, "Data", "due-diligence-decisions.json");

                if (!System.IO.File.Exists(filePath))
                {
                    return;
                }

                var json = System.IO.File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var list = JsonSerializer.Deserialize<List<DueDiligenceDecisionRecord>>(json, options)
                           ?? new List<DueDiligenceDecisionRecord>();

                var sourceItems = list
                    .Where(x => x.AssessmentId == sourceAssessmentId)
                    .ToList();

                if (!sourceItems.Any())
                {
                    return;
                }

                // Verwijder bestaande beslissingen voor het doelassessment.
                list.RemoveAll(x => x.AssessmentId == targetAssessmentId);

                foreach (var src in sourceItems)
                {
                    list.Add(new DueDiligenceDecisionRecord
                    {
                        AssessmentId = targetAssessmentId,
                        ChecklistId = src.ChecklistId,
                        NegativeOutcomeAcceptable = src.NegativeOutcomeAcceptable,
                        DeviationText = src.DeviationText
                    });
                }

                var saveOptions = new JsonSerializerOptions { WriteIndented = true };
                var updatedJson = JsonSerializer.Serialize(list, saveOptions);
                System.IO.File.WriteAllText(filePath, updatedJson);
            }
            catch
            {
                // Eventuele fouten bij klonen van kolom K/M-beslissingen mogen het aanmaken van een nieuwe versie niet blokkeren.
            }
        }

        private void CloneToetsVooronderzoekManualAnswers(Guid sourceAssessmentId, Guid targetAssessmentId)
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, "Data", "toetsvooronderzoek-manual.json");

                if (!System.IO.File.Exists(filePath))
                {
                    return;
                }

                var json = System.IO.File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var list = JsonSerializer.Deserialize<List<ToetsManualAnswerRecord>>(json, options)
                           ?? new List<ToetsManualAnswerRecord>();

                var sourceItems = list
                    .Where(x => x.AssessmentId == sourceAssessmentId)
                    .ToList();

                if (!sourceItems.Any())
                {
                    return;
                }

                // Verwijder bestaande handmatige antwoorden voor het doelassessment.
                list.RemoveAll(x => x.AssessmentId == targetAssessmentId);

                foreach (var src in sourceItems)
                {
                    list.Add(new ToetsManualAnswerRecord
                    {
                        AssessmentId = targetAssessmentId,
                        ToetsId = src.ToetsId,
                        Answer = src.Answer
                    });
                }

                var saveOptions = new JsonSerializerOptions { WriteIndented = true };
                var updatedJson = JsonSerializer.Serialize(list, saveOptions);
                System.IO.File.WriteAllText(filePath, updatedJson);
            }
            catch
            {
                // Eventuele fouten bij klonen van ToetsVooronderzoek-antwoorden mogen het aanmaken van een nieuwe versie niet blokkeren.
            }
        }

        // DTO's voor interne JSON-serialisatie van de verschillende Data-bestanden

        private class AnswerRecord
        {
            public Guid AssessmentId { get; set; }
            public string? ChecklistId { get; set; }
            public string? RawAnswer { get; set; }
            public string? AnswerEvaluation { get; set; }
        }

        private class EvidenceRecord
        {
            public Guid AssessmentId { get; set; }
            public string? ChecklistId { get; set; }
            public string? EvidenceId { get; set; }
            public string? EvidenceName { get; set; }
            public string? Status { get; set; }
            public string? Comment { get; set; }
        }

        private class DueDiligenceDecisionRecord
        {
            public Guid AssessmentId { get; set; }
            public string? ChecklistId { get; set; }
            public bool NegativeOutcomeAcceptable { get; set; }
            public string? DeviationText { get; set; }
        }

        private class ToetsManualAnswerRecord
        {
            public Guid AssessmentId { get; set; }
            public string? ToetsId { get; set; }
            public bool? Answer { get; set; }
        }
    }
}
