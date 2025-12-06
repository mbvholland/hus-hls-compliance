using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    /// <summary>
    /// API-controller voor de due diligence-checklist (tab 7) en voortgangsrapport.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DueDiligenceController : ControllerBase
    {
        private readonly DueDiligenceService _dueDiligenceService;
        private readonly IChecklistDefinitionRepository _definitionRepository;
        private readonly IAssessmentAnswersRepository _answersRepository;
        private readonly IAssessmentEvidenceRepository _evidenceRepository;
        private readonly AssessmentService _assessmentService;
        private readonly ToetsVooronderzoekService _toetsVooronderzoekService;

        public DueDiligenceController(
            DueDiligenceService dueDiligenceService,
            IChecklistDefinitionRepository definitionRepository,
            IAssessmentAnswersRepository answersRepository,
            IAssessmentEvidenceRepository evidenceRepository,
            AssessmentService assessmentService,
            ToetsVooronderzoekService toetsVooronderzoekService)
        {
            _dueDiligenceService = dueDiligenceService ?? throw new ArgumentNullException(nameof(dueDiligenceService));
            _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
            _answersRepository = answersRepository ?? throw new ArgumentNullException(nameof(answersRepository));
            _evidenceRepository = evidenceRepository ?? throw new ArgumentNullException(nameof(evidenceRepository));
            _assessmentService = assessmentService ?? throw new ArgumentNullException(nameof(assessmentService));
            _toetsVooronderzoekService = toetsVooronderzoekService ?? throw new ArgumentNullException(nameof(toetsVooronderzoekService));
        }

        // --------------------------------------------------------------------
        // 1) Checklist voor één assessment
        // --------------------------------------------------------------------

        /// <summary>
        /// Haalt de due diligence-checklist op voor een assessment.
        /// - definities: uit IChecklistDefinitionRepository,
        /// - antwoorden: uit IAssessmentAnswersRepository,
        /// - bewijslast: uit IAssessmentEvidenceRepository,
        /// - toepasbaarheid: op basis van ToetsVooronderzoek (tab 6),
        /// - logica voor BewijsResultaat en Resultaat due diligence is actief.
        /// </summary>
        [HttpGet("{assessmentId:guid}/checklist")]
        public ActionResult<List<DueDiligenceChecklistRowDto>> GetChecklist(Guid assessmentId)
        {
            var definitions = _definitionRepository.GetAll().ToList();
            var answers = _answersRepository.GetByAssessment(assessmentId);
            var evidence = _evidenceRepository.GetByAssessment(assessmentId);

            // ToetsVooronderzoek ophalen incl. ToetsAnswers
            var tvResult = _toetsVooronderzoekService.Get(assessmentId);
            var toetsAnswers = tvResult?.ToetsAnswers;

            var rows = _dueDiligenceService.BuildChecklistRows(
                assessmentId,
                definitions,
                answers,
                evidence,
                toetsAnswers);

            var dtoList = (from row in rows
                           join def in definitions on row.ChecklistId equals def.ChecklistId
                               into defJoin
                           from def in defJoin.DefaultIfEmpty()
                           select new DueDiligenceChecklistRowDto
                           {
                               AssessmentId = row.AssessmentId,
                               ChecklistId = row.ChecklistId,

                               Category = def?.Category ?? string.Empty,
                               Question = def?.Question ?? string.Empty,
                               NormReference = def?.NormReference ?? string.Empty,
                               RiskLevel = def?.RiskLevel ?? string.Empty,

                               IsApplicable = row.IsApplicable,
                               Answer = row.Answer,
                               AnswerEvaluation = row.AnswerEvaluation,
                               EvidenceSummary = row.EvidenceSummary,
                               NegativeOutcomeAcceptable = row.NegativeOutcomeAcceptable,
                               DueDiligenceOutcome = row.DueDiligenceOutcome,
                               DeviationText = row.DeviationText
                           })
                           .ToList();

            return Ok(dtoList);
        }

        // --------------------------------------------------------------------
        // 2) Kolom K/M: Negatief resultaat acceptabel + afwijkingstekst
        // --------------------------------------------------------------------

        /// <summary>
        /// Update kolom K (Negatief resultaat acceptabel?) en kolom M (Afwijkingstekst) voor één checklist-vraag.
        /// </summary>
        [HttpPost("{assessmentId:guid}/decision")]
        public IActionResult UpdateDecision(Guid assessmentId, [FromBody] UpdateDecisionRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.ChecklistId))
            {
                return BadRequest("ChecklistId is required.");
            }

            _dueDiligenceService.UpdateNegativeOutcomeDecision(
                assessmentId,
                request.ChecklistId,
                request.NegativeOutcomeAcceptable,
                request.DeviationText);

            return NoContent();
        }

        // --------------------------------------------------------------------
        // 3) Voortgangsrapport Due Diligence (incl. eindbeslissing uit Assessment)
        // --------------------------------------------------------------------

        /// <summary>
        /// Voortgangsrapport voor de Due Diligence van één assessment:
        /// - headerblok (tab 0 + DD-context),
        /// - aantallen vragen per status/uitkomst,
        /// - inhoudelijk DD-resultaat (G2-equivalent: geaccepteerd / niet geaccepteerd / nog te beoordelen),
        /// - geregistreerde eindbeslissing (stop / go_to_contract) uit Assessment,
        /// - consistentie tussen eindbeslissing en inhoudelijke uitkomst (Voldoet niet).
        /// </summary>
        [HttpGet("{assessmentId:guid}/report")]
        public ActionResult<DueDiligenceReportDto> GetReport(Guid assessmentId)
        {
            var assessment = _assessmentService.GetById(assessmentId);
            if (assessment == null)
            {
                return NotFound("Assessment not found.");
            }

            var definitions = _definitionRepository.GetAll().ToList();
            var answers = _answersRepository.GetByAssessment(assessmentId);
            var evidence = _evidenceRepository.GetByAssessment(assessmentId);

            var tvResult = _toetsVooronderzoekService.Get(assessmentId);
            var toetsAnswers = tvResult?.ToetsAnswers;

            var rows = _dueDiligenceService.BuildChecklistRows(
                assessmentId,
                definitions,
                answers,
                evidence,
                toetsAnswers);

            var applicableRows = rows.Where(r => r.IsApplicable).ToList();
            var notApplicableRows = rows.Where(r => !r.IsApplicable).ToList();

            int totalQuestions = rows.Count;
            int applicableQuestions = applicableRows.Count;
            int notApplicableQuestions = notApplicableRows.Count;

            int compliesCount = applicableRows.Count(r => r.DueDiligenceOutcome == "Voldoet");
            int notCompliesCount = applicableRows.Count(r => r.DueDiligenceOutcome == "Voldoet niet");
            int deviationAcceptableCount = applicableRows.Count(r => r.DueDiligenceOutcome == "Afwijking acceptabel");
            int toBeAssessedCount = applicableRows.Count(r =>
                string.IsNullOrWhiteSpace(r.DueDiligenceOutcome) ||
                r.DueDiligenceOutcome == "Nog te beoordelen");

            int completedQuestions = applicableRows.Count - toBeAssessedCount;

            // Procesmatige status
            string overallStatus;
            if (notCompliesCount > 0)
            {
                overallStatus = "Due diligence bevat niet-acceptabele afwijkingen.";
            }
            else if (toBeAssessedCount > 0)
            {
                overallStatus = "Due diligence is nog niet volledig beoordeeld.";
            }
            else
            {
                overallStatus = "Due diligence is volledig beoordeeld.";
            }

            // Inhoudelijk DD-resultaat (G2-equivalent)
            string resultDueDiligence;
            if (notCompliesCount > 0)
            {
                resultDueDiligence = "Niet geaccepteerd";
            }
            else if (toBeAssessedCount > 0)
            {
                resultDueDiligence = "Nog te beoordelen";
            }
            else
            {
                resultDueDiligence = "Geaccepteerd";
            }

            // Governance-check: eindbeslissing vs. aanwezigheid 'Voldoet niet'
            bool anyNotComplies = notCompliesCount > 0;

            bool? isFinalDecisionConsistent = null;
            string? finalDecisionWarning = null;

            if (!string.IsNullOrWhiteSpace(assessment.DueDiligenceFinalDecision))
            {
                var decisionNorm = assessment.DueDiligenceFinalDecision.Trim().ToLowerInvariant();

                if (decisionNorm == "go_to_contract")
                {
                    if (anyNotComplies)
                    {
                        isFinalDecisionConsistent = false;
                        finalDecisionWarning =
                            "Er zijn nog vragen met 'Voldoet niet' terwijl de eindbeslissing 'go_to_contract' is.";
                    }
                    else
                    {
                        isFinalDecisionConsistent = true;
                    }
                }
                else if (decisionNorm == "stop")
                {
                    if (anyNotComplies)
                    {
                        // Stoppen terwijl er niet-acceptabele bevindingen zijn is inhoudelijk consistent.
                        isFinalDecisionConsistent = true;
                    }
                    else
                    {
                        // Strenger dan nodig: geen 'Voldoet niet', maar toch stop.
                        isFinalDecisionConsistent = false;
                        finalDecisionWarning =
                            "Eindbeslissing 'stop' terwijl er geen vragen met 'Voldoet niet' zijn.";
                    }
                }
            }

            var header = new AssessmentHeaderDto
            {
                AssessmentId = assessment.Id,
                Organisation = assessment.Organisation,
                Supplier = assessment.Supplier,
                Solution = assessment.Solution,
                HlsVersion = assessment.HlsVersion,
                CreatedAt = assessment.CreatedAt,
                UpdatedAt = assessment.UpdatedAt,

                DpiaRequired = assessment.DpiaRequired,
                DpiaRiskScore = assessment.DpiaRiskScore,

                AiActRiskLevel = assessment.AiActRiskLevel,
                MdrClass = assessment.MdrClass,
                SecurityProfileRiskScore = assessment.SecurityProfileRiskScore,
                ConnectionsOverallRisk = assessment.ConnectionsOverallRisk,

                OverallRiskScore = assessment.OverallRiskScore,
                OverallRiskClass = assessment.OverallRiskClass,
                OverallRiskLabel = assessment.OverallRiskLabel
            };

            var dto = new DueDiligenceReportDto
            {
                Header = header,

                TotalQuestions = totalQuestions,
                ApplicableQuestions = applicableQuestions,
                NotApplicableQuestions = notApplicableQuestions,
                CompletedQuestions = completedQuestions,

                CompliesCount = compliesCount,
                NotCompliesCount = notCompliesCount,
                DeviationAcceptableCount = deviationAcceptableCount,
                ToBeAssessedCount = toBeAssessedCount,

                OverallStatus = overallStatus,
                ResultDueDiligence = resultDueDiligence,

                FinalDecision = assessment.DueDiligenceFinalDecision,
                FinalDecisionMotivation = assessment.DueDiligenceFinalDecisionMotivation,
                FinalDecisionBy = assessment.DueDiligenceFinalDecisionBy,
                FinalDecisionDate = assessment.DueDiligenceFinalDecisionDate,

                IsFinalDecisionConsistent = isFinalDecisionConsistent,
                FinalDecisionWarning = finalDecisionWarning,

                LastUpdatedAt = assessment.UpdatedAt
            };

            return Ok(dto);
        }

        // --------------------------------------------------------------------
        // DTO's
        // --------------------------------------------------------------------

        public class DueDiligenceChecklistRowDto
        {
            public Guid AssessmentId { get; set; }
            public string ChecklistId { get; set; } = string.Empty;

            public string Category { get; set; } = string.Empty;
            public string Question { get; set; } = string.Empty;
            public string NormReference { get; set; } = string.Empty;
            public string RiskLevel { get; set; } = string.Empty;

            public bool IsApplicable { get; set; }

            public string? Answer { get; set; }
            public string? AnswerEvaluation { get; set; }

            public string? EvidenceSummary { get; set; }

            public bool NegativeOutcomeAcceptable { get; set; }

            public string? DueDiligenceOutcome { get; set; }

            public string? DeviationText { get; set; }
        }

        public class UpdateDecisionRequest
        {
            public string ChecklistId { get; set; } = string.Empty;
            public bool NegativeOutcomeAcceptable { get; set; }
            public string? DeviationText { get; set; }
        }

        /// <summary>
        /// Headerblok van een assessment (tab 0 + kern-DD-context).
        /// </summary>
        public class AssessmentHeaderDto
        {
            public Guid AssessmentId { get; set; }

            public string Organisation { get; set; } = string.Empty;
            public string Supplier { get; set; } = string.Empty;
            public string Solution { get; set; } = string.Empty;

            /// <summary>
            /// Versie van de HLS/app-release waarmee de assessment is uitgevoerd.
            /// </summary>
            public string HlsVersion { get; set; } = string.Empty;

            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }

            /// <summary>
            /// Resultaat van de DPIA-quickscan (true = DPIA vereist).
            /// </summary>
            public bool? DpiaRequired { get; set; }

            /// <summary>
            /// DPIA-risicoscore uit de quickscan (Excel: DPIA_Quickscan!E18).
            /// </summary>
            public double? DpiaRiskScore { get; set; }

            /// <summary>
            /// Risicoklasse onder de AI Act (zoals vastgelegd in het assessment).
            /// </summary>
            public string? AiActRiskLevel { get; set; }

            /// <summary>
            /// MDR-risicoklasse van de oplossing.
            /// </summary>
            public string? MdrClass { get; set; }

            /// <summary>
            /// Score van het securityprofiel (indien beschikbaar).
            /// </summary>
            public double? SecurityProfileRiskScore { get; set; }

            /// <summary>
            /// Overall risicobeoordeling van koppelingen (Geen / Laag / Midden / Hoog / Onbekend).
            /// </summary>
            public string? ConnectionsOverallRisk { get; set; }

            /// <summary>
            /// Overkoepelende risicoscore C10 uit tab "0. Algemeen".
            /// </summary>
            public double? OverallRiskScore { get; set; }

            /// <summary>
            /// Overkoepelende risicoklasse C11 (0..n) uit tab "0. Algemeen".
            /// </summary>
            public int? OverallRiskClass { get; set; }

            /// <summary>
            /// Overkoepelend risicolabel B10 uit tab "0. Algemeen".
            /// </summary>
            public string? OverallRiskLabel { get; set; }
        }

        public class DueDiligenceReportDto
        {
            /// <summary>
            /// Headerblok van de assessment incl. DD-context en overall risico.
            /// </summary>
            public AssessmentHeaderDto Header { get; set; } = new();

            public int TotalQuestions { get; set; }
            public int ApplicableQuestions { get; set; }
            public int NotApplicableQuestions { get; set; }
            public int CompletedQuestions { get; set; }

            public int CompliesCount { get; set; }
            public int NotCompliesCount { get; set; }
            public int DeviationAcceptableCount { get; set; }
            public int ToBeAssessedCount { get; set; }

            /// <summary>
            /// Korte tekstuele processtatus van de Due Diligence.
            /// </summary>
            public string OverallStatus { get; set; } = string.Empty;

            /// <summary>
            /// Inhoudelijk resultaat van de Due Diligence (G2-equivalent):
            /// - "Geaccepteerd"
            /// - "Niet geaccepteerd"
            /// - "Nog te beoordelen"
            /// </summary>
            public string ResultDueDiligence { get; set; } = string.Empty;

            /// <summary>
            /// Eindbeslissing DD: "stop" of "go_to_contract".
            /// </summary>
            public string? FinalDecision { get; set; }

            /// <summary>
            /// Motivatie bij het besluit.
            /// </summary>
            public string? FinalDecisionMotivation { get; set; }

            /// <summary>
            /// Wie het besluit heeft genomen (rol/naam).
            /// </summary>
            public string? FinalDecisionBy { get; set; }

            /// <summary>
            /// Datum van het besluit.
            /// </summary>
            public DateTime? FinalDecisionDate { get; set; }

            /// <summary>
            /// True = eindbeslissing is inhoudelijk in lijn met de DD-uitkomst,
            /// False = eindbeslissing wijkt af van de DD-uitkomst,
            /// null = geen eindbeslissing bekend.
            /// </summary>
            public bool? IsFinalDecisionConsistent { get; set; }

            /// <summary>
            /// Waarschuwing/attentie indien eindbeslissing afwijkt van de DD-uitkomst
            /// (bijv. 'go_to_contract' terwijl er nog 'Voldoet niet' is).
            /// </summary>
            public string? FinalDecisionWarning { get; set; }

            public DateTime? LastUpdatedAt { get; set; }
        }
    }
}
