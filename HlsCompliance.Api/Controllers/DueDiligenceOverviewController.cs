using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HlsCompliance.Api.Controllers
{
    /// <summary>
    /// Overzicht van de Due Diligence voortgang per assessment.
    /// </summary>
    [ApiController]
    [Route("api/DueDiligence")]
    public class DueDiligenceOverviewController : ControllerBase
    {
        private readonly AssessmentService _assessmentService;
        private readonly DueDiligenceService _dueDiligenceService;
        private readonly IChecklistDefinitionRepository _definitionRepository;
        private readonly IAssessmentAnswersRepository _answersRepository;
        private readonly IAssessmentEvidenceRepository _evidenceRepository;

        public DueDiligenceOverviewController(
            AssessmentService assessmentService,
            DueDiligenceService dueDiligenceService,
            IChecklistDefinitionRepository definitionRepository,
            IAssessmentAnswersRepository answersRepository,
            IAssessmentEvidenceRepository evidenceRepository)
        {
            _assessmentService = assessmentService ?? throw new ArgumentNullException(nameof(assessmentService));
            _dueDiligenceService = dueDiligenceService ?? throw new ArgumentNullException(nameof(dueDiligenceService));
            _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
            _answersRepository = answersRepository ?? throw new ArgumentNullException(nameof(answersRepository));
            _evidenceRepository = evidenceRepository ?? throw new ArgumentNullException(nameof(evidenceRepository));
        }

        /// <summary>
        /// Voortgangsoverzicht van Due Diligence voor alle assessments.
        /// Eén regel per assessment met tellingen van uitkomsten en het eindoordeel.
        /// </summary>
        [HttpGet("overview")]
        public ActionResult<List<DueDiligenceOverviewItemDto>> GetOverview()
        {
            var assessments = _assessmentService.GetAll().ToList();
            var definitions = _definitionRepository.GetAll();

            var result = new List<DueDiligenceOverviewItemDto>();

            foreach (var assessment in assessments)
            {
                // Haal antwoorden en bewijslast op voor dit assessment
                var answers = _answersRepository.GetByAssessment(assessment.Id);
                var evidence = _evidenceRepository.GetByAssessment(assessment.Id);

                // Bouw de volledige checklist-rijen (met alle logica)
                var rows = _dueDiligenceService.BuildChecklistRows(
                    assessment.Id,
                    definitions,
                    answers,
                    evidence);

                int totalQuestions = rows.Count;

                int countVoldoet = 0;
                int countVoldoetNiet = 0;
                int countAfwijkingAcceptabel = 0;
                int countNogTeBeoordelen = 0;
                int countNietVanToepassing = 0;

                foreach (var row in rows)
                {
                    switch (row.DueDiligenceOutcome)
                    {
                        case "Voldoet":
                            countVoldoet++;
                            break;
                        case "Voldoet niet":
                            countVoldoetNiet++;
                            break;
                        case "Afwijking acceptabel":
                            countAfwijkingAcceptabel++;
                            break;
                        case "Nog te beoordelen":
                            countNogTeBeoordelen++;
                            break;
                        case "Niet van toepassing":
                            countNietVanToepassing++;
                            break;
                    }
                }

                // "Niet acceptabel" = er is minstens één rij met "Voldoet niet"
                // én NegativeOutcomeAcceptable == false
                bool hasNotAcceptable = rows.Any(r =>
                    string.Equals(r.DueDiligenceOutcome, "Voldoet niet", StringComparison.OrdinalIgnoreCase)
                    && !r.NegativeOutcomeAcceptable);

                string? decision = assessment.DueDiligenceFinalDecision;
                string? decisionLabel;

                if (string.IsNullOrWhiteSpace(decision))
                {
                    decisionLabel = "Nog geen besluit";
                }
                else if (decision.Equals("stop", StringComparison.OrdinalIgnoreCase))
                {
                    decisionLabel = "Stop (niet verder naar contractering)";
                }
                else if (decision.Equals("go_to_contract", StringComparison.OrdinalIgnoreCase))
                {
                    decisionLabel = "Doorgaan naar contractering";
                }
                else
                {
                    decisionLabel = decision; // fallback, zou niet moeten voorkomen
                }

                var item = new DueDiligenceOverviewItemDto
                {
                    AssessmentId = assessment.Id,
                    Organisation = assessment.Organisation,
                    Supplier = assessment.Supplier,
                    Solution = assessment.Solution,

                    Phase2Status = assessment.Phase2Status,
                    Phase3Status = assessment.Phase3Status,

                    TotalQuestions = totalQuestions,
                    CountVoldoet = countVoldoet,
                    CountVoldoetNiet = countVoldoetNiet,
                    CountAfwijkingAcceptabel = countAfwijkingAcceptabel,
                    CountNogTeBeoordelen = countNogTeBeoordelen,
                    CountNietVanToepassing = countNietVanToepassing,

                    HasNotAcceptable = hasNotAcceptable,

                    DueDiligenceFinalDecision = decision,
                    DueDiligenceFinalDecisionLabel = decisionLabel,
                    DueDiligenceFinalDecisionDate = assessment.DueDiligenceFinalDecisionDate
                };

                result.Add(item);
            }

            return Ok(result);
        }

        // ------------------------------------------------------------
        // DTO
        // ------------------------------------------------------------
        public class DueDiligenceOverviewItemDto
        {
            public Guid AssessmentId { get; set; }

            public string Organisation { get; set; } = string.Empty;
            public string Supplier { get; set; } = string.Empty;
            public string Solution { get; set; } = string.Empty;

            public string Phase2Status { get; set; } = string.Empty;
            public string Phase3Status { get; set; } = string.Empty;

            public int TotalQuestions { get; set; }
            public int CountVoldoet { get; set; }
            public int CountVoldoetNiet { get; set; }
            public int CountAfwijkingAcceptabel { get; set; }
            public int CountNogTeBeoordelen { get; set; }
            public int CountNietVanToepassing { get; set; }

            /// <summary>
            /// True als er minimaal één vraag is met DueDiligenceOutcome = "Voldoet niet"
            /// én NegativeOutcomeAcceptable = false (dus echt niet-acceptabel).
            /// </summary>
            public bool HasNotAcceptable { get; set; }

            /// <summary>
            /// Interne code: "stop" of "go_to_contract" of null.
            /// </summary>
            public string? DueDiligenceFinalDecision { get; set; }

            /// <summary>
            /// Nederlandstalig label: "Stop (…)", "Doorgaan naar contractering", "Nog geen besluit".
            /// </summary>
            public string? DueDiligenceFinalDecisionLabel { get; set; }

            public DateTime? DueDiligenceFinalDecisionDate { get; set; }
        }
    }
}
