using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    public interface IAssessmentEvidenceRepository
    {
        IEnumerable<AssessmentEvidenceItem> GetByAssessment(Guid assessmentId);
        void UpsertEvidence(Guid assessmentId, IEnumerable<AssessmentEvidenceItem> items);
    }

    /// <summary>
    /// JSON-gebaseerde opslag van bewijslast-items (tab 11).
    /// Alles wordt bewaard in Data/evidence.json.
    /// </summary>
    public class JsonAssessmentEvidenceRepository : IAssessmentEvidenceRepository
    {
        private const string FileName = "Data/evidence.json";

        private readonly object _syncRoot = new();
        private readonly List<AssessmentEvidenceItem> _items = new();

        public JsonAssessmentEvidenceRepository()
        {
            LoadFromDisk();
        }

        public IEnumerable<AssessmentEvidenceItem> GetByAssessment(Guid assessmentId)
        {
            lock (_syncRoot)
            {
                return _items
                    .Where(e => e.AssessmentId == assessmentId)
                    .Select(e => e)
                    .ToList();
            }
        }

        /// <summary>
        /// Upsert per (AssessmentId + ChecklistId + EvidenceId).
        /// Bestaat er al een item met dezelfde sleutel, dan wordt die vervangen.
        /// </summary>
        public void UpsertEvidence(Guid assessmentId, IEnumerable<AssessmentEvidenceItem> items)
        {
            if (items == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                var incoming = items
                    .Where(i => !string.IsNullOrWhiteSpace(i.ChecklistId) &&
                                !string.IsNullOrWhiteSpace(i.EvidenceId))
                    .ToList();

                if (!incoming.Any())
                {
                    return;
                }

                foreach (var inc in incoming)
                {
                    // Verwijder bestaande item met zelfde sleutel
                    _items.RemoveAll(e =>
                        e.AssessmentId == assessmentId &&
                        e.ChecklistId.Equals(inc.ChecklistId, StringComparison.OrdinalIgnoreCase) &&
                        e.EvidenceId.Equals(inc.EvidenceId, StringComparison.OrdinalIgnoreCase));

                    _items.Add(inc);
                }

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

                var items = JsonSerializer.Deserialize<List<AssessmentEvidenceItem>>(json, options);
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
                // Fouten negeren -> lege lijst.
            }
        }

        private void SaveToDisk()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, FileName);

                List<AssessmentEvidenceItem> snapshot;
                lock (_syncRoot)
                {
                    snapshot = _items
                        .Select(e => e)
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
                // Fouten negeren; items blijven in memory beschikbaar.
            }
        }
    }
}
