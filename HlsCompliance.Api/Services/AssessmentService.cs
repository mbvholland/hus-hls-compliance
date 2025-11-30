using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services;

public class AssessmentService
{
    // Tijdelijke in-memory opslag
    private readonly List<Assessment> _assessments = new();

    public IEnumerable<Assessment> GetAll()
    {
        return _assessments;
    }

    public Assessment? GetById(Guid id)
    {
        return _assessments.FirstOrDefault(a => a.Id == id);
    }

    public Assessment Create(string organisation, string supplier, string solution, string hlsVersion)
    {
        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            Organisation = organisation,
            Supplier = supplier,
            Solution = solution,
            HlsVersion = hlsVersion,
            Phase1Status = "not_started",
            Phase2Status = "not_started",
            Phase3Status = "not_started",
            Phase4aStatus = "not_started",
            Phase4bStatus = "not_started",
            CreatedAt = DateTime.UtcNow
        };

        _assessments.Add(assessment);
        return assessment;
    }

    public bool UpdatePhaseStatus(Guid id, string phase, string status)
    {
        var assessment = GetById(id);
        if (assessment == null)
        {
            return false;
        }

        switch (phase.ToLowerInvariant())
        {
            case "phase1":
                assessment.Phase1Status = status;
                break;
            case "phase2":
                assessment.Phase2Status = status;
                break;
            case "phase3":
                assessment.Phase3Status = status;
                break;
            case "phase4a":
                assessment.Phase4aStatus = status;
                break;
            case "phase4b":
                assessment.Phase4bStatus = status;
                break;
            default:
                // Onbekende fase
                return false;
        }

        assessment.UpdatedAt = DateTime.UtcNow;
        return true;
    }
}
