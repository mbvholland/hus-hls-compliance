using System;

namespace HlsCompliance.Api.Domain
{
    /// <summary>
    /// Definitie van één controlevraag uit tab 7 (statisch deel).
    /// </summary>
    public class ChecklistQuestionDefinition
    {
        // Tab 7 kolom A
        public string ChecklistId { get; set; } = string.Empty;

        // Tab 7 kolom B: Categorie (Algemeen, AVG, NEN/ISO, etc.)
        public string Category { get; set; } = string.Empty;

        // Tab 7 kolom C: Controlevraag
        public string Question { get; set; } = string.Empty;

        // Tab 7 kolom D: Normverwijzing
        public string NormReference { get; set; } = string.Empty;

        // Tab 7 kolom E: Risico (Geen/Laag/Middel/Hoog/Zeer Hoog)
        public string RiskLevel { get; set; } = string.Empty;

        // Tab 7 kolommen N/O/P: markeren of de vraag relevant is voor SLA / VWO / SO
        public bool IsSlaRelevant { get; set; }
        public bool IsVwoRelevant { get; set; }
        public bool IsSoRelevant { get; set; }

        // Tab 7 kolommen U/V/W: standaardtekst voor SLA / VWO / SO
        public string? StandardTextSla { get; set; }
        public string? StandardTextVwo { get; set; }
        public string? StandardTextSo { get; set; }

        // Tab 7 kolom CL: ToetsIDs gescheiden door ;  (bv. "ALG-a;NENISO-a")
        public string[] ToetsIds { get; set; } = Array.Empty<string>();

        // Tab 7 kolom CM: Normdomeinen
        public string[] NenDomains { get; set; } = Array.Empty<string>();

        // Tab 7 kolom CN: Thema's
        public string[] Themes { get; set; } = Array.Empty<string>();
    }
}
