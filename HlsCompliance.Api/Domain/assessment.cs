using System;

namespace HlsCompliance.Api.Domain
{
    public class Assessment
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Naam van de zorgorganisatie waarvoor deze beoordeling wordt uitgevoerd.
        /// </summary>
        public string Organisation { get; set; } = string.Empty;

        /// <summary>
        /// Leverancier (tab 0. Algemeen / B2).
        /// </summary>
        public string Supplier { get; set; } = string.Empty;

        /// <summary>
        /// Applicatie / oplossing (tab 0. Algemeen / B3).
        /// </summary>
        public string Solution { get; set; } = string.Empty;

        /// <summary>
        /// Versie van het HLS-model dat is gebruikt (bijv. "1.3").
        /// Dit is de modelversie, niet de versienummering van deze specifieke beoordeling.
        /// </summary>
        public string HlsVersion { get; set; } = "1.0";

        /// <summary>
        /// Status van fase 1 (vooronderzoek).
        /// Mogelijke waarden: "not_started", "in_progress", "completed".
        /// </summary>
        public string Phase1Status { get; set; } = "not_started";

        /// <summary>
        /// Status van fase 2 (preselectie / selectie).
        /// </summary>
        public string Phase2Status { get; set; } = "not_started";

        /// <summary>
        /// Status van fase 3 (contractering).
        /// </summary>
        public string Phase3Status { get; set; } = "not_started";

        /// <summary>
        /// Status van fase 4a (implementatie).
        /// </summary>
        public string Phase4aStatus { get; set; } = "not_started";

        /// <summary>
        /// Status van fase 4b (beheer / lifecycle).
        /// </summary>
        public string Phase4bStatus { get; set; } = "not_started";

        // --------------------------------------------------------------------
        // Tab 0. Algemeen – meta / contractgegevens en overall risicoklasse.
        // --------------------------------------------------------------------

        /// <summary>
        /// Contractstatus van de oplossing: bijvoorbeeld "Lopend" of "Nieuw".
        /// (tab 0. Algemeen / B4: "Contract lopend of nieuw?")
        /// </summary>
        public string? ContractStatus { get; set; }

        /// <summary>
        /// Datum van het (huidige) contract.
        /// (tab 0. Algemeen / B5: "Datum contract")
        /// </summary>
        public DateTime? ContractDate { get; set; }

        /// <summary>
        /// Datum van een eventuele verlenging.
        /// (tab 0. Algemeen / B6: "Datum verlenging")
        /// </summary>
        public DateTime? RenewalDate { get; set; }

        /// <summary>
        /// Datum waarop de due-diligence (her)beoordeling is uitgevoerd/gepland.
        /// (tab 0. Algemeen / B7: "Datum Due-diligence")
        /// </summary>
        public DateTime? DueDiligenceDate { get; set; }

        /// <summary>
        /// Versie van deze specifieke HLS-beoordeling.
        /// (tab 0. Algemeen / B8: "Versie")
        /// </summary>
        public string? AssessmentVersion { get; set; }

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

        // --------------------------------------------------------------------
        // Tab 1. DPIA_Quickscan – samenvatting.
        // --------------------------------------------------------------------

        /// <summary>
        /// Result from the DPIA quickscan:
        /// true  = DPIA required
        /// false = DPIA not required
        /// null  = not yet determined (quickscan incomplete).
        /// </summary>
        public bool? DpiaRequired { get; set; }

        /// <summary>
        /// Human-readable DPIA status for this assessment.
        /// </summary>
        public string DpiaStatus { get; set; } = "Onbekend";

        // --------------------------------------------------------------------
        // Tab 3. MDR Beslisboom – samenvatting.
        // --------------------------------------------------------------------

        /// <summary>
        /// MDR-klasse voor dit assessment.
        /// </summary>
        public string? MdrClass { get; set; }

        /// <summary>
        /// MDR-status.
        /// </summary>
        public string MdrStatus { get; set; } = "Onbekend";

        // --------------------------------------------------------------------
        // Tab 4. AI Act Beslisboom – samenvatting.
        // --------------------------------------------------------------------

        /// <summary>
        /// AI Act-risiconiveau voor dit assessment.
        /// </summary>
        public string? AiActRiskLevel { get; set; }

        /// <summary>
        /// Status van de AI Act-classificatie.
        /// </summary>
        public string AiActStatus { get; set; } = "Onbekend";

        // --------------------------------------------------------------------
        // Tab 2. Koppeling-Beslisboom – samenvatting.
        // --------------------------------------------------------------------

        /// <summary>
        /// Overall risiconiveau van koppelingen voor dit assessment.
        /// </summary>
        public string? ConnectionsOverallRisk { get; set; }

        /// <summary>
        /// Status van de koppelingenrisico-analyse.
        /// </summary>
        public string ConnectionsRiskStatus { get; set; } = "Onbekend";

        // --------------------------------------------------------------------
        // Tab 5. Securityprofiel leverancier – samenvatting.
        // --------------------------------------------------------------------

        /// <summary>
        /// Securityprofiel-risicoscore voor de leverancier.
        /// </summary>
        public double? SecurityProfileRiskScore { get; set; }

        /// <summary>
        /// Status van het securityprofiel.
        /// </summary>
        public string SecurityProfileStatus { get; set; } = "Onbekend";

        // --------------------------------------------------------------------
        // Eindbeslissing Due Diligence (F3)
        // --------------------------------------------------------------------

        /// <summary>
        /// Eindbeslissing voor Due Diligence:
        /// - "stop"              = proces stoppen, niet naar contractering
        /// - "go_to_contract"    = doorgaan naar contractering
        /// - null / leeg         = nog geen besluit.
        /// </summary>
        public string? DueDiligenceFinalDecision { get; set; }

        /// <summary>
        /// Motivatie bij de eindbeslissing (business / risk motivatie).
        /// </summary>
        public string? DueDiligenceFinalDecisionMotivation { get; set; }

        /// <summary>
        /// Wie het besluit heeft genomen (rol/naam).
        /// </summary>
        public string? DueDiligenceFinalDecisionBy { get; set; }

        /// <summary>
        /// Datum van het eindbesluit.
        /// </summary>
        public DateTime? DueDiligenceFinalDecisionDate { get; set; }

        // --------------------------------------------------------------------
        // Audit / timestamps
        // --------------------------------------------------------------------

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
