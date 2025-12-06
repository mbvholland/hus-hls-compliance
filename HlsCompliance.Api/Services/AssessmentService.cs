using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    public class AssessmentService
    {
        // Interne opslag van alle assessments. Deze wordt geladen/geschreven vanuit/naar JSON.
        private readonly List<Assessment> _assessments = new();

        // Bestandsnaam voor persistente opslag (relatief t.o.v. HlsCompliance.Api)
        private const string AssessmentsFileName = "Data/assessments.json";

        // Lock-object voor thread safety bij lezen/schrijven
        private readonly object _syncRoot = new();

        public AssessmentService()
        {
            LoadFromDisk();
        }

        /// <summary>
        /// Haal alle assessments op.
        /// </summary>
        public IEnumerable<Assessment> GetAll()
        {
            lock (_syncRoot)
            {
                // We geven een kopie terug zodat aanroepers de interne lijst niet per ongeluk muteren.
                return _assessments
                    .Select(a => a)
                    .ToList();
            }
        }

        /// <summary>
        /// Haal één assessment op basis van Id.
        /// </summary>
        public Assessment? GetById(Guid id)
        {
            lock (_syncRoot)
            {
                return _assessments.FirstOrDefault(a => a.Id == id);
            }
        }

        /// <summary>
        /// Maak een nieuw assessment aan en sla het persistent op.
        /// Eerste versie krijgt standaard AssessmentVersion = "V1"
        /// en DueDiligenceDate = nu (UTC).
        /// </summary>
        public Assessment Create(string organisation, string supplier, string solution, string hlsVersion)
        {
            var now = DateTime.UtcNow;

            var assessment = new Assessment
            {
                Id = Guid.NewGuid(),
                Organisation = organisation,
                Supplier = supplier,
                Solution = solution,
                HlsVersion = hlsVersion,

                Phase1Status = "not_started",
                Phase2Status = "not_started",
                Phase3Status = "not_started",
                Phase4aStatus = "not_started",
                Phase4bStatus = "not_started",

                // Contract/risico velden blijven null totdat fase 1/2 zijn ingevuld.
                ContractStatus = null,
                ContractDate = null,
                RenewalDate = null,

                // Eerste DD-versie
                DueDiligenceDate = now,
                AssessmentVersion = "V1",

                // Overkoepelende risicovelden: worden berekend via RecalculateOverallRisk
                OverallRiskScore = null,
                OverallRiskClass = null,
                OverallRiskLabel = null,

                // Vooronderzoek/risico-inputs
                DpiaRequired = null,
                DpiaRiskScore = null,
                DpiaStatus = "Onbekend",
                MdrClass = null,
                MdrStatus = "Onbekend",
                AiActRiskLevel = null,
                AiActStatus = "Onbekend",
                ConnectionsOverallRisk = null,
                ConnectionsRiskStatus = "Onbekend",
                SecurityProfileRiskScore = null,
                SecurityProfileStatus = "Onbekend",

                DueDiligenceFinalDecision = "unknown",
                DueDiligenceFinalDecisionMotivation = null,
                DueDiligenceFinalDecisionBy = null,
                DueDiligenceFinalDecisionDate = null,

                CreatedAt = now,
                UpdatedAt = null
            };

            // Bepaal (indien mogelijk) de overkoepelende risicoscore op basis van de beschikbare inputs.
            RecalculateOverallRisk(assessment);

            lock (_syncRoot)
            {
                _assessments.Add(assessment);
                SaveToDisk();
            }

            return assessment;
        }

        /// <summary>
        /// Maak een nieuwe assessmentversie op basis van een bestaand assessment.
        ///
        /// Voorbeeld: V3 -> V4. Alle relevante velden (organisatie, leverancier,
        /// oplossing, contract, risico, AI/MDR/NIS2, etc.) worden overgenomen, maar:
        /// - de Id is nieuw,
        /// - AssessmentVersion wordt verhoogd (of expliciet gezet),
        /// - DueDiligenceDate wordt vernieuwd,
        /// - fase-statussen gaan terug naar "not_started",
        /// - DueDiligenceFinalDecision* worden leeggemaakt (nieuwe G3-beslissing nodig).
        /// </summary>
        /// <param name="sourceAssessmentId">De Id van de bron-assessment (bijv. V3).</param>
        /// <param name="newDueDiligenceDate">
        /// Optionele nieuwe datum voor de due diligence; als null wordt DateTime.UtcNow gebruikt.
        /// </param>
        /// <param name="explicitNewVersion">
        /// Optioneel expliciet versienummer (bijv. "V4"). Als null wordt op basis van de bronversie
        /// een nieuwe versie bepaald (bijv. "V3" -> "V4").
        /// </param>
        /// <returns>De nieuw aangemaakte assessment, of null als de bron niet bestaat.</returns>
        public Assessment? CreateNewVersionFromExisting(
            Guid sourceAssessmentId,
            DateTime? newDueDiligenceDate = null,
            string? explicitNewVersion = null)
        {
            lock (_syncRoot)
            {
                var source = _assessments.FirstOrDefault(a => a.Id == sourceAssessmentId);
                if (source == null)
                {
                    return null;
                }

                // Bepaal nieuwe assessmentversie
                var newVersion = !string.IsNullOrWhiteSpace(explicitNewVersion)
                    ? explicitNewVersion!
                    : GetNextAssessmentVersion(source.AssessmentVersion);

                var now = DateTime.UtcNow;
                var ddDate = newDueDiligenceDate ?? now;

                var newAssessment = new Assessment
                {
                    Id = Guid.NewGuid(),

                    // Hoofdgegevens
                    Organisation = source.Organisation,
                    Supplier = source.Supplier,
                    Solution = source.Solution,
                    HlsVersion = source.HlsVersion,

                    // Fase-statussen opnieuw starten
                    Phase1Status = "not_started",
                    Phase2Status = "not_started",
                    Phase3Status = "not_started",
                    Phase4aStatus = "not_started",
                    Phase4bStatus = "not_started",

                    // Contract en datums overnemen
                    ContractStatus = source.ContractStatus,
                    ContractDate = source.ContractDate,
                    RenewalDate = source.RenewalDate,
                    DueDiligenceDate = ddDate,

                    // Assessmentversie
                    AssessmentVersion = newVersion,

                    // Overal risico wordt opnieuw berekend op basis van de overgenomen inputvelden.
                    OverallRiskScore = null,
                    OverallRiskClass = null,
                    OverallRiskLabel = null,

                    // Vooronderzoek/risico-input overnemen (kan voor Vn+1 worden bijgesteld)
                    DpiaRequired = source.DpiaRequired,
                    DpiaRiskScore = source.DpiaRiskScore,
                    DpiaStatus = source.DpiaStatus,
                    MdrClass = source.MdrClass,
                    MdrStatus = source.MdrStatus,
                    AiActRiskLevel = source.AiActRiskLevel,
                    AiActStatus = source.AiActStatus,
                    ConnectionsOverallRisk = source.ConnectionsOverallRisk,
                    ConnectionsRiskStatus = source.ConnectionsRiskStatus,
                    SecurityProfileRiskScore = source.SecurityProfileRiskScore,
                    SecurityProfileStatus = source.SecurityProfileStatus,

                    // Nieuwe versie: eindbeslissing moet opnieuw genomen worden
                    DueDiligenceFinalDecision = "unknown",
                    DueDiligenceFinalDecisionMotivation = null,
                    DueDiligenceFinalDecisionBy = null,
                    DueDiligenceFinalDecisionDate = null,

                    CreatedAt = now,
                    UpdatedAt = null
                };

                // Overkoepelende risicoscore opnieuw bepalen op basis van de inputvelden
                RecalculateOverallRisk(newAssessment);

                _assessments.Add(newAssessment);
                SaveToDisk();

                return newAssessment;
            }
        }

        /// <summary>
        /// Bepaalt op basis van een bestaand versienummer (bijv. "V3") de volgende versie ("V4").
        /// Als de bron null/empty of niet parsebaar is, wordt "V2" teruggegeven.
        /// </summary>
        private static string GetNextAssessmentVersion(string? currentVersion)
        {
            if (string.IsNullOrWhiteSpace(currentVersion))
            {
                // Geen bestaande versie: we gaan uit van een tweede versie.
                return "V2";
            }

            var trimmed = currentVersion.Trim();

            // Strip eventueel een leading "V" of "v"
            if (trimmed.StartsWith("V", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(1);
            }

            if (int.TryParse(trimmed, out var n) && n > 0)
            {
                return $"V{n + 1}";
            }

            // Fallback: niet parsebaar -> gebruik V2 als "volgende".
            return "V2";
        }

        /// <summary>
        /// Update de status van een fase (fase 1–4a/4b) en schrijf de wijziging weg naar JSON.
        /// </summary>
        public bool UpdatePhaseStatus(Guid id, string phase, string status)
        {
            lock (_syncRoot)
            {
                var assessment = _assessments.FirstOrDefault(a => a.Id == id);
                if (assessment == null)
                {
                    return false;
                }

                switch (phase.ToLowerInvariant())
                {
                    case "phase1":
                        assessment.Phase1Status = status;
                        break;
                    case "phase2":
                        assessment.Phase2Status = status;
                        break;
                    case "phase3":
                        assessment.Phase3Status = status;
                        break;
                    case "phase4a":
                        assessment.Phase4aStatus = status;
                        break;
                    case "phase4b":
                        assessment.Phase4bStatus = status;
                        break;
                    default:
                        // Onbekende fase
                        return false;
                }

                assessment.UpdatedAt = DateTime.UtcNow;

                SaveToDisk();
                return true;
            }
        }

        /// <summary>
        /// Sla de eindbeslissing van de Due Diligence (G3) op:
        /// - decision: "stop" of "go_to_contract"
        /// - motivation: business/risk motivatie
        /// - decisionBy: rol/naam van de beslisser
        /// - decisionDate: datum van het besluit
        /// </summary>
        public bool UpdateDueDiligenceFinalDecision(
            Guid id,
            string decision,
            string? motivation,
            string? decisionBy,
            DateTime? decisionDate)
        {
            lock (_syncRoot)
            {
                var assessment = _assessments.FirstOrDefault(a => a.Id == id);
                if (assessment == null)
                {
                    return false;
                }

                assessment.DueDiligenceFinalDecision = decision;
                assessment.DueDiligenceFinalDecisionMotivation = motivation;
                assessment.DueDiligenceFinalDecisionBy = decisionBy;
                assessment.DueDiligenceFinalDecisionDate = decisionDate;

                assessment.UpdatedAt = DateTime.UtcNow;

                SaveToDisk();
                return true;
            }
        }

        // --------------------------------------------------------------------
        // Overkoepelende risico-aggregatie (tab 0. Algemeen – C10, C11, B10)
        // --------------------------------------------------------------------

        /// <summary>
        /// Bereken de overkoepelende risicoscore en -klasse op basis van:
        /// - DPIA-risicoscore (1. DPIA_Quickscan!E18),
        /// - Koppeling-risicoscore (2. Koppeling-Beslisboom!D2),
        /// - MDR-risicoscore (3. MDR Beslisboom!G2),
        /// - AI Act-risicoscore (4. AI Act Beslisboom!H2),
        /// - Securityprofiel-risicoscore (5. Securityprofiel leverancier!F17).
        ///
        /// C10 = som van bovenstaande componenten.
        /// C11 = IF(MOD(C10,5)/5>=0.4,ROUNDUP(C10/5),ROUNDDOWN(C10/5)).
        /// B10 = label op basis van C11 (Geen/Laag/Gemiddeld/Hoog/Zeer Hoog).
        ///
        /// Als er nog helemaal geen input is (alles null/empty),
        /// blijven OverallRiskScore/Class/Label op null staan.
        /// </summary>
        private static void RecalculateOverallRisk(Assessment assessment)
        {
            // Check of er überhaupt input is; zo niet, dan geen aggregatie tonen.
            var hasAnyInput =
                assessment.DpiaRiskScore.HasValue ||
                !string.IsNullOrWhiteSpace(assessment.ConnectionsOverallRisk) ||
                !string.IsNullOrWhiteSpace(assessment.MdrClass) ||
                !string.IsNullOrWhiteSpace(assessment.AiActRiskLevel) ||
                assessment.SecurityProfileRiskScore.HasValue;

            if (!hasAnyInput)
            {
                assessment.OverallRiskScore = null;
                assessment.OverallRiskClass = null;
                assessment.OverallRiskLabel = null;
                return;
            }

            // Componenten volgens Excel-tabellen
            var dpia = assessment.DpiaRiskScore ?? 0.0;

            var koppeling = MapConnectionsOverallRiskToScore(assessment.ConnectionsOverallRisk);
            var mdr = MapMdrClassToScore(assessment.MdrClass);
            var ai = MapAiActRiskLevelToScore(assessment.AiActRiskLevel);
            var security = assessment.SecurityProfileRiskScore ?? 0.0;

            var total = dpia + koppeling + mdr + ai + security;

            assessment.OverallRiskScore = total;

            // C11: IF(MOD(C10,5)/5>=0.4, ROUNDUP(C10/5), ROUNDDOWN(C10/5))
            var mod5 = total % 5.0;
            var fraction = mod5 / 5.0;

            int riskClass;
            if (fraction >= 0.4)
            {
                riskClass = (int)Math.Ceiling(total / 5.0);
            }
            else
            {
                riskClass = (int)Math.Floor(total / 5.0);
            }

            if (riskClass < 0)
            {
                riskClass = 0;
            }

            assessment.OverallRiskClass = riskClass;

            // B10: label op basis van C11
            string label;
            if (riskClass > 3)
            {
                label = "Zeer Hoog";
            }
            else if (riskClass == 3)
            {
                label = "Hoog";
            }
            else if (riskClass == 2)
            {
                label = "Gemiddeld";
            }
            else if (riskClass == 1)
            {
                label = "Laag";
            }
            else // 0
            {
                label = "Geen";
            }

            assessment.OverallRiskLabel = label;
        }

        private static double MapConnectionsOverallRiskToScore(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0.0;
            }

            var v = value.Trim();

            if (v.Equals("Geen", StringComparison.OrdinalIgnoreCase))
                return 0.0;

            if (v.Equals("Laag", StringComparison.OrdinalIgnoreCase))
                return 1.0;

            if (v.Equals("Middel", StringComparison.OrdinalIgnoreCase) ||
                v.Equals("Midden", StringComparison.OrdinalIgnoreCase))
                return 2.0;

            if (v.Equals("Hoog", StringComparison.OrdinalIgnoreCase))
                return 3.0;

            if (v.Equals("Onbekend", StringComparison.OrdinalIgnoreCase))
                return 0.0;

            // Onbekende waarde: conservatief als 0, conform Excel D2 ("", "Onbekend" -> 0).
            return 0.0;
        }

        private static double MapMdrClassToScore(string? mdrClass)
        {
            if (string.IsNullOrWhiteSpace(mdrClass))
            {
                return 0.0;
            }

            var v = mdrClass.Trim();

            if (v.Equals("Geen medisch hulpmiddel", StringComparison.OrdinalIgnoreCase))
                return 0.0;

            if (v.Equals("Klasse I", StringComparison.OrdinalIgnoreCase))
                return 1.0;

            if (v.Equals("Klasse IIa", StringComparison.OrdinalIgnoreCase))
                return 2.0;

            if (v.Equals("Klasse IIb", StringComparison.OrdinalIgnoreCase))
                return 3.0;

            if (v.Equals("Klasse III", StringComparison.OrdinalIgnoreCase))
                return 4.0;

            if (v.Equals("Onbekend", StringComparison.OrdinalIgnoreCase))
                return 0.0;

            return 0.0;
        }

        private static double MapAiActRiskLevelToScore(string? aiLevel)
        {
            if (string.IsNullOrWhiteSpace(aiLevel))
            {
                return 0.0;
            }

            var v = aiLevel.Trim();

            if (v.Equals("Geen AI-systeem (buiten AI Act)", StringComparison.OrdinalIgnoreCase))
                return 0.0;

            if (v.Equals("Laag/minimaal risico", StringComparison.OrdinalIgnoreCase))
                return 1.0;

            if (v.Equals("Beperkt risico", StringComparison.OrdinalIgnoreCase))
                return 2.0;

            if (v.Equals("Hoog risico", StringComparison.OrdinalIgnoreCase))
                return 3.0;

            // Onbekend / overige: 0 (zoals H2 bij onbekende waarde)
            return 0.0;
        }

        // -------------------------
        // Persistente opslag (JSON)
        // -------------------------

        private void LoadFromDisk()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, AssessmentsFileName);

                if (!File.Exists(filePath))
                {
                    // Nog geen bestand -> lege lijst.
                    return;
                }

                var json = File.ReadAllText(filePath);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var items = JsonSerializer.Deserialize<List<Assessment>>(json, options);
                if (items == null)
                {
                    return;
                }

                lock (_syncRoot)
                {
                    _assessments.Clear();
                    _assessments.AddRange(items);
                }
            }
            catch
            {
                // Fouten bij lezen/parse negeren; we starten dan met een lege lijst.
                // Later kun je hier logging toevoegen.
            }
        }

        private void SaveToDisk()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, AssessmentsFileName);

                List<Assessment> snapshot;
                lock (_syncRoot)
                {
                    snapshot = _assessments
                        .Select(a => a)
                        .ToList();
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(snapshot, options);

                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Fouten bij schrijven negeren; de assessments blijven in memory beschikbaar.
                // Later kun je hier logging toevoegen.
            }
        }
    }
}
