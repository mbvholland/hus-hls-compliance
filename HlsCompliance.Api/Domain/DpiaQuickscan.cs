namespace HlsCompliance.Api.Domain;

public class DpiaQuickscanQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Stabiele code van de vraag, bv. "Q1", "Q2", zodat we antwoorden
    /// via deze code kunnen doorgeven vanuit de client.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// De vraagtekst zoals die ook in de HLS Excel-tool staat (later te verfijnen).
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Antwoord, bv. "Ja", "Nee", "Nvt", of leeg/null als nog niet beantwoord.
    /// </summary>
    public string? Answer { get; set; }

    /// <summary>
    /// True als deze vraag een DPIA-risicofactor vertegenwoordigt:
    /// bij antwoord "Ja" telt hij mee voor DPIA verplicht.
    /// </summary>
    public bool IsRiskQuestion { get; set; }

    /// <summary>
    /// Is deze vraag verplicht om te beantwoorden voor een valide quickscan?
    /// </summary>
    public bool IsMandatory { get; set; } = true;
}

public class DpiaQuickscanResult
{
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// Alle vragen voor deze DPIA-quickscan.
    /// </summary>
    public List<DpiaQuickscanQuestion> Questions { get; set; } = new();

    /// <summary>
    /// Of een DPIA verplicht is op basis van de huidige beantwoording:
    /// true  = DPIA vereist
    /// false = DPIA niet vereist
    /// null  = nog niet te bepalen (bijv. niet alles beantwoord).
    /// </summary>
    public bool? DpiaRequired { get; set; }

    /// <summary>
    /// Toelichting waarom (nog) wel/niet DPIA vereist is.
    /// </summary>
    public string? DpiaRequiredReason { get; set; }

    /// <summary>
    /// Aantal verplichte vragen dat is beantwoord.
    /// </summary>
    public int AnsweredMandatoryCount { get; set; }

    /// <summary>
    /// Aantal verplichte vragen dat nog niet is beantwoord.
    /// </summary>
    public int UnansweredMandatoryCount { get; set; }

    /// <summary>
    /// Aantal risicovragen dat met "Ja" is beantwoord.
    /// </summary>
    public int RiskQuestionsAnsweredYes { get; set; }

    /// <summary>
    /// Laatste wijzigingsmoment (UTC).
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
