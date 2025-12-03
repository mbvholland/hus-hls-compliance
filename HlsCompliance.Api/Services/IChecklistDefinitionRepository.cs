using System.Collections.Generic;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Repository voor de statische checklist-definities uit tab 7.
    /// In deze stap is dit nog een simpele interface; de echte JSON-implementatie komt later.
    /// </summary>
    public interface IChecklistDefinitionRepository
    {
        /// <summary>
        /// Haalt alle checklist-definities op (tab 7, kolommen A–E, N/O/P, U/V/W, CL/CM/CN).
        /// </summary>
        IReadOnlyList<ChecklistQuestionDefinition> GetAll();
    }
}
