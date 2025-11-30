namespace HlsCompliance.Api.Domain;

public class MdrClassificationState
{
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// Equivalent van A2: Is het een medisch hulpmiddel? ("Ja" / "Nee" / null)
    /// </summary>
    public string? A2_IsMedicalDevice { get; set; }

    /// <summary>
    /// Equivalent van B2: Valt het onder een uitzondering / specifieke categorie? ("Ja"/"Nee"/null)
    /// Als B2 = "Ja" wordt het vaak toch "Geen medisch hulpmiddel".
    /// </summary>
    public string? B2_ExceptionOrExclusion { get; set; }

    /// <summary>
    /// Equivalent van C2: Invasief/implantaat e.d. ("Ja"/"Nee"/null)
    /// </summary>
    public string? C2_InvasiveOrImplantable { get; set; }

    /// <summary>
    /// Equivalent van D2: Extra risicofactor (bijv. monitoring, kritieke functies) ("Ja"/"Nee"/null)
    /// </summary>
    public string? D2_AdditionalRiskFactor { get; set; }

    /// <summary>
    /// Equivalent van E2: ernst van de mogelijke schade.
    /// Verwacht: "dodelijk_of_onherstelbaar", "ernstig", "niet_ernstig" of null.
    /// </summary>
    public string? E2_Severity { get; set; }

    /// <summary>
    /// Uitkomst van de MDR-beslisboom:
    /// Mogelijke waardes: "Onbekend", "Geen medisch hulpmiddel", "Klasse I",
    /// "Klasse IIa", "Klasse IIb", "Klasse III".
    /// </summary>
    public string Classification { get; set; } = "Onbekend";

    /// <summary>
    /// Is de set MDR-antwoorden compleet genoeg om een definitief oordeel te geven?
    /// </summary>
    public bool IsComplete { get; set; } = false;

    /// <summary>
    /// Korte toelichting waarom deze classificatie is gekozen.
    /// </summary>
    public string Explanation { get; set; } = string.Empty;
}
