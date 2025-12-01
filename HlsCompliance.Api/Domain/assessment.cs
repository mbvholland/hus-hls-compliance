namespace HlsCompliance.Api.Domain;

public class Assessment
{
    public Guid Id { get; set; }

    public string Organisation { get; set; } = string.Empty;

    public string Supplier { get; set; } = string.Empty;

    public string Solution { get; set; } = string.Empty;

    public string HlsVersion { get; set; } = "1.0";

    public string Phase1Status { get; set; } = "not_started";
    public string Phase2Status { get; set; } = "not_started";
    public string Phase3Status { get; set; } = "not_started";
    public string Phase4aStatus { get; set; } = "not_started";
    public string Phase4bStatus { get; set; } = "not_started";

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

    /// <summary>
    /// MDR-klasse voor dit assessment, bijv. "Onbekend",
    /// "Geen medisch hulpmiddel", "Klasse I", "Klasse IIa", "Klasse IIb", "Klasse III".
    /// </summary>
    public string? MdrClass { get; set; }

    /// <summary>
    /// MDR-status, bijv.:
    /// - "Onbekend"
    /// - "MDR geclassificeerd"
    /// </summary>
    public string MdrStatus { get; set; } = "Onbekend";

    /// <summary>
    /// AI Act-risiconiveau voor dit assessment, bijv.:
    /// "Onbekend", "Geen AI-systeem (buiten AI Act)",
    /// "Laag/minimaal risico", "Beperkt risico", "Hoog risico".
    /// </summary>
    public string? AiActRiskLevel { get; set; }

    /// <summary>
    /// Status van de AI Act-classificatie, bijv.:
    /// - "Onbekend"
    /// - "AI Act geclassificeerd"
    /// </summary>
    public string AiActStatus { get; set; } = "Onbekend";

    /// <summary>
    /// Overall risiconiveau van koppelingen voor dit assessment,
    /// zoals berekend in KoppelingenService:
    /// "Onbekend", "Geen", "Laag", "Middel", "Hoog".
    /// </summary>
    public string? ConnectionsOverallRisk { get; set; }

    /// <summary>
    /// Status van de koppelingenrisico-analyse, bijv.:
    /// - "Onbekend"
    /// - "Geen koppelingen geregistreerd"
    /// - "Geen koppelingen volgens DPIA"
    /// - "Koppelingen beoordeeld"
    /// </summary>
    public string ConnectionsRiskStatus { get; set; } = "Onbekend";

    /// <summary>
    /// Securityprofiel-risicoscore voor de leverancier,
    /// in lijn met tab "5. Securityprofiel leverancier" F17 (gemiddelde van F8:F15).
    /// Waarde loopt grofweg tussen 0 en 6.
    /// </summary>
    public double? SecurityProfileRiskScore { get; set; }

    /// <summary>
    /// Status van het securityprofiel, bijv.:
    /// - "Onbekend"
    /// - "Onvolledig"
    /// - "Securityprofiel beoordeeld"
    /// </summary>
    public string SecurityProfileStatus { get; set; } = "Onbekend";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
