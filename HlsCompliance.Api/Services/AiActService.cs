using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services;

public class AiActService
{
    private readonly Dictionary<Guid, AiActProfileState> _storage = new();

    public AiActProfileState GetOrCreateForAssessment(Guid assessmentId)
    {
        if (_storage.TryGetValue(assessmentId, out var existing))
        {
            return existing;
        }

        var state = new AiActProfileState
        {
            AssessmentId = assessmentId,
            RiskLevel = "Onbekend",
            IsComplete = false,
            Explanation = "Nog geen AI Act-gegevens ingevuld."
        };

        _storage[assessmentId] = state;
        return state;
    }

    public AiActProfileState UpdateAnswers(
        Guid assessmentId,
        string? a2IsAiSystem,
        string? b2IsGeneralPurpose,
        string? c2HighRiskUseCase,
        string? d2ProhibitedPractice,
        string? e2ImpactLevel)
    {
        var state = GetOrCreateForAssessment(assessmentId);

        state.A2_IsAiSystem = Normalize(a2IsAiSystem);
        state.B2_IsGeneralPurpose = Normalize(b2IsGeneralPurpose);
        state.C2_HighRiskUseCase = Normalize(c2HighRiskUseCase);
        state.D2_ProhibitedPractice = Normalize(d2ProhibitedPractice);
        state.E2_ImpactLevel = Normalize(e2ImpactLevel);

        Recalculate(state);

        return state;
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    /// <summary>
    /// Voorlopige AI Act-beslislogica.
    /// Later kun je deze 1-op-1 laten aansluiten op je Excel/HLS-regels.
    /// </summary>
    private void Recalculate(AiActProfileState state)
    {
        var a2 = state.A2_IsAiSystem?.ToLowerInvariant();
        var b2 = state.B2_IsGeneralPurpose?.ToLowerInvariant();
        var c2 = state.C2_HighRiskUseCase?.ToLowerInvariant();
        var d2 = state.D2_ProhibitedPractice?.ToLowerInvariant();
        var e2 = state.E2_ImpactLevel?.ToLowerInvariant();

        // 1. Helemaal niets ingevuld
        var anyAnswer = a2 != null || b2 != null || c2 != null || d2 != null || e2 != null;
        if (!anyAnswer)
        {
            state.RiskLevel = "Onbekend";
            state.IsComplete = false;
            state.Explanation = "Er zijn nog geen AI Act-antwoorden ingevuld.";
            return;
        }

        // 2. Onvolledig ingevuld (we verwachten dat A2 t/m E2 allemaal een waarde hebben)
        var missing = a2 == null || b2 == null || c2 == null || d2 == null || e2 == null;
        if (missing)
        {
            state.RiskLevel = "Onbekend";
            state.IsComplete = false;
            state.Explanation = "Niet alle AI Act-vragen zijn ingevuld. Vul A2 t/m E2 volledig in.";
            return;
        }

        // Vanaf hier: complete set antwoorden
        state.IsComplete = true;

        // 3. Beslislogica (voorlopig, maar bruikbaar):

        // 3.1. Geen AI-systeem -> buiten scope AI Act
        if (a2 != "ja")
        {
            state.RiskLevel = "Geen AI-systeem (buiten AI Act)";
            state.Explanation = "A2 is niet 'Ja': dit wordt niet als AI-systeem beschouwd voor de AI Act.";
            return;
        }

        // 3.2. Verboden praktijken -> "Verboden"
        if (d2 == "ja")
        {
            state.RiskLevel = "Verboden";
            state.Explanation = "D2 = 'Ja': er is sprake van een verboden praktijk onder de AI Act.";
            return;
        }

        // 3.3. Hoog-risico use case -> "Hoog risico"
        if (c2 == "ja")
        {
            state.RiskLevel = "Hoog risico";
            state.Explanation = "C2 = 'Ja': deze toepassing valt onder een hoog-risico use case.";
            return;
        }

        // 3.4. Impactniveau bepaalt beperkt / laag
        switch (e2)
        {
            case "hoog":
                state.RiskLevel = "Hoog risico";
                state.Explanation = "Impactniveau is 'hoog'; voorlopig ingedeeld als hoog risico.";
                break;

            case "beperkt":
                state.RiskLevel = "Beperkt risico";
                state.Explanation = "Impactniveau is 'beperkt'; voorlopig ingedeeld als beperkt risico.";
                break;

            case "laag":
                state.RiskLevel = "Laag/minimaal risico";
                state.Explanation = "Impactniveau is 'laag'; voorlopig ingedeeld als laag/minimaal risico.";
                break;

            default:
                state.RiskLevel = "Laag/minimaal risico";
                state.Explanation = "Impactniveau niet herkend; voorlopig ingedeeld als laag/minimaal risico.";
                break;
        }
    }
}
