namespace HlsCompliance.Api.Domain;

public class AiActProfileState
{
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// Equivalent van A2: is er sprake van een AI-systeem? ("Ja" / "Nee" / null)
    /// </summary>
    public string? A2_IsAiSystem { get; set; }

    /// <summary>
    /// Equivalent van B2: betreft het een general purpose / foundation model? ("Ja" / "Nee" / null)
    /// (voor nu alleen informatief)
    /// </summary>
    public string? B2_IsGeneralPurpose { get; set; }

    /// <summary>
    /// Equivalent van C2: valt het gebruik onder een hoog-risico use case? ("Ja" / "Nee" / null)
    /// </summary>
    public string? C2_HighRiskUseCase { get; set; }

    /// <summary>
    /// Equivalent van D2: bevat het verboden praktijken? ("Ja" / "Nee" / null)
    /// </summary>
    public string? D2_ProhibitedPractice { get; set; }

    /// <summary>
    /// Equivalent van E2: indicatie van impact op betrokkenen ("laag", "beperkt", "hoog" / null)
    /// </summary>
    public string? E2_ImpactLevel { get; set; }

    /// <summary>
    /// Uitkomst AI Act-beslisboom:
    /// Bijvoorbeeld: "Onbekend", "Geen AI-systeem (buiten AI Act)",
    /// "Laag/minimaal risico", "Beperkt risico", "Hoog risico", "Verboden".
    /// </summary>
    public string RiskLevel { get; set; } = "Onbekend";

    /// <summary>
    /// Is de set AI Act-antwoorden compleet genoeg voor een beoordeling?
    /// </summary>
    public bool IsComplete { get; set; } = false;

    /// <summary>
    /// Korte toelichting waarom dit risiconiveau is gekozen.
    /// </summary>
    public string Explanation { get; set; } = string.Empty;
}
