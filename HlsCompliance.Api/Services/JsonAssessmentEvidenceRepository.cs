using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// JSON-gebaseerde opslag voor bewijslast (tab 11) per assessment.
    /// Schrijft naar Data/assessment_evidence.json onder de app-folder.
    /// </summary>
    public class JsonAssessmentEvidenceRepository : IAssessmentEvidenceRepository
    {
        private readonly string _filePath;
        private readonly object _lock = new();
        private List<AssessmentEvidenceItem> _items = new();

        public JsonAssessmentEvidenceRepository()
        {
            var basePath = AppContext.BaseDirectory;
            var dataDir = Path.Combine(basePath, "Data");

            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            _filePath = Path.Combine(dataDir, "assessment_evidence.json");
            Load();
        }

        private void Load()
        {
            if (!File.Exists(_filePath))
            {
                _items = new List<AssessmentEvidenceItem>();
                return;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<List<AssessmentEvidenceItem>>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                _items = loaded ?? new List<AssessmentEvidenceItem>();
            }
            catch
            {
                // Bij fout: leeg starten, zodat de app gewoon blijft draaien
                _items = new List<AssessmentEvidenceItem>();
            }
        }

        private void Save()
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(
                    _items,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(_filePath, json);
            }
        }

        /// <summary>
        /// Haal alle evidence-items voor één assessment op.
        /// </summary>
        public IReadOnlyCollection<AssessmentEvidenceItem> GetByAssessment(Guid assessmentId)
        {
            lock (_lock)
            {
                return _items
                    .Where(e => e.AssessmentId == assessmentId)
                    .ToList();
            }
        }

        /// <summary>
        /// Voeg toe of update op basis van (AssessmentId + ChecklistId + EvidenceId).
        /// </summary>
        public void UpsertEvidence(AssessmentEvidenceItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            lock (_lock)
            {
                var existing = _items.FirstOrDefault(e =>
                    e.AssessmentId == item.AssessmentId &&
                    string.Equals(e.ChecklistId, item.ChecklistId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(e.EvidenceId, item.EvidenceId, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    _items.Add(item);
                }
                else
                {
                    existing.EvidenceName = item.EvidenceName;
                    existing.Status = item.Status;
                    existing.Comment = item.Comment;
                }

                Save();
            }
        }
    }
}
