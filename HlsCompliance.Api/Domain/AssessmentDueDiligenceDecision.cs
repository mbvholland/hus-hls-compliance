using System;

namespace HlsCompliance.Api.Domain
{
    /// <summary>
    /// Persistent storage of Due Diligence decisions per assessment+checklist row:
    /// - NegativeOutcomeAcceptable (kolom K)
    /// - DeviationText (kolom M)
    /// </summary>
    public class AssessmentDueDiligenceDecision
    {
        public Guid AssessmentId { get; set; }

        /// <summary>
        /// ChecklistId from the Due Diligence checklist (tab 7).
        /// </summary>
        public string ChecklistId { get; set; } = string.Empty;

        /// <summary>
        /// Kolom K: "Negatief resultaat acceptabel?".
        /// </summary>
        public bool NegativeOutcomeAcceptable { get; set; }

        /// <summary>
        /// Kolom M: "Afwijkingstekst / motivatie".
        /// </summary>
        public string? DeviationText { get; set; }
    }
}
