using System;
using System.Collections.Generic;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Persistentie voor bewijslast (tab 11) per assessment.
    /// </summary>
    public interface IAssessmentEvidenceRepository
    {
        /// <summary>
        /// Haalt alle bewijslast-items op voor een assessment.
        /// </summary>
        IReadOnlyCollection<AssessmentEvidenceItem> GetByAssessment(Guid assessmentId);

        /// <summary>
        /// Voeg een bewijslast-item toe of update een bestaand item
        /// (combinatie AssessmentId + ChecklistId + EvidenceId).
        /// </summary>
        void UpsertEvidence(AssessmentEvidenceItem item);
    }
}
