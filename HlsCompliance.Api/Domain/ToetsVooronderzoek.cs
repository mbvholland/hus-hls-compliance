namespace HlsCompliance.Api.Domain;

public class ToetsVooronderzoekState
{
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// Is een volledig (uitgebreid) onderzoek / volledige toets vereist?
    /// null = nog niet beoordeeld.
    /// </summary>
    public bool? RequiresFullAssessment { get; set; }

    /// <summary>
    /// Korte toelichting / motivatie waarom wel/niet een volledig onderzoek nodig is.
    /// </summary>
    public string? Motivation { get; set; }

    /// <summary>
    /// Status van de toets, bv. "Onbekend", "Concept", "Definitief".
    /// </summary>
    public string Status { get; set; } = "Onbekend";

    /// <summary>
    /// Laatste wijzigingsmoment (UTC).
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
