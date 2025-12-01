using System;

namespace HlsCompliance.Api.Domain
{
    /// <summary>
    /// View van tab 0. Algemeen voor een assessment.
    /// </summary>
    public class AlgemeenInfoResult
    {
        public Guid AssessmentId { get; set; }

        // Meta / contractgegevens (kolom B uit tab 0)
        public string? Leverancier { get; set; }          // B2
        public string? Applicatie { get; set; }           // B3
        public string? ContractStatus { get; set; }       // B4: "Lopend" / "Nieuw"
        public DateTime? ContractDate { get; set; }       // B5
        public DateTime? RenewalDate { get; set; }        // B6
        public DateTime? DueDiligenceDate { get; set; }   // B7
        public string? Versie { get; set; }               // B8

        // Overkoepelende risico-samenvatting (C10/C11/B10)
        public double? TotalRiskScore { get; set; }       // C10
        public int? RiskClass { get; set; }               // C11
        public string? RiskLabel { get; set; }            // B10 ("Geen", "Laag", ...)

        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Request-body voor het bijwerken van tab 0. Algemeen.
    /// Alleen de meta/contractgegevens; de risico-velden worden automatisch berekend.
    /// </summary>
    public class AlgemeenUpdateRequest
    {
        public string? Leverancier { get; set; }
        public string? Applicatie { get; set; }
        public string? ContractStatus { get; set; }
        public DateTime? ContractDate { get; set; }
        public DateTime? RenewalDate { get; set; }
        public DateTime? DueDiligenceDate { get; set; }
        public string? Versie { get; set; }
    }
}
