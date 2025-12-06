using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Simple JSON-backed repository for Due Diligence decisions (kolom K/M) per assessment.
    /// Data file: Data/due-diligence-decisions.json
    /// </summary>
    public class JsonDueDiligenceDecisionRepository
    {
        private const string DecisionsFileName = "Data/due-diligence-decisions.json";

        private readonly List<AssessmentDueDiligenceDecision> _items = new();
        private readonly object _syncRoot = new();

        public JsonDueDiligenceDecisionRepository()
        {
            LoadFromDisk();
        }

        /// <summary>
        /// Get all decisions for a given assessment.
        /// </summary>
        public IReadOnlyList<AssessmentDueDiligenceDecision> GetByAssessment(Guid assessmentId)
        {
            lock (_syncRoot)
            {
                return _items
                    .Where(d => d.AssessmentId == assessmentId)
                    .Select(d => Clone(d))
                    .ToList();
            }
        }

        /// <summary>
        /// Upsert one decision (per AssessmentId + ChecklistId).
        /// If it exists, update; otherwise add a new one.
        /// </summary>
        public void Upsert(Guid assessmentId, string checklistId, bool negativeOutcomeAcceptable, string? deviationText)
        {
            if (string.IsNullOrWhiteSpace(checklistId))
            {
                throw new ArgumentException("ChecklistId is required.", nameof(checklistId));
            }

            lock (_syncRoot)
            {
                var existing = _items.FirstOrDefault(d =>
                    d.AssessmentId == assessmentId &&
                    string.Equals(d.ChecklistId, checklistId, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    existing = new AssessmentDueDiligenceDecision
                    {
                        AssessmentId = assessmentId,
                        ChecklistId = checklistId
                    };
                    _items.Add(existing);
                }

                existing.NegativeOutcomeAcceptable = negativeOutcomeAcceptable;
                existing.DeviationText = deviationText;

                SaveToDisk();
            }
        }

        // -------------------------
        // JSON load / save
        // -------------------------

        private void LoadFromDisk()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, DecisionsFileName);

                if (!File.Exists(filePath))
                {
                    return; // start empty
                }

                var json = File.ReadAllText(filePath);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var items = JsonSerializer.Deserialize<List<AssessmentDueDiligenceDecision>>(json, options);
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
                // Ignore errors; start with empty list.
                // (Add logging here later if desired.)
            }
        }

        private void SaveToDisk()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, DecisionsFileName);

                List<AssessmentDueDiligenceDecision> snapshot;
                lock (_syncRoot)
                {
                    snapshot = _items
                        .Select(Clone)
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
                // Ignore errors; decisions remain in memory.
                // (Add logging later if needed.)
            }
        }

        private static AssessmentDueDiligenceDecision Clone(AssessmentDueDiligenceDecision d)
        {
            return new AssessmentDueDiligenceDecision
            {
                AssessmentId = d.AssessmentId,
                ChecklistId = d.ChecklistId,
                NegativeOutcomeAcceptable = d.NegativeOutcomeAcceptable,
                DeviationText = d.DeviationText
            };
        }
    }
}
