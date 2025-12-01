namespace HlsCompliance.Api.Domain;

public class DpiaQuickscanQuestion
{
    /// <summary>
    /// Vraagcode, bijv. "Q1" t/m "Q14".
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Vraagtekst uit de HLS Excel (tab DPIA_Quickscan).
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Of de vraag verplicht is voor het bepalen van de DPIA-verplichting.
    /// </summary>
    public bool IsMandatory { get; set; }

    /// <summary>
    /// Of dit een risicovraag is (kolom "Risico-indicatie" Middel/Hoog).
    /// </summary>
    public bool IsRiskQuestion { get; set; }

    /// <summary>
    /// Numerieke risicowaarde per vraag (kolom F in Excel):
    /// Laag   = 1
    /// Middel = 2
    /// Hoog   = 3
    /// </summary>
    public int RiskWeight { get; set; }

    /// <summary>
    /// Antwoord: "Ja", "Nee", "Nvt" of null (niet beantwoord).
    /// </summary>
    public string? Answer { get; set; }
}

public class DpiaQuickscanResult
{
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// Alle vragen in de quickscan (Q1 t/m Q14).
    /// </summary>
    public List<DpiaQuickscanQuestion> Questions { get; set; } = new();

    /// <summary>
    /// Of een DPIA verplicht is:
    /// true  = DPIA verplicht
    /// false = DPIA niet verplicht
    /// null  = nog niet te bepalen (bijv. niet alle verplichte vragen zijn beantwoord).
    /// </summary>
    public bool? DpiaRequired { get; set; }

    /// <summary>
    /// Toelichting bij de uitkomst (metadata, geen juridisch bindende tekst).
    /// </summary>
    public string DpiaRequiredReason { get; set; } = string.Empty;

    /// <summary>
    /// Aantal verplichte vragen (IsMandatory) met een ingevuld antwoord.
    /// </summary>
    public int AnsweredMandatoryCount { get; set; }

    /// <summary>
    /// Aantal verplichte vragen zonder antwoord.
    /// </summary>
    public int UnansweredMandatoryCount { get; set; }

    /// <summary>
    /// Aantal risicovragen (IsRiskQuestion = true) die met "Ja" zijn beantwoord.
    /// </summary>
    public int RiskQuestionsAnsweredYes { get; set; }

    /// <summary>
    /// DPIA-risicoscore op basis van Excel V1.2:
    /// =AVERAGE(G2:G15)
    /// waarbij G2..G15 = IF(Answer="Ja"; RiskWeight; 0).
    /// Score loopt van 0 t/m 3.
    /// </summary>
    public double RiskScore { get; set; }

    /// <summary>
    /// Laatste wijzigingstijdstip (UTC).
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }
}
