using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    public interface IAssessmentAnswersRepository
    {
        IEnumerable<AssessmentQuestionAnswer> GetByAssessment(Guid assessmentId);
        void UpsertAnswers(Guid assessmentId, IEnumerable<AssessmentQuestionAnswer> answers);
    }

    /// <summary>
    /// JSON-gebaseerde opslag van antwoorden op controlevragen (tab 8).
    /// Alles wordt bewaard in Data/answers.json.
    /// </summary>
    public class JsonAssessmentAnswersRepository : IAssessmentAnswersRepository
    {
        private const string FileName = "Data/answers.json";

        private readonly object _syncRoot = new();
        private readonly List<AssessmentQuestionAnswer> _items = new();

        public JsonAssessmentAnswersRepository()
        {
            LoadFromDisk();
        }

        public IEnumerable<AssessmentQuestionAnswer> GetByAssessment(Guid assessmentId)
        {
            lock (_syncRoot)
            {
                return _items
                    .Where(a => a.AssessmentId == assessmentId)
                    .Select(a => a)
                    .ToList();
            }
        }

        /// <summary>
        /// Upsert per (AssessmentId + ChecklistId).
        /// Voor elke aangeleverde answer wordt een bestaande answer met dezelfde
        /// AssessmentId + ChecklistId vervangen.
        /// </summary>
        public void UpsertAnswers(Guid assessmentId, IEnumerable<AssessmentQuestionAnswer> answers)
        {
            if (answers == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                var incoming = answers
                    .Where(a => !string.IsNullOrWhiteSpace(a.ChecklistId))
                    .ToList();

                if (!incoming.Any())
                {
                    return;
                }

                // Verwijder bestaande items voor deze assessment + checklistIds uit incoming
                var checklistIds = incoming
                    .Select(a => a.ChecklistId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _items.RemoveAll(a =>
                    a.AssessmentId == assessmentId &&
                    checklistIds.Contains(a.ChecklistId, StringComparer.OrdinalIgnoreCase));

                // Voeg nieuwe items toe
                _items.AddRange(incoming);

                SaveToDisk();
            }
        }

        private void LoadFromDisk()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, FileName);

                if (!File.Exists(filePath))
                {
                    return;
                }

                var json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var items = JsonSerializer.Deserialize<List<AssessmentQuestionAnswer>>(json, options);
                if (items == null)
                {
                    return;
                }

                lock (_syncRoot)
                {
                    _items.Clear();
                    _items.AddRange(items);
                }
            }
            catch
            {
                // Fouten negeren: we starten dan met een lege lijst.
            }
        }

        private void SaveToDisk()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, FileName);

                List<AssessmentQuestionAnswer> snapshot;
                lock (_syncRoot)
                {
                    snapshot = _items
                        .Select(a => a)
                        .ToList();
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(snapshot, options);

                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Fouten bij schrijven negeren; items blijven in memory.
            }
        }
    }
}
