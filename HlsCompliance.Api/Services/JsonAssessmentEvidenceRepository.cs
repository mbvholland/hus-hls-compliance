using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HlsCompliance.Api.Domain;
using Microsoft.AspNetCore.Hosting;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Repository voor bewijslast per assessment (tab 11-concept).
    /// Slaat <see cref="AssessmentEvidenceItem"/> op in een JSON-bestand in de Data-map.
    /// </summary>
    public interface IAssessmentEvidenceRepository
    {
        /// <summary>
        /// Haal alle bewijslast-items op voor één assessment.
        /// </summary>
        IReadOnlyCollection<AssessmentEvidenceItem> GetByAssessment(Guid assessmentId);

        /// <summary>
        /// Vervang alle bewijslast-items voor één assessment door de opgegeven set.
        /// </summary>
        void Upsert(Guid assessmentId, IEnumerable<AssessmentEvidenceItem> items);
    }

    public class JsonAssessmentEvidenceRepository : IAssessmentEvidenceRepository
    {
        private readonly string _filePath;
        private readonly object _syncRoot = new();

        // Centrale opslag in memory, wordt bij start geladen uit JSON
        private List<AssessmentEvidenceItem> _storage = new();

        public JsonAssessmentEvidenceRepository(IWebHostEnvironment env)
        {
            if (env == null) throw new ArgumentNullException(nameof(env));

            var dataDir = Path.Combine(env.ContentRootPath, "Data");
            Directory.CreateDirectory(dataDir);

            _filePath = Path.Combine(dataDir, "assessment-evidence.json");

            LoadFromDisk();
        }

        private void LoadFromDisk()
        {
            lock (_syncRoot)
            {
                if (!File.Exists(_filePath))
                {
                    _storage = new List<AssessmentEvidenceItem>();
                    return;
                }

                var json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _storage = new List<AssessmentEvidenceItem>();
                    return;
                }

                try
                {
                    var items = JsonSerializer.Deserialize<List<AssessmentEvidenceItem>>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    _storage = items ?? new List<AssessmentEvidenceItem>();
                }
                catch
                {
                    // If parsing fails, start with empty storage (loggen kan later nog)
                    _storage = new List<AssessmentEvidenceItem>();
                }
            }
        }

        private void SaveToDisk()
        {
            lock (_syncRoot)
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(_storage, options);
                File.WriteAllText(_filePath, json);
            }
        }

        public IReadOnlyCollection<AssessmentEvidenceItem> GetByAssessment(Guid assessmentId)
        {
            lock (_syncRoot)
            {
                // Clone zodat de caller de interne lijst niet kan muteren
                return _storage
                    .Where(x => x.AssessmentId == assessmentId)
                    .Select(x => new AssessmentEvidenceItem
                    {
                        AssessmentId = x.AssessmentId,
                        ChecklistId = x.ChecklistId,
                        EvidenceId = x.EvidenceId,
                        EvidenceName = x.EvidenceName,
                        Status = x.Status,
                        Comment = x.Comment
                    })
                    .ToList();
            }
        }

        public void Upsert(Guid assessmentId, IEnumerable<AssessmentEvidenceItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            lock (_syncRoot)
            {
                // Verwijder alle bestaande items voor dit assessment
                _storage.RemoveAll(x => x.AssessmentId == assessmentId);

                // Voeg nieuwe items toe (genormaliseerd)
                foreach (var item in items)
                {
                    if (item == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(item.ChecklistId) ||
                        string.IsNullOrWhiteSpace(item.EvidenceId))
                    {
                        // Zonder ChecklistId en EvidenceId kunnen we er niets mee
                        continue;
                    }

                    var clone = new AssessmentEvidenceItem
                    {
                        AssessmentId = assessmentId,
                        ChecklistId = item.ChecklistId.Trim(),
                        EvidenceId = item.EvidenceId.Trim(),
                        EvidenceName = string.IsNullOrWhiteSpace(item.EvidenceName)
                            ? null
                            : item.EvidenceName.Trim(),
                        Status = string.IsNullOrWhiteSpace(item.Status)
                            ? null
                            : item.Status.Trim(),
                        Comment = string.IsNullOrWhiteSpace(item.Comment)
                            ? null
                            : item.Comment.Trim()
                    };

                    _storage.Add(clone);
                }

                SaveToDisk();
            }
        }
    }
}
