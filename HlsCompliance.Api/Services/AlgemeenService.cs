using System;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Service voor tab 0. Algemeen.
    /// Leest/schrijft direct op het root Assessment-object en berekent C10/C11/B10 automatisch.
    /// </summary>
    public class AlgemeenService
    {
        private readonly AssessmentService _assessmentService;

        public AlgemeenService(AssessmentService assessmentService)
        {
            _assessmentService = assessmentService ?? throw new ArgumentNullException(nameof(assessmentService));
        }

        /// <summary>
        /// Haal de algemene informatie voor een assessment op.
        /// C10/C11/B10 worden eerst herberekend op basis van de samenvattende velden in Assessment.
        /// </summary>
        public AlgemeenInfoResult Get(Guid assessmentId)
        {
            var assessment = _assessmentService.GetById(assessmentId)
                             ?? throw new InvalidOperationException($"Assessment {assessmentId} not found.");

            RecalculateOverallRisk(assessment);
            assessment.UpdatedAt = DateTime.UtcNow;

            return MapToResult(assessment);
        }

        /// <summary>
        /// Werk de algemene informatie bij (Leverancier, Applicatie, contractdata, versie).
        /// Daarna worden C10/C11/B10 automatisch herberekend.
        /// </summary>
        public AlgemeenInfoResult Update(Guid assessmentId, AlgemeenUpdateRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var assessment = _assessmentService.GetById(assessmentId)
                             ?? throw new InvalidOperationException($"Assessment {assessmentId} not found.");

            // Meta / contractgegevens
            if (request.Leverancier != null)
            {
                assessment.Supplier = request.Leverancier;
            }

            if (request.Applicatie != null)
            {
                assessment.Solution = request.Applicatie;
            }

            if (request.ContractStatus != null)
            {
                assessment.ContractStatus = request.ContractStatus;
            }

            if (request.ContractDate.HasValue)
            {
                assessment.ContractDate = request.ContractDate;
            }

            if (request.RenewalDate.HasValue)
            {
                assessment.RenewalDate = request.RenewalDate;
            }

            if (request.DueDiligenceDate.HasValue)
            {
                assessment.DueDiligenceDate = request.DueDiligenceDate;
            }

            if (request.Versie != null)
            {
                assessment.AssessmentVersion = request.Versie;
            }

            // Herbereken C10/C11/B10
            RecalculateOverallRisk(assessment);

            assessment.UpdatedAt = DateTime.UtcNow;

            return MapToResult(assessment);
        }

        /// <summary>
        /// Implementeert het Excel-model:
        ///
        /// C10 = '1. DPIA_Quickscan'!E18
        ///    + '2. Koppeling-Beslisboom'!D2
        ///    + '3. MDR Beslisboom'!G2
        ///    + '4. AI Act Beslisboom'!H2
        ///    + '5. Securityprofiel leverancier'!F17
        ///
        /// C11 = IF(MOD(C10;5)/5>=0,4; ROUNDUP(C10/5;0); ROUNDDOWN(C10/5;0))
        ///
        /// B10 = IF(C11>3;"Zeer Hoog";
        ///           IF(C11=3;"Hoog";
        ///             IF(C11=2;"Gemiddeld";
        ///               IF(C11=1;"Laag";
        ///                 IF(C11=0;"Geen";"")))))
        /// </summary>
        private static void RecalculateOverallRisk(Assessment assessment)
        {
            if (assessment == null) throw new ArgumentNullException(nameof(assessment));

            // 1. DPIA_Quickscan!E18 -> we benaderen dit via DpiaRequired:
            //    - true  => maximale score (3)
            //    - false => 0
            //    - null  => 0 (onbekend)
            double dpiaScore = assessment.DpiaRequired switch
            {
                true => 3.0,
                false => 0.0,
                _ => 0.0
            };

            // 2. Koppeling-Beslisboom!D2 -> afgeleid uit ConnectionsOverallRisk (Geen/Laag/Middel/Hoog)
            double koppelingScore = MapConnectionsRiskScore(assessment.ConnectionsOverallRisk);

            // 3. MDR Beslisboom!G2 -> afgeleid uit MdrClass (0–4)
            double mdrScore = MapMdrRiskScore(assessment.MdrClass);

            // 4. AI Act Beslisboom!H2 -> afgeleid uit AiActRiskLevel (0–4)
            double aiScore = MapAiActRiskScore(assessment.AiActRiskLevel);

            // 5. Securityprofiel leverancier!F17 -> SecurityProfileRiskScore (double, kan null zijn)
            double securityScore = assessment.SecurityProfileRiskScore ?? 0.0;

            // C10: som van de vijf contributies
            double c10 = dpiaScore + koppelingScore + mdrScore + aiScore + securityScore;
            assessment.OverallRiskScore = c10;

            // C11: IF(MOD(C10;5)/5>=0,4; ROUNDUP(C10/5;0); ROUNDDOWN(C10/5;0))
            double mod = c10 % 5.0;
            double fraction = mod / 5.0;

            int riskClass;
            if (fraction >= 0.4)
            {
                riskClass = (int)Math.Ceiling(c10 / 5.0);
            }
            else
            {
                riskClass = (int)Math.Floor(c10 / 5.0);
            }

            assessment.OverallRiskClass = riskClass;

            // B10: mapping van C11 naar label
            string? label = riskClass switch
            {
                > 3 => "Zeer Hoog",
                3 => "Hoog",
                2 => "Gemiddeld",
                1 => "Laag",
                0 => "Geen",
                _ => null
            };

            assessment.OverallRiskLabel = label;
        }

        private static double MapConnectionsRiskScore(string? overallRisk)
        {
            if (string.IsNullOrWhiteSpace(overallRisk))
                return 0.0;

            return overallRisk.Trim() switch
            {
                "Geen" => 0.0,
                "Laag" => 1.0,
                "Middel" => 2.0,
                "Hoog" => 3.0,
                _ => 0.0
            };
        }

        private static double MapMdrRiskScore(string? mdrClass)
        {
            if (string.IsNullOrWhiteSpace(mdrClass))
                return 0.0;

            return mdrClass.Trim() switch
            {
                "Klasse I" => 1.0,
                "Klasse IIa" => 2.0,
                "Klasse IIb" => 3.0,
                "Klasse III" => 4.0,
                "Geen medisch hulpmiddel" => 0.0,
                _ => 0.0
            };
        }

        private static double MapAiActRiskScore(string? riskLevel)
        {
            if (string.IsNullOrWhiteSpace(riskLevel))
                return 0.0;

            var value = riskLevel.Trim();

            return value switch
            {
                "Laag/minimaal risico" => 1.0,
                "Beperkt risico" => 2.0,
                "Hoog risico" => 3.0,
                "Verboden" => 4.0,
                "Geen AI-systeem (buiten AI Act)" => 0.0,
                "Onbekend" => 0.0,
                _ => 0.0
            };
        }

        private static AlgemeenInfoResult MapToResult(Assessment assessment)
        {
            return new AlgemeenInfoResult
            {
                AssessmentId = assessment.Id,
                Leverancier = assessment.Supplier,
                Applicatie = assessment.Solution,
                ContractStatus = assessment.ContractStatus,
                ContractDate = assessment.ContractDate,
                RenewalDate = assessment.RenewalDate,
                DueDiligenceDate = assessment.DueDiligenceDate,
                Versie = assessment.AssessmentVersion,
                TotalRiskScore = assessment.OverallRiskScore,
                RiskClass = assessment.OverallRiskClass,
                RiskLabel = assessment.OverallRiskLabel,
                LastUpdated = assessment.UpdatedAt ?? assessment.CreatedAt
            };
        }
    }
}
