using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services;

public class DpiaQuickscanService
{
    // In-memory opslag per assessment
    private readonly Dictionary<Guid, DpiaQuickscanResult> _storage = new();

    // Voorbeeld: vaste set vraagnummers (1 t/m 10, later kun je dit uitbreiden)
    private static readonly int[] AllQuestionNumbers = Enumerable.Range(1, 10).ToArray();

    // Voorbeeld: risico-vragen (deze lijst pas je later aan naar je echte Excel-logica)
    private static readonly int[] RiskQuestionNumbers = { 2, 3, 5, 7 };

    public DpiaQuickscanResult GetOrCreateForAssessment(Guid assessmentId)
    {
        if (_storage.TryGetValue(assessmentId, out var existing))
        {
            return existing;
        }

        // Nieuwe quickscan initialiseren met lege antwoorden
        var result = new DpiaQuickscanResult
        {
            AssessmentId = assessmentId,
            Questions = AllQuestionNumbers
                .Select(n => new DpiaQuickscanQuestion
                {
                    Number = n,
                    Code = $"DPIA-Q{n}",
                    Text = $"DPIA vraag {n} (later vervangen door echte tekst)",
                    Answer = null
                })
                .ToList(),
            DpiaRequired = null,
            Explanation = "Nog niet alle vragen zijn ingevuld."
        };

        _storage[assessmentId] = result;
        return result;
    }

    public DpiaQuickscanResult UpdateAnswers(Guid assessmentId, Dictionary<int, string?> answers)
    {
        var quickscan = GetOrCreateForAssessment(assessmentId);

        foreach (var kvp in answers)
        {
            var questionNumber = kvp.Key;
            var answer = kvp.Value; // verwacht "Ja", "Nee" of null

            var question = quickscan.Questions.FirstOrDefault(q => q.Number == questionNumber);
            if (question != null)
            {
                // Normalizeer antwoorden een beetje
                if (string.IsNullOrWhiteSpace(answer))
                {
                    question.Answer = null;
                }
                else
                {
                    question.Answer = answer.Trim();
                }
            }
        }

        // Herbereken de uitkomst
        RecalculateResult(quickscan);

        return quickscan;
    }

    private void RecalculateResult(DpiaQuickscanResult quickscan)
    {
        // 1. Zijn alle vragen beantwoord (Ja/Nee)?
        var unanswered = quickscan.Questions
            .Where(q => q.Answer == null || q.Answer.Trim() == string.Empty)
            .ToList();

        if (unanswered.Any())
        {
            quickscan.DpiaRequired = null;
            quickscan.Explanation = "Nog niet alle DPIA-quickscanvragen zijn beantwoord.";
            return;
        }

        // 2. Eenvoudige risico-logica:
        //    - als een van de risicovragen "Ja" is -> DPIA verplicht
        //    - anders niet verplicht
        var anyRiskYes = quickscan.Questions
            .Where(q => RiskQuestionNumbers.Contains(q.Number))
            .Any(q => string.Equals(q.Answer, "Ja", StringComparison.OrdinalIgnoreCase));

        if (anyRiskYes)
        {
            quickscan.DpiaRequired = true;
            quickscan.Explanation = "Minstens één risicovraag is met 'Ja' beantwoord. DPIA is verplicht (voorlopige logica).";
        }
        else
        {
            quickscan.DpiaRequired = false;
            quickscan.Explanation = "Alle risicovragen zijn met 'Nee' beantwoord. DPIA is niet verplicht (voorlopige logica).";
        }
    }
}
