using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services;

/// <summary>
/// Service voor de AI Act-beslisboom (tab "4. AI Act Beslisboom").
/// </summary>
public class AiActService
{
    private readonly Dictionary<Guid, AiActProfileState> _storage = new();
    private readonly DpiaQuickscanService _dpiaQuickscanService;
    private readonly MdrService _mdrService;

    public AiActService(
        DpiaQuickscanService dpiaQuickscanService,
        MdrService mdrService)
    {
        _dpiaQuickscanService = dpiaQuickscanService;
        _mdrService = mdrService;
    }

    /// <summary>
    /// Haal of maak het AI Act-profiel voor een assessment.
    /// Prefill A2/C2/D2 vanuit DPIA en B2 vanuit MDR.
    /// </summary>
    public AiActProfileState GetOrCreateForAssessment(Guid assessmentId)
    {
        if (!_storage.TryGetValue(assessmentId, out var state))
        {
            state = new AiActProfileState
            {
                AssessmentId = assessmentId
            };

            PrefillFromDpia(assessmentId, state);
            PrefillFromMdr(assessmentId, state);
            Recalculate(state);

            _storage[assessmentId] = state;
        }
        else
        {
            // Prefills updaten op basis van huidige DPIA/MDR (override voor B2, non-destructief voor A/C/D).
            PrefillFromDpia(assessmentId, state);
            PrefillFromMdr(assessmentId, state);
            Recalculate(state);
        }

        return state;
    }

    /// <summary>
    /// Update het AI Act-profiel op basis van de velden A2, C2, D2, E2, F2.
    /// B2 (IsHighRiskMedicalDevice) wordt altijd automatisch gezet op basis van MDR.
    /// Null = veld niet wijzigen.
    /// </summary>
    public AiActProfileState UpdateProfile(
        Guid assessmentId,
        string? isAiSystem,
        string? decidesOnEssentialCareTriage,
        string? directClinicalDecision,
        string? interactiveAiWithUser,
        string? generatesContentForUser)
    {
        var state = GetOrCreateForAssessment(assessmentId);

        if (isAiSystem != null)
        {
            state.IsAiSystem = NormalizeAnswer(isAiSystem);
        }

        if (decidesOnEssentialCareTriage != null)
        {
            state.DecidesOnEssentialCareTriage = NormalizeAnswer(decidesOnEssentialCareTriage);
        }

        if (directClinicalDecision != null)
        {
            state.DirectClinicalDecision = NormalizeAnswer(directClinicalDecision);
        }

        if (interactiveAiWithUser != null)
        {
            state.InteractiveAiWithUser = NormalizeAnswer(interactiveAiWithUser);
        }

        if (generatesContentForUser != null)
        {
            state.GeneratesContentForUser = NormalizeAnswer(generatesContentForUser);
        }

        // B2 altijd opnieuw afleiden uit MDR
        PrefillFromMdr(assessmentId, state);
        Recalculate(state);
        return state;
    }

    /// <summary>
    /// Prefill A2, C2 en D2 vanuit de DPIA-quickscan:
    /// - A2 (Is_AI_systeem) ← Q6: Nieuwe technologie (AI, ML, cloud analytics)
    /// - C2 (Beslist_over_toegang_tot_essentiele_zorg) ← Q11
    /// - D2 (Directe_klinische_beslissing_AI) ← Q3
    /// </summary>
    private void PrefillFromDpia(Guid assessmentId, AiActProfileState state)
    {
        var dpia = _dpiaQuickscanService.GetOrCreateForAssessment(assessmentId);

        string? GetAnswer(string code)
        {
            var q = dpia.Questions.FirstOrDefault(
                x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
            return q?.Answer;
        }

        // Q6 → A2 (alleen prefill als nog leeg)
        if (string.IsNullOrWhiteSpace(state.IsAiSystem))
        {
            var a = GetAnswer("Q6"); // "Wordt nieuwe technologie ingezet (AI, machine learning, cloud analytics)?"
            if (!string.IsNullOrWhiteSpace(a))
            {
                state.IsAiSystem = a;
            }
        }

        // Q11 → C2 (alleen prefill als nog leeg)
        if (string.IsNullOrWhiteSpace(state.DecidesOnEssentialCareTriage))
        {
            var c = GetAnswer("Q11"); // "Kan de verwerking leiden tot uitsluiting, evaluatie of medische beoordeling..."
            if (!string.IsNullOrWhiteSpace(c))
            {
                state.DecidesOnEssentialCareTriage = c;
            }
        }

        // Q3 → D2 (alleen prefill als nog leeg)
        if (string.IsNullOrWhiteSpace(state.DirectClinicalDecision))
        {
            var d = GetAnswer("Q3"); // geautomatiseerde besluitvorming / profilering
            if (!string.IsNullOrWhiteSpace(d))
            {
                state.DirectClinicalDecision = d;
            }
        }
    }

    /// <summary>
    /// Prefill B2 vanuit MDR-klasse:
    /// '=IF(
    ///   F2="Onbekend"; "";
    ///   IF(F2="Geen medisch hulpmiddel"; "Nee";
    ///      IF(F2="Klasse I"; "Nee";
    ///         IF(F2 IN ("Klasse IIa","Klasse IIb","Klasse III"); "Ja"; "")
    ///   )))'
    /// </summary>
    private void PrefillFromMdr(Guid assessmentId, AiActProfileState state)
    {
        var mdr = _mdrService.GetOrCreateForAssessment(assessmentId);
        var mdrClass = mdr.MdrClass;

        state.IsHighRiskMedicalDevice = DeriveHighRiskFromMdrClass(mdrClass);
    }

    private static string? DeriveHighRiskFromMdrClass(string? mdrClass)
    {
        if (string.IsNullOrWhiteSpace(mdrClass))
        {
            return null;
        }

        var c = mdrClass.Trim();

        if (string.Equals(c, "Onbekend", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(c, "Geen medisch hulpmiddel", StringComparison.OrdinalIgnoreCase))
        {
            return "Nee";
        }

        if (string.Equals(c, "Klasse I", StringComparison.OrdinalIgnoreCase))
        {
            return "Nee";
        }

        if (string.Equals(c, "Klasse IIa", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c, "Klasse IIb", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c, "Klasse III", StringComparison.OrdinalIgnoreCase))
        {
            return "Ja";
        }

        // Onbekende klasse → geen uitspraak
        return null;
    }

    private static string? NormalizeAnswer(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? NormalizeJaNee(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var t = value.Trim().ToLowerInvariant();
        if (t == "ja")
            return "Ja";
        if (t == "nee" || t == "geen")
            return "Nee";

        return value.Trim();
    }

    private static bool IsYes(string? value)
    {
        var n = NormalizeJaNee(value);
        return string.Equals(n, "Ja", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Implementeert de Excel-formule uit G2/H2:
    ///
    /// =IF(AND(A2="",B2="",C2="",D2="",E2="",F2=""),"Onbekend",
    /// IF(A2<>"Ja",
    ///     "Geen AI-systeem (buiten AI Act)",
    ///     IF(OR(B2="Ja",C2="Ja",D2="Ja"),
    ///         "Hoog risico",
    ///         IF(OR(E2="Ja",F2="Ja"),
    ///             "Beperkt risico",
    ///             "Laag/minimaal risico"
    ///         )
    ///     )
    /// ))
    ///
    /// PLUS: H2 vertaalt tekst naar score 0–3.
    /// </summary>
    private void Recalculate(AiActProfileState state)
    {
        var a = state.IsAiSystem;
        var b = state.IsHighRiskMedicalDevice;
        var c = state.DecidesOnEssentialCareTriage;
        var d = state.DirectClinicalDecision;
        var e = state.InteractiveAiWithUser;
        var f = state.GeneratesContentForUser;

        bool allEmpty =
            string.IsNullOrWhiteSpace(a) &&
            string.IsNullOrWhiteSpace(b) &&
            string.IsNullOrWhiteSpace(c) &&
            string.IsNullOrWhiteSpace(d) &&
            string.IsNullOrWhiteSpace(e) &&
            string.IsNullOrWhiteSpace(f);

        if (allEmpty)
        {
            state.RiskLevel = "Onbekend";
            state.RiskScore = 0;
            state.IsComplete = false;
            state.Explanation =
                "AI Act-profiel is nog niet ingevuld: alle velden A2–F2 zijn leeg.";
            return;
        }

        var aNorm = NormalizeJaNee(a);

        // A2 <> "Ja" → Geen AI-systeem (buiten AI Act)
        if (!string.Equals(aNorm, "Ja", StringComparison.OrdinalIgnoreCase))
        {
            state.RiskLevel = "Geen AI-systeem (buiten AI Act)";
            state.RiskScore = 0;
            state.IsComplete = true;
            state.Explanation =
                "De oplossing wordt niet als AI-systeem aangemerkt (Is_AI_systeem is niet 'Ja'); " +
                "deze valt buiten de scope van de AI Act.";
            return;
        }

        // A2 = "Ja": AI-systeem
        bool anyHigh = IsYes(b) || IsYes(c) || IsYes(d);
        bool anyLimited = IsYes(e) || IsYes(f);

        if (anyHigh)
        {
            state.RiskLevel = "Hoog risico";
            state.RiskScore = 3;
            state.IsComplete = true;
            state.Explanation =
                "De oplossing wordt als AI-systeem aangemerkt en voldoet aan ten minste één hoog-risico criterium " +
                "(medisch hulpmiddel met MDR-klasse IIa/IIb/III, triage/toegang tot essentiële zorg of directe klinische beslissing).";
        }
        else if (anyLimited)
        {
            state.RiskLevel = "Beperkt risico";
            state.RiskScore = 2;
            state.IsComplete = true;
            state.Explanation =
                "De oplossing wordt als AI-systeem aangemerkt en voldoet aan één of meer beperkt-risico criteria " +
                "(interactieve AI met gebruiker of contentgeneratie), zonder hoog-risico kenmerken.";
        }
        else
        {
            state.RiskLevel = "Laag/minimaal risico";
            state.RiskScore = 1;
            state.IsComplete = true;
            state.Explanation =
                "De oplossing wordt als AI-systeem aangemerkt, maar voldoet niet aan hoog- of beperkt-risico criteria; " +
                "de AI Act-risicoklasse is Laag/minimaal risico.";
        }
    }
}
