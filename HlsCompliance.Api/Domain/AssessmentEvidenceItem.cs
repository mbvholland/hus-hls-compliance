using System;

namespace HlsCompliance.Api.Domain
{
    /// <summary>
    /// Eén bewijslast-item (BewijsID) voor een specifieke assessment en ChecklistID.
    /// Spiegelt de structuur van tab 11 in de Excel.
    /// </summary>
    public class AssessmentEvidenceItem
    {
        public Guid AssessmentId { get; set; }

        // Koppeling naar de vraag (ChecklistID uit tab 7/8/11)
        public string ChecklistId { get; set; } = string.Empty;

        // BewijsID (zoals in tab 10/11)
        public string EvidenceId { get; set; } = string.Empty;

        // Bewijsnaam / beschrijving (BewijsTekst)
        public string? EvidenceName { get; set; }

        /// <summary>
        /// Status van het bewijslast-item:
        /// "Goedgekeurd", "In beoordeling", "Niet aangeleverd", "Afgekeurd", ...
        /// </summary>
        public string? Status { get; set; }

        // Optionele toelichting
        public string? Comment { get; set; }
    }
}
