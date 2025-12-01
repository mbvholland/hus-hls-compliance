namespace HlsCompliance.Api.Domain;

/// <summary>
/// AI Act-profiel voor een assessment, gemodelleerd naar tab
/// "4. AI Act Beslisboom" (A2–H2).
/// </summary>
public class AiActProfileState
{
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// A2: Is_AI_systeem (Ja/Nee/…).
    /// In Excel afgeleid uit DPIA_Quickscan!E7 (vraag over nieuwe technologie / AI).
    /// </summary>
    public string? IsAiSystem { get; set; }

    /// <summary>
    /// B2: Medisch_hulpmiddel_of_onderdeel_MDR klasse hoger dan Klasse I (Ja/Nee/…).
    /// In Excel automatisch afgeleid uit '3. MDR Beslisboom'!F2 (MDR-klasse):
    /// - Onbekend of leeg -> ""
    /// - "Geen medisch hulpmiddel" -> "Nee"
    /// - "Klasse I" -> "Nee"
    /// - "Klasse IIa", "Klasse IIb", "Klasse III" -> "Ja"
    /// In de API wordt dit veld automatisch gezet vanuit MdrService.
    /// </summary>
    public string? IsHighRiskMedicalDevice { get; set; }

    /// <summary>
    /// C2: Beslist_over_toegang_tot_essentiele_zorg (triage, urgentiebepaling) (Ja/Nee/…).
    /// In Excel gekoppeld aan DPIA_Quickscan!E12.
    /// </summary>
    public string? DecidesOnEssentialCareTriage { get; set; }

    /// <summary>
    /// D2: Directe_klinische_beslissing_AI (Ja/Nee/…).
    /// In Excel gekoppeld aan DPIA_Quickscan!E4.
    /// </summary>
    public string? DirectClinicalDecision { get; set; }

    /// <summary>
    /// E2: Interactieve_AI_met_gebruiker (Chatbot met tekst-, beeld-, spraak- of codegeneratie) (Ja/Nee/…).
    /// </summary>
    public string? InteractiveAiWithUser { get; set; }

    /// <summary>
    /// F2: Genereert_content_voor_gebruiker (Ja/Nee/…).
    /// </summary>
    public string? GeneratesContentForUser { get; set; }

    /// <summary>
    /// G2: AI_Act_risicoklasse.
    /// Mogelijke waarden:
    /// - "Onbekend"
    /// - "Geen AI-systeem (buiten AI Act)"
    /// - "Laag/minimaal risico"
    /// - "Beperkt risico"
    /// - "Hoog risico"
    /// </summary>
    public string RiskLevel { get; set; } = "Onbekend";

    /// <summary>
    /// H2: Risicoscore (0–3) op basis van RiskLevel.
    /// </summary>
    public int RiskScore { get; set; } = 0;

    /// <summary>
    /// Indicator of er voldoende informatie is om een risicoklasse te geven.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Korte toelichting / samenvatting van de classificatie.
    /// </summary>
    public string Explanation { get; set; } = string.Empty;
}
