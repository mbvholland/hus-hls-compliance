using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace HlsevidenceHlsEvidenceExport
{
    // Matches EvidenceDefinition used by JsonEvidenceDefinitionRepository
    public class EvidenceDefinition
    {
        public string EvidenceId { get; set; } = string.Empty;
        public string EvidenceName { get; set; } = string.Empty;
    }

    // Matches ChecklistEvidenceLink used by JsonChecklistEvidenceLinkRepository
    public class ChecklistEvidenceLink
    {
        public string ChecklistId { get; set; } = string.Empty;
        public string EvidenceId { get; set; } = string.Empty;
        public string EvidenceText { get; set; } = string.Empty;
    }

    internal class Program
    {
        /// <summary>
        /// Usage:
        ///   dotnet run "C:\...\HlsevidenceHlsEvidenceExport" "C:\...\HlsCompliance.Api\Data"
        ///
        /// If you call without arguments, it assumes:
        ///   - CSVs next to the executable (9_Bewijs.csv, 10_Bewijs_Hulp.csv)
        ///   - output in ..\HlsCompliance.Api\Data
        /// </summary>
        static int Main(string[] args)
        {
            try
            {
                // where the CSV files live
                string csvDir = args.Length > 0
                    ? args[0]
                    : AppContext.BaseDirectory;

                // where the JSON files should be written
                string outputDir;
                if (args.Length > 1)
                {
                    outputDir = args[1];
                }
                else
                {
                    // ..\..\..\ -> project root, then HlsCompliance.Api\Data
                    var baseDir = AppContext.BaseDirectory;
                    var root = Directory.GetParent(baseDir)!.Parent!.Parent!.Parent!;
                    outputDir = Path.Combine(root.FullName, "HlsCompliance.Api", "Data");
                }

                string evidenceCsvPath = Path.Combine(csvDir, "9_Bewijs.csv");
                string linksCsvPath = Path.Combine(csvDir, "10_Bewijs_Hulp.csv");

                Console.WriteLine("CSV directory : " + csvDir);
                Console.WriteLine("Output (Data) : " + outputDir);
                Console.WriteLine("Evidence CSV  : " + evidenceCsvPath);
                Console.WriteLine("Links CSV     : " + linksCsvPath);
                Console.WriteLine();

                var evidenceDefinitions = ReadEvidenceDefinitions(evidenceCsvPath);
                var checklistLinks = ReadChecklistEvidenceLinks(linksCsvPath);

                Directory.CreateDirectory(outputDir);

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var evidenceJsonPath = Path.Combine(outputDir, "evidence-definitions.json");
                var linksJsonPath = Path.Combine(outputDir, "checklist-evidence-links.json");

                File.WriteAllText(
                    evidenceJsonPath,
                    JsonSerializer.Serialize(evidenceDefinitions, jsonOptions),
                    Encoding.UTF8);

                File.WriteAllText(
                    linksJsonPath,
                    JsonSerializer.Serialize(checklistLinks, jsonOptions),
                    Encoding.UTF8);

                Console.WriteLine($"Written {evidenceDefinitions.Count} evidence definitions to:");
                Console.WriteLine("  " + evidenceJsonPath);
                Console.WriteLine();
                Console.WriteLine($"Written {checklistLinks.Count} checklist-evidence links to:");
                Console.WriteLine("  " + linksJsonPath);

                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR while generating JSON:");
                Console.WriteLine(ex);
                Console.ResetColor();
                return 1;
            }
        }

        private static List<EvidenceDefinition> ReadEvidenceDefinitions(string csvPath)
        {
            if (!File.Exists(csvPath))
                throw new FileNotFoundException("Evidence CSV not found", csvPath);

            var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
            if (lines.Length <= 1) return new List<EvidenceDefinition>();

            // header: BewijsID;BewijsNaam
            var result = new List<EvidenceDefinition>();

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cells = SplitCsvLine(line);
                if (cells.Length < 2) continue;

                var id = cells[0].Trim();
                var name = cells[1].Trim();

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                    continue;

                result.Add(new EvidenceDefinition
                {
                    EvidenceId = id,
                    EvidenceName = name
                });
            }

            return result;
        }

        private static List<ChecklistEvidenceLink> ReadChecklistEvidenceLinks(string csvPath)
        {
            if (!File.Exists(csvPath))
                throw new FileNotFoundException("Checklist-evidence CSV not found", csvPath);

            var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
            if (lines.Length <= 1) return new List<ChecklistEvidenceLink>();

            // header: Checklist;BewijsTek;BewijsID
            var result = new List<ChecklistEvidenceLink>();

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cells = SplitCsvLine(line);
                if (cells.Length < 3) continue;

                var checklistId = cells[0].Trim();
                var evidenceText = cells[1].Trim();
                var evidenceId = cells[2].Trim();

                if (string.IsNullOrWhiteSpace(checklistId) || string.IsNullOrWhiteSpace(evidenceId))
                    continue;

                result.Add(new ChecklistEvidenceLink
                {
                    ChecklistId = checklistId,
                    EvidenceId = evidenceId,
                    EvidenceText = evidenceText
                });
            }

            return result;
        }

        /// <summary>
        /// Very simple CSV splitter:
        /// - Tries ';' first (Dutch Excel), then ','.
        /// - Strips quotes and whitespace.
        /// </summary>
        private static string[] SplitCsvLine(string line)
        {
            var cells = line.Split(';');
            if (cells.Length == 1)
            {
                cells = line.Split(',');
            }

            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = cells[i].Trim().Trim('"');
            }

            return cells;
        }
    }
}
