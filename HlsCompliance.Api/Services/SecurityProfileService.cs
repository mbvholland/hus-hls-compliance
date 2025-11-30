using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services;

public class SecurityProfileService
{
    private readonly Dictionary<Guid, SecurityProfileState> _storage = new();

    public SecurityProfileState GetOrCreateForAssessment(Guid assessmentId)
    {
        if (_storage.TryGetValue(assessmentId, out var existing))
        {
            return existing;
        }

        var state = new SecurityProfileState
        {
            AssessmentId = assessmentId,
            ProfileVersion = null,
            OverallSecurityLevel = "Onbekend",
            BlockScores = new Dictionary<string, string>(),
            LastUpdated = DateTimeOffset.UtcNow
        };

        _storage[assessmentId] = state;
        return state;
    }

    public SecurityProfileState UpdateProfile(
        Guid assessmentId,
        string? profileVersion,
        string? overallSecurityLevel,
        Dictionary<string, string>? blockScores)
    {
        var state = GetOrCreateForAssessment(assessmentId);

        state.ProfileVersion = profileVersion ?? state.ProfileVersion;

        if (!string.IsNullOrWhiteSpace(overallSecurityLevel))
        {
            state.OverallSecurityLevel = overallSecurityLevel;
        }

        if (blockScores is not null)
        {
            state.BlockScores = blockScores;
        }

        state.LastUpdated = DateTimeOffset.UtcNow;

        _storage[assessmentId] = state;
        return state;
    }
}
