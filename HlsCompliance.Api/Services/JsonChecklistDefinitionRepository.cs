using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    public class JsonChecklistDefinitionRepository : IChecklistDefinitionRepository
    {
        private readonly List<ChecklistQuestionDefinition> _definitions = new();

        private const string DefaultFileName = "Data/checklist-v1.7.json";

        public JsonChecklistDefinitionRepository()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, DefaultFileName);

                if (!File.Exists(filePath))
                {
                    return;
                }

                var json = File.ReadAllText(filePath);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var items = JsonSerializer.Deserialize<List<ChecklistQuestionDefinition>>(json, options);
                if (items != null)
                {
                    _definitions.AddRange(items);
                }
            }
            catch (Exception)
            {
                // Later eventueel logging toevoegen.
            }
        }

        public IReadOnlyList<ChecklistQuestionDefinition> GetAll() => _definitions;
    }
}
