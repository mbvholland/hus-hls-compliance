namespace HlsCompliance.Api.Domain;

public class Koppeling
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Naam of korte omschrijving van de koppeling (bv. "EPD → LSP").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type koppeling (bijv. "API", "Bestandsuitwisseling", "Message broker", etc.).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Richting van de koppeling (bijv. "inbound", "outbound", "bidirectioneel").
    /// </summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Gevoeligheid van de gegevens (bijv. "Geen", "Laag", "Middel", "Hoog").
    /// </summary>
    public string DataSensitivity { get; set; } = string.Empty;

    /// <summary>
    /// Risiconiveau voor deze koppeling (placeholder: "Onbekend", "Geen", "Laag", "Middel", "Hoog").
    /// </summary>
    public string RiskLevel { get; set; } = "Onbekend";
}

public class KoppelingenResult
{
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// Alle koppelingen voor dit assessment.
    /// </summary>
    public List<Koppeling> Connections { get; set; } = new();

    /// <summary>
    /// Aggregatie van risico over alle koppelingen (bijv. "Onbekend", "Geen", "Laag", "Middel", "Hoog").
    /// </summary>
    public string OverallRiskLevel { get; set; } = "Onbekend";

    /// <summary>
    /// Korte toelichting / samenvatting.
    /// </summary>
    public string Explanation { get; set; } = string.Empty;
}
