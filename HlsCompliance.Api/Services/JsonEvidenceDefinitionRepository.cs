using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HlsCompliance.Api.Domain;
using Microsoft.AspNetCore.Hosting;

namespace HlsCompliance.Api.Services
{
    public interface IEvidenceDefinitionRepository
    {
        IReadOnlyCollection<EvidenceDefinition> GetAll();
        EvidenceDefinition? GetById(string evidenceId);
    }

    /// <summary>
    /// Leest de bewijscatalogus uit Data\evidence-definitions.json (equivalent van tab 9).
    /// </summary>
    public class JsonEvidenceDefinitionRepository : IEvidenceDefinitionRepository
    {
        private readonly string _filePath;
        private readonly object _syncRoot = new();

        private List<EvidenceDefinition> _cache = new();

        public JsonEvidenceDefinitionRepository(IWebHostEnvironment env)
        {
            if (env == null) throw new ArgumentNullException(nameof(env));

            var dataDir = Path.Combine(env.ContentRootPath, "Data");
            Directory.CreateDirectory(dataDir);

            _filePath = Path.Combine(dataDir, "evidence-definitions.json");

            LoadFromDisk();
        }

        private void LoadFromDisk()
        {
            lock (_syncRoot)
            {
                if (!File.Exists(_filePath))
                {
                    _cache = new List<EvidenceDefinition>();
                    return;
                }

                var json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _cache = new List<EvidenceDefinition>();
                    return;
                }

                try
                {
                    var list = JsonSerializer.Deserialize<List<EvidenceDefinition>>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    _cache = list ?? new List<EvidenceDefinition>();
                }
                catch
                {
                    _cache = new List<EvidenceDefinition>();
                }
            }
        }

        public IReadOnlyCollection<EvidenceDefinition> GetAll()
        {
            lock (_syncRoot)
            {
                return _cache.ToList();
            }
        }

        public EvidenceDefinition? GetById(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
                return null;

            lock (_syncRoot)
            {
                return _cache.FirstOrDefault(x =>
                    x.EvidenceId.Equals(evidenceId, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
