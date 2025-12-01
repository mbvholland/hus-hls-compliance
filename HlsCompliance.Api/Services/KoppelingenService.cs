using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services;

public class KoppelingenService
{
    private readonly Dictionary<Guid, KoppelingenResult> _storage = new();
    private readonly DpiaQuickscanService _dpiaQuickscanService;

    public KoppelingenService(DpiaQuickscanService dpiaQuickscanService)
    {
        _dpiaQuickscanService = dpiaQuickscanService;
    }

    public KoppelingenResult GetOrCreateForAssessment(Guid assessmentId)
    {
        if (!_storage.TryGetValue(assessmentId, out var existing))
        {
            existing = new KoppelingenResult
            {
                AssessmentId = assessmentId,
                OverallRiskLevel = "Onbekend",
                OverallRiskScore = 0,
                Explanation = "Nog geen koppelingen geregistreerd."
            };

            _storage[assessmentId] = existing;
        }

        // Elke keer dat we de staat ophalen, opnieuw overall risk bepalen
        // op basis van huidige DPIA-quickscan + geregistreerde koppelingen.
        RecalculateOverallRisk(assessmentId, existing);
        return existing;
    }

    /// <summary>
    /// Voeg een koppeling toe of update een bestaande koppeling.
    /// RiskLevel wordt altijd automatisch berekend uit DataSensitivity
    /// volgens HLS tab "2. Koppeling-Beslisboom".
    /// </summary>
    public KoppelingenResult AddOrUpdateConnection(Guid assessmentId, Koppeling connection)
    {
        var result = GetOrCreateForAssessment(assessmentId);

        // RiskLevel altijd herberekenen op basis van DataSensitivity
        connection.RiskLevel = CalculateRiskLevelFromSensitivity(connection.DataSensitivity);

        // Bestaat deze koppeling al? (Id check)
        var existing = result.Connections.FirstOrDefault(c => c.Id == connection.Id);
        if (existing == null)
        {
            // Nieuwe koppeling
            result.Connections.Add(connection);
        }
        else
        {
            // Bestaande koppeling bijwerken
            existing.Name = connection.Name;
            existing.Type = connection.Type;
            existing.Direction = connection.Direction;
            existing.DataSensitivity = connection.DataSensitivity;
            existing.RiskLevel = connection.RiskLevel;
        }

        RecalculateOverallRisk(assessmentId, result);
        return result;
    }

    public bool RemoveConnection(Guid assessmentId, Guid connectionId)
    {
        var result = GetOrCreateForAssessment(assessmentId);

        var existing = result.Connections.FirstOrDefault(c => c.Id == connectionId);
        if (existing == null)
        {
            return false;
        }

        result.Connections.Remove(existing);
        RecalculateOverallRisk(assessmentId, result);
        return true;
    }

    /// <summary>
    /// Haalt het antwoord op de DPIA-vraag over koppelingen op.
    /// In de Excel is dit DPIA_Quickscan!E10 -> tab "2. Koppeling-Beslisboom"!A2.
    /// In onze API gebruiken we hiervoor vraagcode "Q9":
    /// "Is er sprake van datakoppelingen met andere zorgsystemen of externe partijen?"
    /// </summary>
    private string? GetDpiaConnectionsAnswer(Guid assessmentId)
    {
        var dpia = _dpiaQuickscanService.GetOrCreateForAssessment(assessmentId);

        var q = dpia.Questions
            .FirstOrDefault(q => string.Equals(q.Code, "Q9", StringComparison.OrdinalIgnoreCase));

        return q?.Answer;
    }

    private static string? NormalizeJaNee(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var v = value.Trim().ToLowerInvariant();
        if (v == "ja") return "Ja";
        if (v == "nee") return "Nee";
        if (v == "geen") return "Nee"; // "Geen koppelingen" behandelen als Nee
        return null;
    }

    /// <summary>
    /// Mirror van de Excel-logica in tab 2 "Koppeling-Beslisboom":
    /// DataSensitivity -> RiskLevel.
    /// </summary>
    private string CalculateRiskLevelFromSensitivity(string? sensitivity)
    {
        if (string.IsNullOrWhiteSpace(sensitivity))
        {
            return "Onbekend";
        }

        var v = sensitivity.Trim().ToLowerInvariant();

        // Exacte Excel-waarden (hoofdletterongevoelig)
        if (v == "geen")
        {
            return "Geen";
        }

        if (v == "laag")
        {
            return "Laag";
        }

        if (v == "geaggregeerd/geanonimiseerd/pseudoniem")
        {
            return "Middel";
        }

        if (v == "identificeerbaar medisch of persoon")
        {
            return "Hoog";
        }

        // Onbekende/afwijkende waarde
        return "Onbekend";
    }

    /// <summary>
    /// Zet OverallRiskLevel om naar numerieke score 0–3, zoals D2 in Excel:
    /// "Geen"      -> 0
    /// "Laag"      -> 1
    /// "Middel"    -> 2
    /// "Hoog"      -> 3
    /// "Onbekend"  -> 0
    /// </summary>
    private int MapRiskLevelToScore(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
            return 0;

        var v = level.Trim().ToLowerInvariant();

        return v switch
        {
            "geen" => 0,
            "laag" => 1,
            "middel" => 2,
            "hoog" => 3,
            "onbekend" => 0,
            _ => 0
        };
    }

    /// <summary>
    /// Bepaalt het overall-risico over alle koppelingen, met DPIA-poortwachter (Q9/E10):
    ///
    /// - Als DPIA Q9 = Nee/Geen én er zijn geen koppelingen geregistreerd:
    ///     Overall = "Geen" (zoals A2="Geen" -> risicoklasse "Geen" in Excel),
    ///     Explanation legt uit dat DPIA aangeeft dat er geen koppelingen zijn.
    ///
    /// - Als er wél koppelingen geregistreerd zijn:
    ///     Hoog > Middel > Laag > Geen > Onbekend.
    ///     Explanation bevat tellingen per niveau.
    ///     Als DPIA Q9 = Nee/Geen maar er zijn toch koppelingen, melden we dat.
    ///
    /// - Als geen koppelingen én DPIA Q9 onbekend:
    ///     Overall = "Onbekend".
    ///
    /// Daarnaast wordt overal OverallRiskScore (0–3) gezet
    /// volgens dezelfde mapping als Excel D2.
    /// </summary>
    private void RecalculateOverallRisk(Guid assessmentId, KoppelingenResult result)
    {
        var dpiaAnswerRaw = GetDpiaConnectionsAnswer(assessmentId);
        var dpiaAnswer = NormalizeJaNee(dpiaAnswerRaw);

        var hasConnections = result.Connections.Any();

        // Case 1: DPIA zegt expliciet "geen koppelingen" en er zijn er ook geen geregistreerd.
        if (string.Equals(dpiaAnswer, "Nee", StringComparison.OrdinalIgnoreCase) && !hasConnections)
        {
            result.OverallRiskLevel = "Geen";
            result.OverallRiskScore = MapRiskLevelToScore(result.OverallRiskLevel);
            result.Explanation =
                "Volgens de DPIA-quickscan (vraag Q9: datakoppelingen) zijn er geen koppelingen. " +
                "Overall risiconiveau voor koppelingen is 'Geen'.";
            return;
        }

        // Case 2: Er zijn nog geen koppelingen geregistreerd en DPIA is leeg/onduidelijk.
        if (!hasConnections && dpiaAnswer is null)
        {
            result.OverallRiskLevel = "Onbekend";
            result.OverallRiskScore = MapRiskLevelToScore(result.OverallRiskLevel);
            result.Explanation =
                "Er zijn nog geen koppelingen geregistreerd en de DPIA-quickscan (vraag Q9) is nog niet ingevuld.";
            return;
        }

        // Vanaf hier: óf er zijn koppelingen geregistreerd, óf DPIA geeft aan dat er koppelingen zijn.
        if (!hasConnections)
        {
            // DPIA = Ja, maar nog geen koppelingen ingevuld in de module.
            result.OverallRiskLevel = "Onbekend";
            result.OverallRiskScore = MapRiskLevelToScore(result.OverallRiskLevel);
            result.Explanation =
                "De DPIA-quickscan (vraag Q9) geeft aan dat er koppelingen zijn, " +
                "maar er zijn nog geen koppelingen geregistreerd in deze HLS-module.";
            return;
        }

        // Er zijn koppelingen: per-koppeling risiconiveaus tellen
        var levels = result.Connections
            .Select(c => c.RiskLevel ?? "Onbekend")
            .Select(l => l.Trim())
            .ToList();

        // Tellingen per niveau
        int countHoog = levels.Count(l => string.Equals(l, "Hoog", StringComparison.OrdinalIgnoreCase));
        int countMiddel = levels.Count(l => string.Equals(l, "Middel", StringComparison.OrdinalIgnoreCase));
        int countLaag = levels.Count(l => string.Equals(l, "Laag", StringComparison.OrdinalIgnoreCase));
        int countGeen = levels.Count(l => string.Equals(l, "Geen", StringComparison.OrdinalIgnoreCase));
        int countOnbekend = levels.Count(l => string.Equals(l, "Onbekend", StringComparison.OrdinalIgnoreCase));

        // Overall: Hoog > Middel > Laag > Geen > Onbekend
        if (countHoog > 0)
        {
            result.OverallRiskLevel = "Hoog";
        }
        else if (countMiddel > 0)
        {
            result.OverallRiskLevel = "Middel";
        }
        else if (countLaag > 0)
        {
            result.OverallRiskLevel = "Laag";
        }
        else if (countGeen > 0)
        {
            result.OverallRiskLevel = "Geen";
        }
        else
        {
            result.OverallRiskLevel = "Onbekend";
        }

        result.OverallRiskScore = MapRiskLevelToScore(result.OverallRiskLevel);

        var total = levels.Count;

        var explanation =
            $"Totaal {total} koppeling(en). " +
            $"Hoog: {countHoog}, Middel: {countMiddel}, Laag: {countLaag}, Geen: {countGeen}, Onbekend: {countOnbekend}. " +
            $"Overall risiconiveau: {result.OverallRiskLevel} (score {result.OverallRiskScore}).";

        // Als DPIA zegt "geen koppelingen", maar we er toch hebben geregistreerd, dit benoemen.
        if (string.Equals(dpiaAnswer, "Nee", StringComparison.OrdinalIgnoreCase) && hasConnections)
        {
            explanation +=
                " Let op: de DPIA-quickscan (vraag Q9) gaf aan dat er geen koppelingen zijn, " +
                "maar er zijn wel koppelingen geregistreerd in deze assessment.";
        }

        result.Explanation = explanation;
    }
}
