using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HlsCompliance.Api.Domain;
using Microsoft.AspNetCore.Hosting;

namespace HlsCompliance.Api.Services
{
    public interface IChecklistEvidenceLinkRepository
    {
        IReadOnlyCollection<ChecklistEvidenceLink> GetAll();
        IReadOnlyCollection<ChecklistEvidenceLink> GetByChecklistId(string checklistId);
    }

    /// <summary>
    /// Leest de mapping ChecklistID ↔ BewijsID uit Data\checklist-evidence-links.json (equivalent van tab 10).
    /// </summary>
    public class JsonChecklistEvidenceLinkRepository : IChecklistEvidenceLinkRepository
    {
        private readonly string _filePath;
        private readonly object _syncRoot = new();

        private List<ChecklistEvidenceLink> _cache = new();

        public JsonChecklistEvidenceLinkRepository(IWebHostEnvironment env)
        {
            if (env == null) throw new ArgumentNullException(nameof(env));

            var dataDir = Path.Combine(env.ContentRootPath, "Data");
            Directory.CreateDirectory(dataDir);

            _filePath = Path.Combine(dataDir, "checklist-evidence-links.json");

            LoadFromDisk();
        }

        private void LoadFromDisk()
        {
            lock (_syncRoot)
            {
                if (!File.Exists(_filePath))
                {
                    _cache = new List<ChecklistEvidenceLink>();
                    return;
                }

                var json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _cache = new List<ChecklistEvidenceLink>();
                    return;
                }

                try
                {
                    var list = JsonSerializer.Deserialize<List<ChecklistEvidenceLink>>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    _cache = list ?? new List<ChecklistEvidenceLink>();
                }
                catch
                {
                    _cache = new List<ChecklistEvidenceLink>();
                }
            }
        }

        public IReadOnlyCollection<ChecklistEvidenceLink> GetAll()
        {
            lock (_syncRoot)
            {
                return _cache.ToList();
            }
        }

        public IReadOnlyCollection<ChecklistEvidenceLink> GetByChecklistId(string checklistId)
        {
            if (string.IsNullOrWhiteSpace(checklistId))
                return Array.Empty<ChecklistEvidenceLink>();

            lock (_syncRoot)
            {
                return _cache
                    .Where(x => x.ChecklistId.Equals(checklistId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }
    }
}
