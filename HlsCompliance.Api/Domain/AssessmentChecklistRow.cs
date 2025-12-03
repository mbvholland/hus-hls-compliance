using System;

namespace HlsCompliance.Api.Domain
{
    /// <summary>
    /// De per-assessment status van één controlevraag uit tab 7.
    /// Dit is conceptueel één rij in tab 7 voor een specifieke assessment.
    /// </summary>
    public class AssessmentChecklistRow
    {
        public Guid AssessmentId { get; set; }

        // Koppeling naar de definitie uit ChecklistQuestionDefinition
        public string ChecklistId { get; set; } = string.Empty;

        // Kolom F: Toepasselijk? (Ja/Nee) - in code als bool
        public bool IsApplicable { get; set; }

        // Kolom G: Antwoord (ruwe tekst uit tab 8)
        public string? Answer { get; set; }

        // Kolom I: ControlevraagResultaat (Goedgekeurd / Deels goedgekeurd / Afgekeurd / ...)
        public string? AnswerEvaluation { get; set; }

        // Kolom J: BewijsResultaat (Compleet / Onvoldoende / In beoordeling /
        // Niet aangeleverd / Geen bewijs vereist)
        public string? EvidenceSummary { get; set; }

        // Kolom K: Negatief resultaat acceptabel? (Ja/Nee -> bool)
        public bool NegativeOutcomeAcceptable { get; set; }

        // Kolom L: Resultaat due diligence (OK / Nog te beoordelen / Afwijking acceptabel / Niet acceptabel)
        public string? DueDiligenceOutcome { get; set; }

        // Kolom M: Afwijkingstekst (contract)
        public string? DeviationText { get; set; }
    }
}
