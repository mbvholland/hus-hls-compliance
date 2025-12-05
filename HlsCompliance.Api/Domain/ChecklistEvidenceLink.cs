using System;

namespace HlsCompliance.Api.Domain
{
    /// <summary>
    /// Koppeling tussen een ChecklistID (tab 7) en een BewijsID (tab 9/10).
    /// Spiegelt tab 10 "Bewijs_Hulp" in Excel.
    /// </summary>
    public class ChecklistEvidenceLink
    {
        /// <summary>
        /// ChecklistID uit tab 7/8 (bijv. "Algemeen1").
        /// </summary>
        public string ChecklistId { get; set; } = string.Empty;

        /// <summary>
        /// BewijsID uit tab 9 (bijv. "B001").
        /// </summary>
        public string EvidenceId { get; set; } = string.Empty;
    }
}
