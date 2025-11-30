using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services;

public class MdrService
{
    private readonly Dictionary<Guid, MdrClassificationState> _storage = new();

    public MdrClassificationState GetOrCreateForAssessment(Guid assessmentId)
    {
        if (_storage.TryGetValue(assessmentId, out var existing))
        {
            return existing;
        }

        var state = new MdrClassificationState
        {
            AssessmentId = assessmentId,
            Classification = "Onbekend",
            IsComplete = false,
            Explanation = "Nog geen MDR-gegevens ingevuld."
        };

        _storage[assessmentId] = state;
        return state;
    }

    public MdrClassificationState UpdateAnswers(
        Guid assessmentId,
        string? a2,
        string? b2,
        string? c2,
        string? d2,
        string? e2Severity)
    {
        var state = GetOrCreateForAssessment(assessmentId);

        state.A2_IsMedicalDevice = Normalize(a2);
        state.B2_ExceptionOrExclusion = Normalize(b2);
        state.C2_InvasiveOrImplantable = Normalize(c2);
        state.D2_AdditionalRiskFactor = Normalize(d2);
        state.E2_Severity = Normalize(e2Severity);

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
    /// MDR-beslislogica (vereenvoudigd, maar gebaseerd op eerder gebruikte regels).
    /// </summary>
    private void Recalculate(MdrClassificationState state)
    {
        var a2 = state.A2_IsMedicalDevice?.ToLowerInvariant();
        var b2 = state.B2_ExceptionOrExclusion?.ToLowerInvariant();
        var c2 = state.C2_InvasiveOrImplantable?.ToLowerInvariant();
        var d2 = state.D2_AdditionalRiskFactor?.ToLowerInvariant();
        var severity = state.E2_Severity?.ToLowerInvariant();

        // 1. Als helemaal niets is ingevuld: Onbekend
        var anyAnswer =
            a2 != null || b2 != null || c2 != null || d2 != null || severity != null;

        if (!anyAnswer)
        {
            state.Classification = "Onbekend";
            state.IsComplete = false;
            state.Explanation = "Er zijn nog geen MDR-antwoorden ingevuld.";
            return;
        }

        // 2. Als een deel is ingevuld maar niet alles: "onvolledig"
        // (hier eisen we dat A2, B2, C2, D2 en E2 allemaal iets hebben)
        var missing =
            a2 == null || b2 == null || c2 == null || d2 == null || severity == null;

        if (missing)
        {
            state.Classification = "Onbekend";
            state.IsComplete = false;
            state.Explanation = "Niet alle MDR-vragen zijn ingevuld. Vul A2 t/m E2 volledig in.";
            return;
        }

        // Vanaf hier hebben we een volledige set antwoorden
        state.IsComplete = true;

        // 3. Logica (gebaseerd op jouw Excel-formule en eerdere uitleg):

        // 3.1. Als A2 != "ja" -> geen medisch hulpmiddel
        if (a2 != "ja")
        {
            state.Classification = "Geen medisch hulpmiddel";
            state.Explanation = "A2 is niet 'Ja': deze oplossing wordt niet als medisch hulpmiddel aangemerkt.";
            return;
        }

        // 3.2. Als B2 = "ja" -> geen medisch hulpmiddel (valt onder uitzondering)
        if (b2 == "ja")
        {
            state.Classification = "Geen medisch hulpmiddel";
            state.Explanation = "B2 is 'Ja': de oplossing valt onder een uitzondering en is geen medisch hulpmiddel.";
            return;
        }

        // 3.3. Als C2 != "ja" -> Klasse I
        if (c2 != "ja")
        {
            state.Classification = "Klasse I";
            state.Explanation = "C2 is niet 'Ja': op basis hiervan wordt Klasse I toegekend.";
            return;
        }

        // 3.4. Als D2 != "ja" -> Klasse I
        if (d2 != "ja")
        {
            state.Classification = "Klasse I";
            state.Explanation = "D2 is niet 'Ja': op basis hiervan wordt Klasse I toegekend.";
            return;
        }

        // 3.5. Ernst (E2) bepaalt hogere klasse
        switch (severity)
        {
            case "dodelijk_of_onherstelbaar":
                state.Classification = "Klasse III";
                state.Explanation = "E2 = dodelijk_of_onherstelbaar: hoogste risicoklasse (III).";
                break;

            case "ernstig":
                state.Classification = "Klasse IIb";
                state.Explanation = "E2 = ernstig: Klasse IIb.";
                break;

            case "niet_ernstig":
                state.Classification = "Klasse IIa";
                state.Explanation = "E2 = niet_ernstig: Klasse IIa.";
                break;

            default:
                state.Classification = "Klasse I";
                state.Explanation = "E2 niet herkend, default naar Klasse I.";
                break;
        }
    }
}
