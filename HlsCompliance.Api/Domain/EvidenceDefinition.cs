using System;

namespace HlsCompliance.Api.Domain
{
    /// <summary>
    /// Definitie van één bewijstype (spiegelt tab 9 "Bewijs" in Excel).
    /// </summary>
    public class EvidenceDefinition
    {
            /// <summary>
            /// BewijsID (bijv. "B001"). Moet uniek en stabiel zijn per HLS-versie.
            /// </summary>
            public string EvidenceId { get; set; } = string.Empty;

            /// <summary>
            /// Naam / omschrijving van het bewijstype (bijv. "DPIA-rapport").
            /// </summary>
            public string Name { get; set; } = string.Empty;
    }
}
