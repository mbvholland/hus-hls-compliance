using System;
using System.Collections.Generic;

namespace HlsCompliance.Api.Domain
{
    public class ToetsVooronderzoekQuestion
    {
        /// <summary>
        /// ToetsID uit kolom A van tab 6 (bijv. "ALG-a", "AVG-a", "NENISO-a", "NIS2-a", "AIAct-a", "MDR-a", "ISO13485-a", "CRA-a").
        /// </summary>
        public string ToetsId { get; set; } = default!;

        /// <summary>
        /// Vraagtekst uit kolom B.
        /// </summary>
        public string Text { get; set; } = default!;

        /// <summary>
        /// Antwoord J/N (true = Ja, false = Nee, null = (nog) onbekend).
        /// </summary>
        public bool? Answer { get; set; }

        /// <summary>
        /// True als deze vraag afgeleid wordt uit andere blokken (DPIA, MDR, AI Act, Securityprofiel, Koppelingen, etc.).
        /// False als de vraag handmatig ingevuld wordt (bijvoorbeeld LHV-acceptatie).
        /// </summary>
        public bool IsDerived { get; set; }

        /// <summary>
        /// Optionele bronverwijzing (bijv. "DPIA_Quickscan", "MDR", "AI Act", "Securityprofiel", "Koppelingen").
        /// Alleen documentair; heeft geen functionele impact.
        /// </summary>
        public string? DerivedFrom { get; set; }

        /// <summary>
        /// Optionele toelichting / uitleg (bijv. samenvatting van Excel-formule).
        /// </summary>
        public string? Explanation { get; set; }
    }

    public class ToetsVooronderzoekResult
    {
        public Guid AssessmentId { get; set; }

        /// <summary>
        /// Alle ToetsVooronderzoek-vragen (AVG, NEN/ISO, NIS2, AI Act, MDR, ISO13485, CRA, ALG, ...).
        /// </summary>
        public List<ToetsVooronderzoekQuestion> Questions { get; set; } = new();

        /// <summary>
        /// Samenvattende “Toepasselijk?”-velden uit kolom E (tab 6).
        /// </summary>
        public bool? DpiaApplicable { get; set; }
        public bool? NenIsoApplicable { get; set; }
        public bool? Nis2Applicable { get; set; }

        /// <summary>
        /// Risicoklasse AI Act (spiegelt kolom E “Risicoklasse AI Act?” / Assessment.AiActRiskLevel).
        /// </summary>
        public string? AiActRiskLevel { get; set; }

        /// <summary>
        /// Risicoklasse MDR (spiegelt kolom E “Risicoklasse MDR?” / Assessment.MdrClass).
        /// </summary>
        public string? MdrRiskClass { get; set; }

        public bool? Iso13485Applicable { get; set; }
        public bool? CraApplicable { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}
