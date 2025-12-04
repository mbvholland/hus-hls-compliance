using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    public class MdrService
    {
        private const string FileName = "Data/mdr.json";

        // In-memory opslag per assessment
        private readonly Dictionary<Guid, MdrClassificationState> _storage = new();
        private readonly object _syncRoot = new();
        private readonly DpiaQuickscanService _dpiaQuickscanService;

        public MdrService(DpiaQuickscanService dpiaQuickscanService)
        {
            _dpiaQuickscanService = dpiaQuickscanService ?? throw new ArgumentNullException(nameof(dpiaQuickscanService));
            LoadFromDisk();
        }

        public MdrClassificationState GetOrCreateForAssessment(Guid assessmentId)
        {
            lock (_syncRoot)
            {
                if (!_storage.TryGetValue(assessmentId, out var state))
                {
                    state = new MdrClassificationState
                    {
                        AssessmentId = assessmentId,
                        MdrClass = "Onbekend",
                        Explanation = "Nog geen MDR-criteria ingevuld.",
                        IsComplete = false,
                        LastUpdated = DateTimeOffset.UtcNow
                    };

                    _storage[assessmentId] = state;
                    SaveToDisk();
                }

                // Criteria A–D altijd opnieuw afleiden uit de actuele DPIA-quickscan
                ApplyDpiaInputs(assessmentId, state);
                Recalculate(state);

                _storage[assessmentId] = state;
                SaveToDisk();

                return state;
            }
        }

        /// <summary>
        /// Update alleen de ernst van de schade bij fout (E2).
        /// De rest wordt afgeleid uit de DPIA-quickscan.
        /// </summary>
        public MdrClassificationState UpdateSeverity(Guid assessmentId, string? severity)
        {
            var state = GetOrCreateForAssessment(assessmentId);

            state.ErnstSchadeBijFout = string.IsNullOrWhiteSpace(severity)
                ? null
                : severity.Trim();

            Recalculate(state);

            lock (_syncRoot)
            {
                _storage[assessmentId] = state;
                SaveToDisk();
            }

            return state;
        }

        /// <summary>
        /// Leest de relevante DPIA-quickscan antwoorden in en mapt deze naar de MDR-criteria.
        /// A2 <- Q6 (Medisch doel)
        /// C2/D2 <- Q11 (Klinische interpretatie / ondersteunt klinische beslissing)
        /// </summary>
        private void ApplyDpiaInputs(Guid assessmentId, MdrClassificationState state)
        {
            var dpia = _dpiaQuickscanService.GetOrCreateForAssessment(assessmentId);

            string? GetAnswer(string code) =>
                dpia.Questions
                    .FirstOrDefault(q =>
                        string.Equals(q.Code, code, StringComparison.OrdinalIgnoreCase))
                    ?.Answer;

            // A2 <- DPIA Q6
            var medischDoelAnswer = GetAnswer("Q6");
            state.MedischDoel = NormalizeJaNee(medischDoelAnswer);

            if (string.Equals(state.MedischDoel, "Nee", StringComparison.OrdinalIgnoreCase))
            {
                // Als er géén medisch doel is:
                // C2 en D2 automatisch "Nee" (zoals je Excel-logica).
                state.KlinischeInterpretatie = "Nee";
                state.OndersteuntKlinischeBeslissing = "Nee";
            }
            else
            {
                // C2/D2 <- DPIA Q11
                var klinischeInterpretatieAnswer = GetAnswer("Q11");
                var normalized = NormalizeJaNee(klinischeInterpretatieAnswer);

                state.KlinischeInterpretatie = normalized;
                state.OndersteuntKlinischeBeslissing = normalized;
            }

            state.LastUpdated = DateTimeOffset.UtcNow;
        }

        private static string? NormalizeJaNee(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var v = value.Trim().ToLowerInvariant();
            if (v == "ja") return "Ja";
            if (v == "nee") return "Nee";
            return null;
        }

        private static string? NormalizeSeverity(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var v = value.Trim().ToLowerInvariant();
            return v switch
            {
                "dodelijk_of_onherstelbaar" => "dodelijk_of_onherstelbaar",
                "ernstig" => "ernstig",
                "niet_ernstig" => "niet_ernstig",
                "geen" => "Geen",
                _ => null
            };
        }

        /// <summary>
        /// Spiegelt de logica van F2 in tab '3. MDR Beslisboom'.
        /// </summary>
        private void Recalculate(MdrClassificationState state)
        {
            // A2..E2-equivalenten opbouwen
            var a = state.MedischDoel;                             // A2
            var b = state.AlleenAdministratiefOfGeneriek;          // B2 (afgeleid uit MedischDoel)
            var c = state.KlinischeInterpretatie;                  // C2
            var d = state.OndersteuntKlinischeBeslissing;          // D2
            var e = NormalizeSeverity(state.ErnstSchadeBijFout);   // E2 (ernst, niet Ja/Nee)

            // Tel alleen Ja/Nee voor A–D
            var yesNoInputs = new[] { a, b, c, d };
            int yesNoCount = yesNoInputs.Count(v =>
                string.Equals(v, "Ja", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(v, "Nee", StringComparison.OrdinalIgnoreCase));

            var allInputs = new string?[] { a, b, c, d, e };
            bool anyEmpty = allInputs.Any(v => v is null);

            // 1) Helemaal niets ingevuld -> Onbekend
            if (yesNoCount == 0 && e is null)
            {
                state.MdrClass = "Onbekend";
                state.Explanation =
                    "Nog geen MDR-criteria ingevuld (DPIA-quickscan en ernst van de schade zijn leeg).";
                state.IsComplete = false;
                state.LastUpdated = DateTimeOffset.UtcNow;
                return;
            }

            // 2) Speciaal: Medisch doel = Nee -> direct Geen medisch hulpmiddel
            //    E2 moet in dit geval automatisch "Geen" zijn.
            if (string.Equals(a, "Nee", StringComparison.OrdinalIgnoreCase))
            {
                state.ErnstSchadeBijFout = "Geen";
                state.MdrClass = "Geen medisch hulpmiddel";
                state.Explanation =
                    "Medisch doel is 'Nee' (afgeleid uit DPIA); volgens MDR is dit geen medisch hulpmiddel.";
                state.IsComplete = true;
                state.LastUpdated = DateTimeOffset.UtcNow;
                return;
            }

            // 3) Speciaal: Medisch doel = Ja, C en D beide "Nee" -> Klasse I,
            //    en E2 automatisch "Geen" (zoals in de Excel-beslislogica).
            if (string.Equals(a, "Ja", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c, "Nee", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(d, "Nee", StringComparison.OrdinalIgnoreCase))
            {
                state.ErnstSchadeBijFout = "Geen";
                state.MdrClass = "Klasse I";
                state.Explanation =
                    "Wel medisch doel, maar geen klinische interpretatie en geen klinische beslissingsondersteuning; daarmee MDR klasse I. Ernst van schade is automatisch 'Geen'.";
                state.IsComplete = true;
                state.LastUpdated = DateTimeOffset.UtcNow;
                return;
            }

            // 4) Als we wél Ja/Nee hebben, maar nog lege velden (bijv. ernst niet ingevuld)
            //    en we zitten niet in een van de speciale gevallen hierboven:
            if (anyEmpty)
            {
                state.MdrClass = "Onbekend";
                state.Explanation =
                    "Vul alle antwoorden in (ook in DPIA-quickscan en ernst van de schade) voor een MDR-classificatieresultaat.";
                state.IsComplete = false;
                state.LastUpdated = DateTimeOffset.UtcNow;
                return;
            }

            // Vanaf hier weten we:
            // - MedischDoel is NIET Nee (dus Ja)
            // - We zitten niet in de 'allebei Nee'-case (C/D)
            // - B, C, D en E zijn allemaal gevuld
            // - We kunnen de Excel F2-logica verder volgen

            state.IsComplete = true;

            // Extra robuustheid: als B toch "Ja" is, is het alsnog geen medisch hulpmiddel.
            if (string.Equals(b, "Ja", StringComparison.OrdinalIgnoreCase))
            {
                state.MdrClass = "Geen medisch hulpmiddel";
                state.Explanation =
                    "De software is alleen administratief/generieke communicatie; volgens MDR is dit geen medisch hulpmiddel.";
            }
            else if (!string.Equals(c, "Ja", StringComparison.OrdinalIgnoreCase))
            {
                state.MdrClass = "Klasse I";
                state.Explanation =
                    "Wel medisch doel, maar geen klinische interpretatie; daarmee maximaal MDR klasse I.";
            }
            else if (!string.Equals(d, "Ja", StringComparison.OrdinalIgnoreCase))
            {
                state.MdrClass = "Klasse I";
                state.Explanation =
                    "Wel klinische interpretatie, maar de software ondersteunt geen klinische beslissing; daarmee maximaal MDR klasse I.";
            }
            else
            {
                // Nu bepaalt de ernst van de schade (E2) de klasse
                switch (e)
                {
                    case "dodelijk_of_onherstelbaar":
                        state.MdrClass = "Klasse III";
                        state.Explanation =
                            "Bij falen kan dodelijke of onherstelbare schade optreden; MDR klasse III is van toepassing.";
                        break;

                    case "ernstig":
                        state.MdrClass = "Klasse IIb";
                        state.Explanation =
                            "Bij falen kan ernstige schade optreden; MDR klasse IIb is van toepassing.";
                        break;

                    case "niet_ernstig":
                        state.MdrClass = "Klasse IIa";
                        state.Explanation =
                            "Bij falen is de schade niet-ernstig; MDR klasse IIa is van toepassing.";
                        break;

                    default:
                        state.MdrClass = "Klasse I";
                        state.Explanation =
                            "Ernst van schade is onbekend of niet herkend; default naar MDR klasse I.";
                        break;
                }
            }

            state.LastUpdated = DateTimeOffset.UtcNow;
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

                var list = JsonSerializer.Deserialize<List<MdrClassificationState>>(json, options);
                if (list == null)
                {
                    return;
                }

                lock (_syncRoot)
                {
                    _storage.Clear();
                    foreach (var item in list)
                    {
                        if (item != null)
                        {
                            _storage[item.AssessmentId] = item;
                        }
                    }
                }
            }
            catch
            {
                // Bij fouten starten we met een lege storage.
            }
        }

        private void SaveToDisk()
        {
            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, FileName);

                List<MdrClassificationState> snapshot;
                lock (_syncRoot)
                {
                    snapshot = _storage.Values
                        .Select(v => v)
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
                // Fouten bij schrijven negeren; data blijft in memory beschikbaar.
            }
        }
    }
}
