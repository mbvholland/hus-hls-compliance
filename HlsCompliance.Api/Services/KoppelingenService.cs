using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services;

public class KoppelingenService
{
    private readonly Dictionary<Guid, KoppelingenResult> _storage = new();

    public KoppelingenResult GetOrCreateForAssessment(Guid assessmentId)
    {
        if (_storage.TryGetValue(assessmentId, out var existing))
        {
            return existing;
        }

        var result = new KoppelingenResult
        {
            AssessmentId = assessmentId,
            OverallRiskLevel = "Onbekend",
            Explanation = "Nog geen koppelingen geregistreerd."
        };

        _storage[assessmentId] = result;
        return result;
    }

    public KoppelingenResult AddOrUpdateConnection(Guid assessmentId, Koppeling connection)
    {
        var result = GetOrCreateForAssessment(assessmentId);

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

        RecalculateOverallRisk(result);
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
        RecalculateOverallRisk(result);
        return true;
    }

    private void RecalculateOverallRisk(KoppelingenResult result)
    {
        if (!result.Connections.Any())
        {
            result.OverallRiskLevel = "Onbekend";
            result.Explanation = "Er zijn nog geen koppelingen geregistreerd.";
            return;
        }

        // Placeholder-logica:
        // Hoog > Middel > Laag > Geen
        // Als er één "Hoog" is → Overall "Hoog", etc.

        var levels = result.Connections
            .Select(c => c.RiskLevel)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim().ToLowerInvariant())
            .ToList();

        if (!levels.Any())
        {
            result.OverallRiskLevel = "Onbekend";
            result.Explanation = "Geen risiconiveaus ingesteld voor de geregistreerde koppelingen.";
            return;
        }

        if (levels.Contains("hoog"))
        {
            result.OverallRiskLevel = "Hoog";
            result.Explanation = "Ten minste één koppeling heeft risiconiveau 'Hoog'.";
        }
        else if (levels.Contains("middel"))
        {
            result.OverallRiskLevel = "Middel";
            result.Explanation = "Er zijn koppelingen met risiconiveau 'Middel', geen 'Hoog'.";
        }
        else if (levels.Contains("laag"))
        {
            result.OverallRiskLevel = "Laag";
            result.Explanation = "Er zijn alleen koppelingen met risiconiveau 'Laag'.";
        }
        else if (levels.Contains("geen"))
        {
            result.OverallRiskLevel = "Geen";
            result.Explanation = "Alle koppelingen hebben risiconiveau 'Geen'.";
        }
        else
        {
            result.OverallRiskLevel = "Onbekend";
            result.Explanation = "Risiconiveaus konden niet eenduidig worden geïnterpreteerd.";
        }
    }
}
