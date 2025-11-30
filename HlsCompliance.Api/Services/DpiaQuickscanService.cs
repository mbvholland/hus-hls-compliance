using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services;

public class DpiaQuickscanService
{
    private readonly Dictionary<Guid, DpiaQuickscanResult> _storage = new();

    public DpiaQuickscanResult GetOrCreateForAssessment(Guid assessmentId)
    {
        if (_storage.TryGetValue(assessmentId, out var existing))
        {
            return existing;
        }

        var result = new DpiaQuickscanResult
        {
            AssessmentId = assessmentId,
            Questions = CreateDefaultQuestions(),
            DpiaRequired = null,
            DpiaRequiredReason = "Nog niet alle vragen zijn beantwoord.",
            AnsweredMandatoryCount = 0,
            UnansweredMandatoryCount = 0,
            RiskQuestionsAnsweredYes = 0,
            LastUpdated = DateTimeOffset.UtcNow
        };

        Recalculate(result);

        _storage[assessmentId] = result;
        return result;
    }

    /// <summary>
    /// Update de antwoorden op basis van vraagcodes (Q1..Q14).
    /// </summary>
    public DpiaQuickscanResult UpdateAnswers(
        Guid assessmentId,
        IEnumerable<(string Code, string? Answer)> answers)
    {
        var result = GetOrCreateForAssessment(assessmentId);

        foreach (var (code, answer) in answers)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var question = result.Questions.FirstOrDefault(q =>
                string.Equals(q.Code, code, StringComparison.OrdinalIgnoreCase));

            if (question == null)
            {
                // Onbekende code negeren (eventueel later loggen).
                continue;
            }

            question.Answer = string.IsNullOrWhiteSpace(answer)
                ? null
                : answer.Trim();
        }

        Recalculate(result);
        return result;
    }

    /// <summary>
    /// Kernlogica:
    /// - Niet alle verplichte vragen beantwoord -> DpiaRequired = null.
    /// - Als Q1 = "Nee" -> geen persoonsgegevens -> DpiaRequired = false.
    /// - Anders: als een risicovraag "Ja" is -> DpiaRequired = true.
    /// - Zo niet -> DpiaRequired = false.
    /// </summary>
    private void Recalculate(DpiaQuickscanResult result)
    {
        var mandatory = result.Questions.Where(q => q.IsMandatory).ToList();
        var answeredMandatory = mandatory
            .Where(q => !string.IsNullOrWhiteSpace(q.Answer))
            .ToList();

        result.AnsweredMandatoryCount = answeredMandatory.Count;
        result.UnansweredMandatoryCount = mandatory.Count - answeredMandatory.Count;

        if (!mandatory.Any())
        {
            result.DpiaRequired = null;
            result.DpiaRequiredReason = "Er zijn geen vragen geconfigureerd voor de quickscan.";
            result.RiskQuestionsAnsweredYes = 0;
            result.LastUpdated = DateTimeOffset.UtcNow;
            return;
        }

        if (result.UnansweredMandatoryCount > 0)
        {
            result.DpiaRequired = null;
            result.DpiaRequiredReason =
                "Niet alle verplichte vragen zijn beantwoord; de DPIA-verplichting kan nog niet worden bepaald.";
            result.RiskQuestionsAnsweredYes = 0;
            result.LastUpdated = DateTimeOffset.UtcNow;
            return;
        }

        // Speciale poortwachter: Q1 = persoonsgegevens ja/nee
        var q1 = result.Questions.FirstOrDefault(q =>
            string.Equals(q.Code, "Q1", StringComparison.OrdinalIgnoreCase));

        if (q1 != null &&
            !string.IsNullOrWhiteSpace(q1.Answer) &&
            string.Equals(q1.Answer, "Nee", StringComparison.OrdinalIgnoreCase))
        {
            // Geen persoonsgegevens -> geen DPIA-plicht, wel registratielast
            result.DpiaRequired = false;
            result.DpiaRequiredReason =
                "Er worden geen persoonsgegevens verwerkt; een DPIA is niet verplicht (wel registratielast).";
            result.RiskQuestionsAnsweredYes = 0;
            result.LastUpdated = DateTimeOffset.UtcNow;
            return;
        }

        var riskYes = result.Questions
            .Where(q => q.IsRiskQuestion)
            .Count(q => string.Equals(q.Answer, "Ja", StringComparison.OrdinalIgnoreCase));

        result.RiskQuestionsAnsweredYes = riskYes;

        if (riskYes > 0)
        {
            result.DpiaRequired = true;
            result.DpiaRequiredReason =
                "Ten minste één risicovraag is met 'Ja' beantwoord; een DPIA is vereist.";
        }
        else
        {
            result.DpiaRequired = false;
            result.DpiaRequiredReason =
                "Alle verplichte vragen zijn beantwoord en geen risicovraag is met 'Ja' beantwoord; een DPIA lijkt niet verplicht.";
        }

        result.LastUpdated = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Vragen uit tabblad '1. DPIA_Quickscan' van de HLS Excel.
    /// IsRiskQuestion is gebaseerd op de kolom 'Risico-indicatie':
    /// - Laag  -> false
    /// - Middel/Hoog -> true
    /// </summary>
    private List<DpiaQuickscanQuestion> CreateDefaultQuestions()
    {
        return new List<DpiaQuickscanQuestion>
        {
            new()
            {
                Code = "Q1",
                Text = "Worden persoonsgegevens verwerkt binnen de applicatie of dienst?",
                IsRiskQuestion = false,   // Risico-indicatie: Laag
                IsMandatory = true
            },
            new()
            {
                Code = "Q2",
                Text = "Worden bijzondere persoonsgegevens verwerkt, zoals gezondheidsgegevens, genetische of biometrische data?",
                IsRiskQuestion = true,    // Risico-indicatie: Hoog
                IsMandatory = true
            },
            new()
            {
                Code = "Q3",
                Text = "Bevat de applicatie automatische besluitvorming of profilering...met juridische, medische of significante gevolgen voor personen?",
                IsRiskQuestion = true,    // Risico-indicatie: Hoog
                IsMandatory = true
            },
            new()
            {
                Code = "Q4",
                Text = "Vindt grootschalige verwerking plaats van persoonsgegevens of zorgdata?",
                IsRiskQuestion = true,    // Risico-indicatie: Hoog
                IsMandatory = true
            },
            new()
            {
                Code = "Q5",
                Text = "Worden individuen gevolgd, gemonitord of geobserveerd via logins, sensoren of gedragstracering?",
                IsRiskQuestion = true,    // Risico-indicatie: Hoog
                IsMandatory = true
            },
            new()
            {
                Code = "Q6",
                Text = "Wordt nieuwe technologie ingezet (AI, machine learning, cloud analytics)?",
                IsRiskQuestion = true,    // Risico-indicatie: Hoog
                IsMandatory = true
            },
            new()
            {
                Code = "Q7",
                Text = "Hebben derde partijen of subverwerkers toegang tot persoonsgegevens?",
                IsRiskQuestion = true,    // Risico-indicatie: Middel
                IsMandatory = true
            },
            new()
            {
                Code = "Q8",
                Text = "Worden persoonsgegevens buiten de EU/EER verwerkt of opgeslagen?",
                IsRiskQuestion = true,    // Risico-indicatie: Hoog
                IsMandatory = true
            },
            new()
            {
                Code = "Q9",
                Text = "Is er sprake van datakoppelingen met andere zorgsystemen of externe partijen?",
                IsRiskQuestion = true,    // Risico-indicatie: Hoog
                IsMandatory = true
            },
            new()
            {
                Code = "Q10",
                Text = "Betreft de verwerking kwetsbare betrokkenen, zoals patiënten, ouderen of kinderen?",
                IsRiskQuestion = true,    // Risico-indicatie: Hoog
                IsMandatory = true
            },
            new()
            {
                Code = "Q11",
                Text = "Kan de verwerking leiden tot uitsluiting, evaluatie of medische beoordeling van personen?",
                IsRiskQuestion = true,    // Risico-indicatie: Hoog
                IsMandatory = true
            },
            new()
            {
                Code = "Q12",
                Text = "Is de verwerking structureel, herhalend of langdurig?",
                IsRiskQuestion = true,    // Risico-indicatie: Middel
                IsMandatory = true
            },
            new()
            {
                Code = "Q13",
                Text = "Kunnen personen direct of indirect worden geïdentificeerd via dataset of logs?",
                IsRiskQuestion = true,    // Risico-indicatie: Hoog
                IsMandatory = true
            },
            new()
            {
                Code = "Q14",
                Text = "Zou een datalek of fout aanzienlijke schade veroorzaken voor betrokkenen?",
                IsRiskQuestion = true,    // Risico-indicatie: Hoog
                IsMandatory = true
            },
        };
    }
}
