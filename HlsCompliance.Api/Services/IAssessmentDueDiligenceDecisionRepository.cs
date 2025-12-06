using System;
using System.Collections.Generic;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Repository-interface voor due diligence-beslissingen (kolom K/M)
    /// per assessment + ChecklistId.
    /// </summary>
    public interface IAssessmentDueDiligenceDecisionRepository
    {
        /// <summary>
        /// Haal alle beslissingen op voor één assessment.
        /// </summary>
        IReadOnlyList<AssessmentDueDiligenceDecision> GetByAssessment(Guid assessmentId);

        /// <summary>
        /// Voeg een beslissing toe of werk deze bij (per AssessmentId + ChecklistId).
        /// </summary>
        void Upsert(Guid assessmentId, string checklistId, bool negativeOutcomeAcceptable, string? deviationText);
    }
}
