using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services;

/// <summary>
/// Service voor tab "5. Securityprofiel leverancier".
/// Spiegelt de logica:
/// - C8/C12 zijn afgeleid uit DPIA_Quickscan (E2/E8).
/// - D8..D15 = IF(Cx="Ja";1;0)
/// - F8..F15 = IF(Dx=1; weight(Ex); 0)
/// - F17 = AVERAGE(F8:F15)
/// - C16 = COUNTA(C8:C15)=8?
/// </summary>
public class SecurityProfileService
{
    private readonly Dictionary<Guid, SecurityProfileResult> _storage = new();
    private readonly DpiaQuickscanService _dpiaQuickscanService;

    public SecurityProfileService(DpiaQuickscanService dpiaQuickscanService)
    {
        _dpiaQuickscanService = dpiaQuickscanService;
    }

    public SecurityProfileResult GetOrCreateForAssessment(Guid assessmentId)
    {
        if (!_storage.TryGetValue(assessmentId, out var result))
        {
            result = new SecurityProfileResult
            {
                AssessmentId = assessmentId,
                Questions = CreateDefaultQuestions()
            };

            PrefillFromDpia(assessmentId, result);
            Recalculate(result);

            _storage[assessmentId] = result;
        }
        else
        {
            // Afgeleide velden (C8/C12) elke keer updaten vanuit DPIA.
            PrefillFromDpia(assessmentId, result);
            Recalculate(result);
        }

        return result;
    }

    /// <summary>
    /// Update antwoorden voor alle niet-afgeleide vragen.
    /// Afgeleide vragen (IsDerivedFromDpia = true) worden genegeerd
    /// en altijd uit DPIA gevuld.
    /// </summary>
    public SecurityProfileResult UpdateAnswers(
        Guid assessmentId,
        IEnumerable<(string Code, string? Answer)> answers)
    {
        var result = GetOrCreateForAssessment(assessmentId);

        foreach (var (code, answer) in answers)
        {
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var question = result.Questions
                .FirstOrDefault(q => string.Equals(q.Code, code, StringComparison.OrdinalIgnoreCase));

            if (question == null)
                continue;

            if (question.IsDerivedFromDpia)
            {
                // C8/C12 worden altijd uit DPIA gehaald, user-input negeren.
                continue;
            }

            question.Answer = string.IsNullOrWhiteSpace(answer)
                ? null
                : answer.Trim();
        }

        // Afgeleide vragen opnieuw vullen uit DPIA
        PrefillFromDpia(assessmentId, result);
        Recalculate(result);

        return result;
    }

    /// <summary>
    /// Haal de bronantwoorden uit DPIA_Quickscan voor de
    /// afgeleide vragen (C8 en C12).
    /// - C8  <- DPIA Q1  (E2)
    /// - C12 <- DPIA Q7  (E8)
    /// </summary>
    private void PrefillFromDpia(Guid assessmentId, SecurityProfileResult result)
    {
        var dpia = _dpiaQuickscanService.GetOrCreateForAssessment(assessmentId);

        string? GetDpiaAnswer(string code)
        {
            var q = dpia.Questions.FirstOrDefault(
                x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
            return q?.Answer;
        }

        foreach (var q in result.Questions.Where(q => q.IsDerivedFromDpia))
        {
            if (string.IsNullOrWhiteSpace(q.DpiaSourceQuestionCode))
            {
                q.Answer = null;
                continue;
            }

            q.Answer = GetDpiaAnswer(q.DpiaSourceQuestionCode);
        }
    }

    private static bool IsYes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim().ToLowerInvariant();
        return v == "ja";
    }

    /// <summary>
    /// Bereken IsComplete + RiskScore + Explanation:
    /// - RiskScore = gemiddelde van per-vraagscore:
    ///     IF(Answer="Ja"; RiskWeight; 0)
    /// - IsComplete = alle vragen hebben een niet-lege Answer
    ///   (incl. derived, dus vereist ook dat DPIA Q1/Q7 is ingevuld).
    /// </summary>
    private void Recalculate(SecurityProfileResult result)
    {
        var questions = result.Questions ?? new List<SecurityProfileQuestion>();

        if (!questions.Any())
        {
            result.IsComplete = false;
            result.RiskScore = 0.0;
            result.Explanation = "Er zijn geen vragen geconfigureerd voor het securityprofiel.";
            result.LastUpdated = DateTimeOffset.UtcNow;
            return;
        }

        var perQuestionScores = questions.Select(q =>
            IsYes(q.Answer) ? q.RiskWeight : 0);

        var scoresList = perQuestionScores.ToList();
        result.RiskScore = scoresList.Any()
            ? scoresList.Average()
            : 0.0;

        // Compleetheid: alle 8 antwoorden ingevuld
        result.IsComplete = questions.All(q =>
            !string.IsNullOrWhiteSpace(q.Answer));

        var yesCount = questions.Count(q => IsYes(q.Answer));

        result.Explanation =
            $"Securityprofiel leverancier: {yesCount} van {questions.Count} vragen zijn met 'Ja' beantwoord. " +
            $"Gemiddelde risicoscore (F17) = {result.RiskScore:0.00}. " +
            (result.IsComplete
                ? "Alle vragen zijn ingevuld."
                : "Niet alle vragen zijn ingevuld (profiel is onvolledig).");

        result.LastUpdated = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Vragen uit tabblad '5. Securityprofiel leverancier' (rij 8 t/m 15),
    /// met RiskClass (E-kolom) en RiskWeight (F-formule-gewicht).
    ///
    /// Mapping RiskClass -> RiskWeight:
    /// - "Zeer Hoog" -> 6
    /// - "Hoog"      -> 4
    /// - "Gemiddeld" -> 2
    /// - "Laag"      -> 1
    ///
    /// Afgeleide vragen:
    /// - Q1 (C8)  <- DPIA Q1
    /// - Q5 (C12) <- DPIA Q7
    /// </summary>
    private List<SecurityProfileQuestion> CreateDefaultQuestions()
    {
        return new List<SecurityProfileQuestion>
        {
            new()
            {
                Code = "Q1",
                Text = "Heeft toegang tot persoonsgegevens van patienten van HUS",
                RiskClass = "Hoog",
                RiskWeight = 4,
                IsDerivedFromDpia = true,
                DpiaSourceQuestionCode = "Q1"
            },
            new()
            {
                Code = "Q2",
                Text = "Heeft toegang tot vertrouwelijke of geheime bedrijfs- of zorginformatie van HUS",
                RiskClass = "Zeer Hoog",
                RiskWeight = 6,
                IsDerivedFromDpia = false
            },
            new()
            {
                Code = "Q3",
                Text = "Beheert ICT dat wordt gebruikt door HUS (eigendom van leverancier of HUS)",
                RiskClass = "Hoog",
                RiskWeight = 4,
                IsDerivedFromDpia = false
            },
            new()
            {
                Code = "Q4",
                Text = "Ontwikkelt of levert software dat wordt gebruikt door HUS (niet alleen softwarelicenties van derde partijen)",
                RiskClass = "Hoog",
                RiskWeight = 4,
                IsDerivedFromDpia = false
            },
            new()
            {
                Code = "Q5",
                Text = "Gebruikt diensten van subleveranciers voor dienstverlening aan HUS",
                RiskClass = "Hoog",
                RiskWeight = 4,
                IsDerivedFromDpia = true,
                DpiaSourceQuestionCode = "Q7"
            },
            new()
            {
                Code = "Q6",
                Text = "Heeft toegang tot niet-publieke delen van HUS gebouwen",
                RiskClass = "Gemiddeld",
                RiskWeight = 2,
                IsDerivedFromDpia = false
            },
            new()
            {
                Code = "Q7",
                Text = "Gebruikt voor dienstverlening aan HUS gebouwen die niet toebehoren aan, of niet onder beheer vallen van HUS",
                RiskClass = "Laag",
                RiskWeight = 1,
                IsDerivedFromDpia = false
            },
            new()
            {
                Code = "Q8",
                Text = "Voert activiteiten uit onder gezag van HUS (Projecten, verzoek tot Wijzigingen, etc.)",
                RiskClass = "Gemiddeld",
                RiskWeight = 2,
                IsDerivedFromDpia = false
            }
        };
    }
}
