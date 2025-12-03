using System.Collections.Generic;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Tijdelijke, lege in-memory implementatie van IChecklistDefinitionRepository.
    /// Wordt later vervangen door een JSON-gebaseerde implementatie.
    /// </summary>
    public class InMemoryChecklistDefinitionRepository : IChecklistDefinitionRepository
    {
        private readonly List<ChecklistQuestionDefinition> _definitions = new();

        public IReadOnlyList<ChecklistQuestionDefinition> GetAll() => _definitions;
    }
}
