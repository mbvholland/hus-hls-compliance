namespace HlsCompliance.Api.Domain;

public class DpiaQuickscanQuestion
{
    public int Number { get; set; }              // Vraagnummer (1, 2, 3, ...)
    public string Code { get; set; } = string.Empty; // Optioneel: code uit Excel, bv. "DPIA-Q2"
    public string Text { get; set; } = string.Empty; // Korte vraagtekst (kunnen we later vullen uit config)
    public string? Answer { get; set; }          // "Ja", "Nee" of null
}

public class DpiaQuickscanResult
{
    public Guid AssessmentId { get; set; }

    // Alle vragen + antwoorden
    public List<DpiaQuickscanQuestion> Questions { get; set; } = new();

    /// <summary>
    /// Is een DPIA verplicht?
    /// true  = Ja
    /// false = Nee
    /// null  = Onbekend (nog niet alle vragen ingevuld)
    /// </summary>
    public bool? DpiaRequired { get; set; }

    /// <summary>
    /// Korte toelichting waarom deze uitkomst is gekozen.
    /// </summary>
    public string Explanation { get; set; } = string.Empty;
}
