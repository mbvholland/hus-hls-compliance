namespace HlsCompliance.Api.Domain;

/// <summary>
/// Eén vraag uit tab "5. Securityprofiel leverancier" (rij 8 t/m 15).
/// </summary>
public class SecurityProfileQuestion
{
    /// <summary>
    /// Interne code, bijv. "Q1" t/m "Q8".
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Vraagtekst (kolom B in de Excel).
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Tekstuele risicoklasse (kolom E), bijv.:
    /// "Zeer Hoog", "Hoog", "Gemiddeld", "Laag".
    /// </summary>
    public string RiskClass { get; set; } = string.Empty;

    /// <summary>
    /// Gewogen risicowaarde per vraag (zoals F8..F15):
    /// "Zeer Hoog" -> 6
    /// "Hoog"      -> 4
    /// "Gemiddeld" -> 2
    /// "Laag"      -> 1
    /// </summary>
    public int RiskWeight { get; set; }

    /// <summary>
    /// Of het antwoord wordt afgeleid uit de DPIA-quickscan (C8/C12).
    /// </summary>
    public bool IsDerivedFromDpia { get; set; }

    /// <summary>
    /// Indien afgeleid uit DPIA: code van de bronvraag (bijv. "Q1" of "Q7").
    /// </summary>
    public string? DpiaSourceQuestionCode { get; set; }

    /// <summary>
    /// Antwoord: "Ja", "Nee", of null (niet ingevuld/afgeleid).
    /// In Excel staat dit in kolom C.
    /// </summary>
    public string? Answer { get; set; }
}

/// <summary>
/// Resultaat van tab "5. Securityprofiel leverancier"
/// voor één assessment.
/// </summary>
public class SecurityProfileResult
{
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// Alle 8 vragen (rij 8 t/m 15).
    /// </summary>
    public List<SecurityProfileQuestion> Questions { get; set; } = new();

    /// <summary>
    /// Of alle 8 vragen een ingevuld antwoord hebben
    /// (in Excel: C16 = "Ja" als COUNTA(Profielantwoorden)=8).
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Totale risicoscore (F17 = AVERAGE(F8:F15)):
    /// per vraag:
    /// - "Ja" -> RiskWeight (6/4/2/1)
    /// - anders -> 0
    /// en dan het gemiddelde over de 8 vragen.
    /// </summary>
    public double RiskScore { get; set; }

    /// <summary>
    /// Korte toelichting op de risicoscore en compleetheid.
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// Laatste wijzigingstijdstip (UTC).
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }
}
