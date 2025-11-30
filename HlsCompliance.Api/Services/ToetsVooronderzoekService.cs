using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services;

public class ToetsVooronderzoekService
{
    private readonly Dictionary<Guid, ToetsVooronderzoekState> _storage = new();

    public ToetsVooronderzoekState GetOrCreateForAssessment(Guid assessmentId)
    {
        if (_storage.TryGetValue(assessmentId, out var existing))
        {
            return existing;
        }

        var state = new ToetsVooronderzoekState
        {
            AssessmentId = assessmentId,
            RequiresFullAssessment = null,
            Motivation = null,
            Status = "Onbekend",
            LastUpdated = DateTimeOffset.UtcNow
        };

        _storage[assessmentId] = state;
        return state;
    }

    public ToetsVooronderzoekState Update(
        Guid assessmentId,
        bool? requiresFullAssessment,
        string? motivation,
        string? status)
    {
        var state = GetOrCreateForAssessment(assessmentId);

        state.RequiresFullAssessment = requiresFullAssessment;
        state.Motivation = motivation ?? state.Motivation;

        if (!string.IsNullOrWhiteSpace(status))
        {
            state.Status = status;
        }

        state.LastUpdated = DateTimeOffset.UtcNow;

        _storage[assessmentId] = state;
        return state;
    }
}
