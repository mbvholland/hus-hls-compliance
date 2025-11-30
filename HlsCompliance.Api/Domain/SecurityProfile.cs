namespace HlsCompliance.Api.Domain;

public class SecurityProfileState
{
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// (Optioneel) Versie of variant van het securityprofiel / HLS-profiel.
    /// </summary>
    public string? ProfileVersion { get; set; }

    /// <summary>
    /// Overall niveau van informatiebeveiliging voor deze oplossing.
    /// Placeholder: "Onbekend", "Laag", "Middel", "Hoog".
    /// </summary>
    public string OverallSecurityLevel { get; set; } = "Onbekend";

    /// <summary>
    /// Per blok of domein een score, bv.:
    /// "Organisatorisch" → "Middel", "Technisch" → "Hoog", etc.
    /// Dit gaan we later afstemmen op de HLS Excel-tool.
    /// </summary>
    public Dictionary<string, string> BlockScores { get; set; } = new();

    /// <summary>
    /// Laatste wijzigingsmoment (UTC).
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
