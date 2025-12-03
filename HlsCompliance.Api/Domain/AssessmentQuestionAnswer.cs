using System;

namespace HlsCompliance.Api.Domain
{
    /// <summary>
    /// Eén antwoord op een controlevraag (tab 8) voor een specifieke assessment.
    /// </summary>
    public class AssessmentQuestionAnswer
    {
        public Guid AssessmentId { get; set; }

        // ChecklistId komt overeen met tab 7/8 kolom A
        public string ChecklistId { get; set; } = string.Empty;

        // Tab 8 kolom F: Antwoord (inhoudelijk)
        public string? RawAnswer { get; set; }

        // Tab 8 kolom G: Antwoord OK?
        // "Goedgekeurd" / "Deels goedgekeurd" / "Afgekeurd" / "Nog niet goedgekeurd i.a.v. toelichting"
        public string? AnswerEvaluation { get; set; }
    }
}
