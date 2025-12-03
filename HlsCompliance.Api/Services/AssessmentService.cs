using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    public class AssessmentService
    {
        // Interne opslag van alle assessments. Deze wordt geladen/geschreven vanuit/naar JSON.
        private readonly List<Assessment> _assessments = new();

        // Bestandsnaam voor persistente opslag (relatief t.o.v. HlsCompliance.Api)
        private const string AssessmentsFileName = "Data/assessments.json";

        // Lock-object voor thread safety bij lezen/schrijven
        private readonly object _syncRoot = new();

        public AssessmentService()
        {
            LoadFromDisk();
        }

        /// <summary>
        /// Haal alle assessments op.
        /// </summary>
        public IEnumerable<Assessment> GetAll()
        {
            lock (_syncRoot)
            {
                // We geven een kopie terug zodat aanroepers de interne lijst niet per ongeluk muteren.
                return _assessments
                    .Select(a => a)
                    .ToList();
            }
        }

        /// <summary>
        /// Haal één assessment op basis van Id.
        /// </summary>
        public Assessment? GetById(Guid id)
        {
            lock (_syncRoot)
            {
                return _assessments.FirstOrDefault(a => a.Id == id);
            }
        }

        /// <summary>
        /// Maak een nieuw assessment aan en sla het persistent op.
        /// </summary>
        public Assessment Create(string organisation, string supplier, string solution, string hlsVersion)
        {
            var assessment = new Assessment
            {
                Id = Guid.NewGuid(),
                Organisation = organisation,
                Supplier = supplier,
                Solution = solution,
                HlsVersion = hlsVersion,
                Phase1Status = "not_started",
                Phase2Status = "not_started",
                Phase3Status = "not_started",
                Phase4aStatus = "not_started",
                Phase4bStatus = "not_started",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };

            lock (_syncRoot)
            {
                _assessments.Add(assessment);
                SaveToDisk();
            }

            return assessment;
        }

        /// <summary>
        /// Update de status van een fase (fase 1–4a/4b) en schrijf de wijziging weg naar JSON.
        /// </summary>
        public bool UpdatePhaseStatus(Guid id, string phase, string status)
        {
            lock (_syncRoot)
            {
                var assessment = _assessments.FirstOrDefault(a => a.Id == id);
                if (assessment == null)
                {
                    return false;
                }

                switch (phase.ToLowerInvariant())
                {
                    case "phase1":
                        assessment.Phase1Status = status;
                        break;
                    case "phase2":
                        assessment.Phase2Status = status;
                        break;
                    case "phase3":
                        assessment.Phase3Status = status;
                        break;
                    case "phase4a":
                        assessment.Phase4aStatus = status;
                        break;
                    case "phase4b":
                        assessment.Phase4bStatus = status;
                        break;
                    default:
                        // Onbekende fase
                        return false;
                }

                assessment.UpdatedAt = DateTime.UtcNow;

                SaveToDisk();
                return true;
            }
        }

        // -------------------------
        // Persistente opslag (JSON)
        // -------------------------

        private void LoadFromDisk()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, AssessmentsFileName);

                if (!File.Exists(filePath))
                {
                    // Nog geen bestand -> lege lijst.
                    return;
                }

                var json = File.ReadAllText(filePath);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var items = JsonSerializer.Deserialize<List<Assessment>>(json, options);
                if (items == null)
                {
                    return;
                }

                lock (_syncRoot)
                {
                    _assessments.Clear();
                    _assessments.AddRange(items);
                }
            }
            catch
            {
                // Fouten bij lezen/parse negeren; we starten dan met een lege lijst.
                // Later kun je hier logging toevoegen.
            }
        }

        private void SaveToDisk()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, AssessmentsFileName);

                List<Assessment> snapshot;
                lock (_syncRoot)
                {
                    snapshot = _assessments
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
                // Fouten bij schrijven negeren; de assessments blijven in memory beschikbaar.
                // Later kun je hier logging toevoegen.
            }
        }
    }
}
