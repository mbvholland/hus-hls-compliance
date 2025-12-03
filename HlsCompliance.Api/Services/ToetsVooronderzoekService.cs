using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    public class ToetsVooronderzoekService
    {
        private readonly AssessmentService _assessmentService;

        // In-memory opslag per assessment
        private readonly Dictionary<Guid, ToetsVooronderzoekResult> _results = new();

        public ToetsVooronderzoekService(AssessmentService assessmentService)
        {
            _assessmentService = assessmentService ?? throw new ArgumentNullException(nameof(assessmentService));
        }

        /// <summary>
        /// Haal het ToetsVooronderzoek-resultaat op (recalculate altijd even).
        /// </summary>
        public ToetsVooronderzoekResult Get(Guid assessmentId)
        {
            var result = GetOrCreateResult(assessmentId);
            Recalculate(result);
            return result;
        }

        /// <summary>
        /// Update handmatige J/N-antwoorden (de 13 niet-afgeleide vragen)
        /// en reken daarna alle afgeleide logica opnieuw uit.
        /// </summary>
        public ToetsVooronderzoekResult UpdateManualAnswers(
            Guid assessmentId,
            IEnumerable<(string ToetsId, bool? Answer)> manualAnswers)
        {
            var result = GetOrCreateResult(assessmentId);

            if (manualAnswers != null)
            {
                var lookup = result.Questions
                    .ToDictionary(q => q.ToetsId, q => q, StringComparer.OrdinalIgnoreCase);

                foreach (var (toetsId, answer) in manualAnswers)
                {
                    if (lookup.TryGetValue(toetsId, out var question) && !question.IsDerived)
                    {
                        question.Answer = answer;
                    }
                }
            }

            Recalculate(result);
            return result;
        }

        private ToetsVooronderzoekResult GetOrCreateResult(Guid assessmentId)
        {
            if (_results.TryGetValue(assessmentId, out var existing))
            {
                return existing;
            }

            var result = new ToetsVooronderzoekResult
            {
                AssessmentId = assessmentId,
                Questions = CreateQuestionTemplate()
            };

            _results[assessmentId] = result;
            return result;
        }

        /// <summary>
        /// Template van alle 51 ToetsVooronderzoek-vragen (exact structuur tab 6).
        /// 38 auto (IsDerived = true, incl. hardcoded ALG-a) en 13 handmatige (IsDerived = false).
        /// </summary>
        private static List<ToetsVooronderzoekQuestion> CreateQuestionTemplate()
        {
            return new List<ToetsVooronderzoekQuestion>
            {
                // -------------------------
                // ALGEMEEN – (r.2–4 + F5)
                // -------------------------
                new()
                {
                    ToetsId = "ALG-a",
                    Text = "Wordt deze leverancier/oplossing structureel ingezet binnen de zorgorganisatie?",
                    IsDerived = true,
                    DerivedFrom = "Excel C2 (hardcoded Ja)",
                    Explanation = "In Excel C2 staat altijd 'Ja'; hier gemodelleerd als vaste Ja (niet handmatig wijzigbaar)."
                },
                new()
                {
                    ToetsId = "ALG-b",
                    Text = "Accepteert de leverancier de standaard BoZ Verwerkersovereenkomst?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "C3: handmatige J/N-vraag."
                },
                new()
                {
                    ToetsId = "ALG-c",
                    Text = "Accepteert de leverancier de standaard LHV Inkoopvoorwaarden-ICT?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "C4: handmatige J/N-vraag."
                },

                // -------------------------
                // AVG / DPIA – (AVG-a t/m AVG-i, r.6–14 + F15)
                // In de engine zijn alleen proxy-signalen beschikbaar; detailvragen zetten we (voor nu) op Onbekend.
                // -------------------------
                new()
                {
                    ToetsId = "AVG-a",
                    Text = "Is er sprake van verwerking van persoonsgegevens (AVG van toepassing)?",
                    IsDerived = true,
                    DerivedFrom = "DPIA/Assessment",
                    Explanation = "Excel C6: afgeleid uit DPIA_Quickscan!E2; hier benaderd via DpiaRequired != null."
                },
                new()
                {
                    ToetsId = "AVG-b",
                    Text = "Gaat het om bijzondere persoonsgegevens / gezondheidsgegevens?",
                    IsDerived = true,
                    DerivedFrom = "DPIA_Quickscan (niet apart gemodelleerd)",
                    Explanation = "Excel C7; hier voorlopig als Onbekend omdat we per-vraag DPIA niet hebben."
                },
                new()
                {
                    ToetsId = "AVG-c",
                    Text = "Is er sprake van grootschalige verwerking?",
                    IsDerived = true,
                    DerivedFrom = "DPIA_Quickscan",
                    Explanation = "Excel C8; hier voorlopig Onbekend."
                },
                new()
                {
                    ToetsId = "AVG-d",
                    Text = "Is er sprake van profilering of geautomatiseerde besluitvorming?",
                    IsDerived = true,
                    DerivedFrom = "DPIA_Quickscan",
                    Explanation = "Excel C9; hier voorlopig Onbekend."
                },
                new()
                {
                    ToetsId = "AVG-e",
                    Text = "Is er sprake van kwetsbare groepen (zoals patiënten, kinderen)?",
                    IsDerived = true,
                    DerivedFrom = "DPIA_Quickscan",
                    Explanation = "Excel C10; hier voorlopig Onbekend."
                },
                new()
                {
                    ToetsId = "AVG-f",
                    Text = "Worden gegevens buiten de EU/EER verwerkt of opgeslagen?",
                    IsDerived = true,
                    DerivedFrom = "DPIA_Quickscan",
                    Explanation = "Excel C11; hier voorlopig Onbekend."
                },
                new()
                {
                    ToetsId = "AVG-g",
                    Text = "Zijn er koppelingen met andere systemen met privacy-impact?",
                    IsDerived = true,
                    DerivedFrom = "DPIA_Quickscan/Koppelingen",
                    Explanation = "Excel C12; hier voorlopig Onbekend."
                },
                new()
                {
                    ToetsId = "AVG-h",
                    Text = "Worden gegevens langer bewaard dan noodzakelijk?",
                    IsDerived = true,
                    DerivedFrom = "DPIA_Quickscan",
                    Explanation = "Excel C13; hier voorlopig Onbekend."
                },
                new()
                {
                    ToetsId = "AVG-i",
                    Text = "Is vernietiging of anonimisering van gegevens een specifiek risico?",
                    IsDerived = true,
                    DerivedFrom = "DPIA_Quickscan",
                    Explanation = "Excel C14; hier voorlopig Onbekend."
                },

                // -------------------------
                // AI Act – (AI Act-a t/m f, r.28–33 + F34)
                // -------------------------
                new()
                {
                    ToetsId = "AIAct-a",
                    Text = "Is er sprake van een AI-systeem onder de AI Act?",
                    IsDerived = true,
                    DerivedFrom = "AI Act",
                    Explanation = "Excel C28: verwijst naar AI Act A2 (is AI-systeem?). Hier op basis van AiActRiskLevel."
                },
                new()
                {
                    ToetsId = "AIAct-b",
                    Text = "Heeft de AI-uitkomst impact op gezondheid, veiligheid of grondrechten?",
                    IsDerived = true,
                    DerivedFrom = "AI Act",
                    Explanation = "Excel C29: zelfde A2-bron; als er een AI-systeem is, hier 'Ja'."
                },
                new()
                {
                    ToetsId = "AIAct-c",
                    Text = "Is het AI-systeem (mogelijk) hoog risico (bijv. medisch hulpmiddel klasse IIa/IIb/III)?",
                    IsDerived = true,
                    DerivedFrom = "AI Act/MDR",
                    Explanation = "Excel C30/C32: hoog risico. Hier: AiActRiskLevel = 'Hoog risico' of MDR-klasse IIa/IIb/III."
                },
                new()
                {
                    ToetsId = "AIAct-d",
                    Text = "Zijn er DPIA-signalen die wijzen op inzet van AI (bijv. DPIA-vraag over AI)?",
                    IsDerived = true,
                    DerivedFrom = "DPIA",
                    Explanation = "Excel C31: verwijst naar DPIA_Quickscan!E7; hier benaderd via DpiaRequired."
                },
                new()
                {
                    ToetsId = "AIAct-e",
                    Text = "Is de risicoklasse AI Act expliciet 'Hoog risico'?",
                    IsDerived = true,
                    DerivedFrom = "AI Act",
                    Explanation = "Excel C32: check op 'Hoog risico' uit tab 4."
                },
                new()
                {
                    ToetsId = "AIAct-f",
                    Text = "Valt de oplossing buiten de AI Act (geen AI-systeem)?",
                    IsDerived = true,
                    DerivedFrom = "AI Act",
                    Explanation = "Excel C33: verwijst naar AI Act E2 (geen AI-systeem)."
                },

                // -------------------------
                // MDR – (MDR-a t/m f, r.35–40 + F41)
                // -------------------------
                new()
                {
                    ToetsId = "MDR-a",
                    Text = "Valt de oplossing onder de MDR (medisch hulpmiddel)?",
                    IsDerived = true,
                    DerivedFrom = "MDR",
                    Explanation = "Excel C35: afgeleid uit MDR F2; hier via Assessment.MdrClass."
                },
                new()
                {
                    ToetsId = "MDR-b",
                    Text = "Is de software geclassificeerd als Klasse I?",
                    IsDerived = true,
                    DerivedFrom = "MDR",
                    Explanation = "Excel C36."
                },
                new()
                {
                    ToetsId = "MDR-c",
                    Text = "Is de software geclassificeerd als Klasse IIa?",
                    IsDerived = true,
                    DerivedFrom = "MDR",
                    Explanation = "Excel C37."
                },
                new()
                {
                    ToetsId = "MDR-d",
                    Text = "Is de software geclassificeerd als Klasse IIb?",
                    IsDerived = true,
                    DerivedFrom = "MDR",
                    Explanation = "Excel C38."
                },
                new()
                {
                    ToetsId = "MDR-e",
                    Text = "Is de software geclassificeerd als Klasse III?",
                    IsDerived = true,
                    DerivedFrom = "MDR",
                    Explanation = "Excel C39."
                },
                new()
                {
                    ToetsId = "MDR-f",
                    Text = "Is de oplossing geen medisch hulpmiddel onder de MDR?",
                    IsDerived = true,
                    DerivedFrom = "MDR",
                    Explanation = "Excel C40: 'Geen medisch hulpmiddel'."
                },

                // -------------------------
                // NEN / ISO – (NEN/ISO-a t/m e, r.16–20 + F21)
                // -------------------------
                new()
                {
                    ToetsId = "NENISO-a",
                    Text = "Is de leverancier (mede) verantwoordelijk voor de beveiliging van data in de dienst?",
                    IsDerived = true,
                    DerivedFrom = "Securityprofiel/Koppelingen",
                    Explanation = "Excel C16: Securityprofiel C10; hier benaderd via SecurityProfileRiskScore > 0 of koppelingen."
                },
                new()
                {
                    ToetsId = "NENISO-b",
                    Text = "Heeft de leverancier structureel een verhoogd informatiebeveiligingsrisico (bijv. uit securityprofiel)?",
                    IsDerived = true,
                    DerivedFrom = "Securityprofiel",
                    Explanation = "Excel C17: Securityprofiel C8; hier benaderd als Onbekend (geen directe bron)."
                },
                new()
                {
                    ToetsId = "NENISO-c",
                    Text = "Is de geleverde dienst essentieel voor continuïteit of patiëntveiligheid?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "Excel C18: handmatige J/N-vraag."
                },
                new()
                {
                    ToetsId = "NENISO-d",
                    Text = "Is NIS2 van toepassing op de organisatie/oplossing?",
                    IsDerived = true,
                    DerivedFrom = "NIS2-a",
                    Explanation = "Excel C19: =C22 (NIS2-a)."
                },
                new()
                {
                    ToetsId = "NENISO-e",
                    Text = "Maakt de leverancier deel uit van een keten van zorgkritische systemen of koppelingen?",
                    IsDerived = true,
                    DerivedFrom = "AIAct-a/Koppeling-a",
                    Explanation = "Excel C20: IF(OR(C28='Ja',C57='Ja'),'Ja',IF(AND(C28='Nee',C57='Nee'),'Nee',''))."
                },

                // -------------------------
                // NIS2 – (NIS2-a t/m e, r.22–26 + F27)
                // -------------------------
                new()
                {
                    ToetsId = "NIS2-b",
                    Text = "Valt de dienst/sector onder essentiële/belangrijke NIS2-sectoren (bijv. zorg)?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "Excel C23: handmatige sectorvraag."
                },
                new()
                {
                    ToetsId = "NIS2-c",
                    Text = "Heeft de organisatie een omvang conform NIS2-criteria (middelgroot of groter)?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "Excel C24: handmatige omvangsvraag."
                },
                new()
                {
                    ToetsId = "NIS2-e",
                    Text = "Is er sprake van een essentiële/belangrijke digitale dienst in de keten van een NIS2-entiteit?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "Excel C26: handmatige ketenrol/digitale dienst-vraag."
                },
                new()
                {
                    ToetsId = "NIS2-a",
                    Text = "Valt de organisatie/oplossing onder de scope van NIS2 (samenvattend)?",
                    IsDerived = true,
                    DerivedFrom = "NIS2-b/NIS2-c/NIS2-e",
                    Explanation = "Excel C22: Ja als één van b/c/e Ja, Nee als alle drie Nee, anders Onbekend."
                },
                new()
                {
                    ToetsId = "NIS2-d",
                    Text = "Is NIS2 relevant voor verdere uitwerking van NEN7510/ISO27001-verplichtingen?",
                    IsDerived = true,
                    DerivedFrom = "NENISO-b/NIS2-a",
                    Explanation = "Excel C25: Ja als NEN/ISO-b en NIS2-a beide Ja; Nee als waarden bekend maar niet beide Ja; anders Onbekend."
                },

                // -------------------------
                // ISO13485 – (ISO13485-a t/m g, r.42–48 + F49)
                // -------------------------
                new()
                {
                    ToetsId = "ISO13485-a",
                    Text = "Is er een medisch hulpmiddel of AI-toepassing waar ISO13485 waarschijnlijk geldt?",
                    IsDerived = true,
                    DerivedFrom = "MDR/AI Act",
                    Explanation = "Excel C42: koppelt AI Act & MDR; hier benaderd via 'MdrClass != Geen medisch hulpmiddel'."
                },
                new()
                {
                    ToetsId = "ISO13485-b",
                    Text = "Betreft het een medische software/dienst in de zin van ISO13485?",
                    IsDerived = true,
                    DerivedFrom = "ISO13485-a",
                    Explanation = "Excel C43: =IF(C42=\"\",\"\",C42); hier gemodelleerd als kopie van ISO13485-a."
                },
                new()
                {
                    ToetsId = "ISO13485-c",
                    Text = "Heeft de software een medisch doel of beïnvloedt zij medische beslissingen?",
                    IsDerived = true,
                    DerivedFrom = "MDR",
                    Explanation = "Excel C44: verwijst naar MDR A2 (medisch doel ja/nee)."
                },
                new()
                {
                    ToetsId = "ISO13485-d",
                    Text = "Is de software geclassificeerd in MDR-klasse IIa of hoger?",
                    IsDerived = true,
                    DerivedFrom = "MDR/AI Act",
                    Explanation = "Excel C45: AI Act B2; hier benaderd als hoog-risico medisch hulpmiddel (MDR IIa/IIb/III)."
                },
                new()
                {
                    ToetsId = "ISO13485-e",
                    Text = "Heeft de leverancier een kwaliteitssysteem voor medische software (ISO13485 of gelijkwaardig)?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "Excel C46: handmatig/overig."
                },
                new()
                {
                    ToetsId = "ISO13485-f",
                    Text = "Worden ontwerp, productie en onderhoud aantoonbaar beheerst (bijv. validatie, wijzigingen)?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "Excel C47: handmatig."
                },
                new()
                {
                    ToetsId = "ISO13485-g",
                    Text = "Gebruikt de leverancier onderaannemers voor ontwerp/validatie/productie van medische software?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "Excel C48: handmatige J/N-vraag."
                },

                // -------------------------
                // CRA – (CRA-a t/m f, r.50–55 + F56)
                // -------------------------
                new()
                {
                    ToetsId = "CRA-a",
                    Text = "Bevat het product digitale componenten/koppelingen met andere systemen (CRA-scope)?",
                    IsDerived = true,
                    DerivedFrom = "Koppeling-a",
                    Explanation = "Excel C50: =C57 (Koppeling-a)."
                },
                new()
                {
                    ToetsId = "CRA-b",
                    Text = "Is er sprake van een hoog-risico medisch hulpmiddel dat onder CRA kan vallen?",
                    IsDerived = true,
                    DerivedFrom = "MDR-d",
                    Explanation = "Excel C51: =C38 (MDR-d). Hier benaderd via MDR-klasse IIb/III."
                },
                new()
                {
                    ToetsId = "CRA-c",
                    Text = "Kan de software kwetsbaarheden/incidenten veroorzaken met impact op andere systemen?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "Excel C52: handmatige J/N-vraag."
                },
                new()
                {
                    ToetsId = "CRA-d",
                    Text = "Wordt het product regelmatig bijgewerkt of onderhouden gedurende de levenscyclus?",
                    IsDerived = true,
                    DerivedFrom = "ISO13485-f",
                    Explanation = "Excel C53: =IF(C47=\"\",\"\",C47); hier gemodelleerd als kopie van ISO13485-f."
                },
                new()
                {
                    ToetsId = "CRA-e",
                    Text = "Is er een essentiële/risicovolle combinatie van kritieke dienst (NEN/ISO-c) en koppelingen?",
                    IsDerived = true,
                    DerivedFrom = "NENISO-c/Koppeling-a",
                    Explanation = "Excel C54: IF(OR(C18='Ja',C57='Ja'),'Ja',IF(AND(C18='Nee',C57='Nee'),'Nee',''))."
                },
                new()
                {
                    ToetsId = "CRA-f",
                    Text = "Gebruikt de leverancier open-source of externe cloudsoftware met veiligheidsimpact?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "Excel C55: handmatige J/N-vraag."
                },

                // -------------------------
                // Koppeling – (Koppeling-a, r.57–59)
                // -------------------------
                new()
                {
                    ToetsId = "Koppeling-a",
                    Text = "Bevat het product koppelingen met andere zorgsystemen?",
                    IsDerived = true,
                    DerivedFrom = "Koppeling-Beslisboom/Assessment",
                    Explanation = "Excel C57: afgeleid uit Koppeling-Beslisboom C2 (Geen/Laag/Middel/Hoog/Onbekend). Hier via Assessment.ConnectionsOverallRisk."
                },

                // -------------------------
                // Continuiteit – (Continuiteit-a/b/c, r.60–62)
                // -------------------------
                new()
                {
                    ToetsId = "Continuiteit-a",
                    Text = "Is de leverancier contractueel verplicht tot continuïteitsmaatregelen (escrow, exit)?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "Excel C60: handmatige J/N-vraag."
                },
                new()
                {
                    ToetsId = "Continuiteit-b",
                    Text = "Is opslag/verwijdering van persoonsgegevens als specifiek risico benoemd (AVG-i)?",
                    IsDerived = true,
                    DerivedFrom = "AVG-i/placeholder",
                    Explanation = "Excel C61: =IF(C14=\"\",\"\",C14); hier benaderd via DpiaRequired."
                },
                new()
                {
                    ToetsId = "Continuiteit-c",
                    Text = "Is de leverancier afhankelijk van een zeer kleine personele basis (< 10 FTE)?",
                    IsDerived = false,
                    DerivedFrom = "Handmatig",
                    Explanation = "Excel C62: handmatige J/N-vraag."
                }
            };
        }

        /// <summary>
        /// Helper om een J/N antwoord op te halen.
        /// </summary>
        private static bool? GetQuestionAnswer(ToetsVooronderzoekResult result, string toetsId)
        {
            var q = result.Questions.FirstOrDefault(
                x => x.ToetsId.Equals(toetsId, StringComparison.OrdinalIgnoreCase));
            return q?.Answer;
        }

        /// <summary>
        /// Helper om een vraag in te stellen (als hij bestaat).
        /// </summary>
        private static void SetQuestionAnswer(ToetsVooronderzoekResult result, string toetsId, bool? value)
        {
            var q = result.Questions.FirstOrDefault(
                x => x.ToetsId.Equals(toetsId, StringComparison.OrdinalIgnoreCase));
            if (q != null)
            {
                q.Answer = value;
            }
        }

        /// <summary>
        /// Aggregatie-helper volgens F-kolom logica:
        /// - Ja als minstens één Ja.
        /// - Nee als álle vragen Nee zijn.
        /// - Anders Onbekend (null).
        /// </summary>
        private static bool? AggregateYesNo(ToetsVooronderzoekResult result, params string[] toetsIds)
        {
            var answers = toetsIds
                .Select(id => GetQuestionAnswer(result, id))
                .ToList();

            var yesCount = answers.Count(a => a == true);
            var noCount = answers.Count(a => a == false);
            var total = toetsIds.Length;

            if (yesCount > 0)
            {
                return true;
            }

            if (noCount == total)
            {
                return false;
            }

            return null;
        }

        /// <summary>
        /// Publieke helper voor tests: maak een leeg resultaat met standaardvragen.
        /// </summary>
        public static ToetsVooronderzoekResult CreateEmptyResultForTest(Guid assessmentId)
        {
            return new ToetsVooronderzoekResult
            {
                AssessmentId = assessmentId,
                Questions = CreateQuestionTemplate()
            };
        }

        /// <summary>
        /// Herbereken ToetsVooronderzoekResult op basis van een aangeleverde Assessment.
        /// (Dit is de kernlogica die we ook in unit tests kunnen gebruiken.)
        /// </summary>
        public static void RecalculateWithAssessment(ToetsVooronderzoekResult result, Assessment assessment)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (assessment == null) throw new ArgumentNullException(nameof(assessment));

            // 1. Alle "direct afgeleide" vragen op basis van Assessment
            foreach (var question in result.Questions.Where(q => q.IsDerived))
            {
                question.Answer = DeriveAnswer(question.ToetsId, assessment);
            }

            // 2. NIS2-afleiding (C22, C25) en NEN/ISO-d/e afhankelijkheden
            ApplyNis2Logic(result);
            ApplyNenIsoExtras(result);

            // 3. ISO13485-afleiding die andere vragen gebruikt (C43)
            ApplyIso13485Extras(result);

            // 4. CRA-afleiding (C50–C55) en Koppeling-afleidingen
            ApplyCraLogic(result);

            // 5. Samenvattende velden (kolom E/F equivalent)

            // DPIA Toepasselijk? – Excel F15 verwijst naar DPIA_Quickscan!E17
            result.DpiaApplicable = GetQuestionAnswer(result, "AVG-a") ?? assessment.DpiaRequired;

            // Risicoklasse AI Act? – direct uit Assessment
            result.AiActRiskLevel = assessment.AiActRiskLevel;

            // Risicoklasse MDR? – direct uit Assessment
            result.MdrRiskClass = assessment.MdrClass;

            // NEN7510/ISO27001 Toepasselijk? – F21: aggregatie over NEN/ISO-a..e
            result.NenIsoApplicable = AggregateYesNo(result,
                "NENISO-a", "NENISO-b", "NENISO-c", "NENISO-d", "NENISO-e");

            // NIS2 Toepasselijk? – F27: aggregatie over NIS2-a..e
            result.Nis2Applicable = AggregateYesNo(result,
                "NIS2-a", "NIS2-b", "NIS2-c", "NIS2-d", "NIS2-e");

            // ISO13485 Toepasselijk? – F49: aggregatie over ISO13485-a..g
            result.Iso13485Applicable = AggregateYesNo(result,
                "ISO13485-a", "ISO13485-b", "ISO13485-c",
                "ISO13485-d", "ISO13485-e", "ISO13485-f", "ISO13485-g");

            // CRA Toepasselijk? – F56: aggregatie over CRA-a..f
            result.CraApplicable = AggregateYesNo(result,
                "CRA-a", "CRA-b", "CRA-c", "CRA-d", "CRA-e", "CRA-f");

            // 6. Vul ToetsAnswers dictionary met alle ToetsID → Answer
            result.ToetsAnswers.Clear();
            foreach (var q in result.Questions)
            {
                result.ToetsAnswers[q.ToetsId] = q.Answer;
            }

            // 7. BoZ/LHV-dekking:
            // eerste versie: direct op basis van ALG-b (BoZ-acceptatie) en ALG-c (LHV-acceptatie).
            result.IsBozCovered = GetQuestionAnswer(result, "ALG-b");
            result.IsLhvCovered = GetQuestionAnswer(result, "ALG-c");

            // Timestamp
            result.LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// NIS2: implementeert C22 en C25 + houdt rekening met handmatige b/c/e.
        /// </summary>
        private static void ApplyNis2Logic(ToetsVooronderzoekResult result)
        {
            // NIS2-a – C22: Ja als één van b/c/e Ja is, Nee als alle drie Nee, anders Onbekend.
            var b = GetQuestionAnswer(result, "NIS2-b");
            var c = GetQuestionAnswer(result, "NIS2-c");
            var e = GetQuestionAnswer(result, "NIS2-e");

            var yesCount = new[] { b, c, e }.Count(x => x == true);
            var noCount = new[] { b, c, e }.Count(x => x == false);

            bool? nis2A;
            if (yesCount > 0)
            {
                nis2A = true;
            }
            else if (noCount == 3)
            {
                nis2A = false;
            }
            else
            {
                nis2A = null;
            }

            SetQuestionAnswer(result, "NIS2-a", nis2A);

            // NIS2-d – C25: Ja als NEN/ISO-b en NIS2-a beide Ja, Nee als waarden bekend maar niet beide Ja, anders Onbekend.
            var nenIsoB = GetQuestionAnswer(result, "NENISO-b");
            var nis2aAnswer = nis2A;

            bool? nis2D;
            if (!nenIsoB.HasValue && !nis2aAnswer.HasValue)
            {
                nis2D = null;
            }
            else if (nenIsoB == true && nis2aAnswer == true)
            {
                nis2D = true;
            }
            else
            {
                nis2D = false;
            }

            SetQuestionAnswer(result, "NIS2-d", nis2D);
        }

        /// <summary>
        /// NEN/ISO-afleidingen die van andere vragen afhangen:
        /// - NEN/ISO-d = NIS2-a (C19)
        /// - NEN/ISO-e uit AIAct-a en Koppeling-a (C20).
        /// </summary>
        private static void ApplyNenIsoExtras(ToetsVooronderzoekResult result)
        {
            // NEN/ISO-d – C19: =C22 (NIS2-a)
            var nis2A = GetQuestionAnswer(result, "NIS2-a");
            SetQuestionAnswer(result, "NENISO-d", nis2A);

            // NEN/ISO-e – C20: IF(OR(C28="Ja",C57="Ja"),"Ja",IF(AND(C28="Nee",C57="Nee"),"Nee",""))
            var aiActA = GetQuestionAnswer(result, "AIAct-a");
            var koppelingA = GetQuestionAnswer(result, "Koppeling-a");

            bool? nenIsoE;
            if ((aiActA == true) || (koppelingA == true))
            {
                nenIsoE = true;
            }
            else if (aiActA == false && koppelingA == false)
            {
                nenIsoE = false;
            }
            else
            {
                nenIsoE = null;
            }

            SetQuestionAnswer(result, "NENISO-e", nenIsoE);
        }

        /// <summary>
        /// ISO13485-extra logica:
        /// - ISO13485-b = ISO13485-a (C43).
        /// </summary>
        private static void ApplyIso13485Extras(ToetsVooronderzoekResult result)
        {
            var isoA = GetQuestionAnswer(result, "ISO13485-a");
            // C43: IF(C42="", "", C42) -> als a Onbekend, b Onbekend; anders gelijk aan a.
            bool? isoB = isoA.HasValue ? isoA : null;
            SetQuestionAnswer(result, "ISO13485-b", isoB);
        }

        /// <summary>
        /// CRA-logica: koppelt CRA-a/b/d/e aan Koppeling/NEN/ISO/MDR/ISO13485 en zet vervolgens de vragen.
        /// </summary>
        private static void ApplyCraLogic(ToetsVooronderzoekResult result)
        {
            // CRA-a – C50: =C57 (Koppeling-a)
            var koppA = GetQuestionAnswer(result, "Koppeling-a");
            SetQuestionAnswer(result, "CRA-a", koppA);

            // CRA-b – C51: =C38 (MDR-d) – hier benaderen we dit als Ja bij hoge MDR-klasse (IIb/III),
            // maar C38 zelf is al via DeriveAnswer voor MDR-d gezet.
            var mdrD = GetQuestionAnswer(result, "MDR-d");
            SetQuestionAnswer(result, "CRA-b", mdrD);

            // CRA-d – C53: =IF(C47="", "", C47) -> kopie van ISO13485-f
            var isoF = GetQuestionAnswer(result, "ISO13485-f");
            bool? craD = isoF.HasValue ? isoF : (bool?)null;
            SetQuestionAnswer(result, "CRA-d", craD);

            // CRA-e – C54: IF(OR(C18="Ja",C57="Ja"),"Ja",IF(AND(C18="Nee",C57="Nee"),"Nee",""))
            var nenIsoC = GetQuestionAnswer(result, "NENISO-c");
            var koppelingA = koppA;

            bool? craE;
            if ((nenIsoC == true) || (koppelingA == true))
            {
                craE = true;
            }
            else if (nenIsoC == false && koppelingA == false)
            {
                craE = false;
            }
            else
            {
                craE = null;
            }

            SetQuestionAnswer(result, "CRA-e", craE);

            // CRA-c en CRA-f blijven handmatig.
        }

        /// <summary>
        /// Productiepad: herbereken met AssessmentService.
        /// </summary>
        private void Recalculate(ToetsVooronderzoekResult result)
        {
            var assessment = _assessmentService.GetById(result.AssessmentId)
                             ?? throw new InvalidOperationException(
                                 $"Assessment {result.AssessmentId} not found.");

            RecalculateWithAssessment(result, assessment);
        }

        /// <summary>
        /// Kern van de afleidingslogica per ToetsID op basis van Assessment.
        /// Dit dekt alle vragen die direct uit tab 1–5 komen (zonder eerst andere ToetsVooronderzoek-vragen nodig te hebben).
        /// </summary>
        private static bool? DeriveAnswer(string toetsId, Assessment assessment)
        {
            var ai = assessment.AiActRiskLevel;
            var mdr = assessment.MdrClass;
            var securityProfileScore = assessment.SecurityProfileRiskScore;
            var connectionsOverallRisk = assessment.ConnectionsOverallRisk;

            bool HasAiRisk(string riskLabel) =>
                !string.IsNullOrWhiteSpace(ai) &&
                ai.Equals(riskLabel, StringComparison.OrdinalIgnoreCase);

            bool HasMdrClass(string classLabel) =>
                !string.IsNullOrWhiteSpace(mdr) &&
                mdr.Equals(classLabel, StringComparison.OrdinalIgnoreCase);

            bool IsNoAiSystem() =>
                !string.IsNullOrWhiteSpace(ai) &&
                ai.Equals("Geen AI-systeem (buiten AI Act)", StringComparison.OrdinalIgnoreCase);

            bool IsNoMedicalDevice() =>
                !string.IsNullOrWhiteSpace(mdr) &&
                mdr.Equals("Geen medisch hulpmiddel", StringComparison.OrdinalIgnoreCase);

            bool IsHighRiskMedicalDevice() =>
                !IsNoMedicalDevice() &&
                (HasMdrClass("Klasse IIa") || HasMdrClass("Klasse IIb") || HasMdrClass("Klasse III"));

            switch (toetsId)
            {
                // -------------------------
                // ALG – C2 hardcoded Ja
                // -------------------------
                case "ALG-a":
                    // Excel C2 bevat letterlijk "Ja".
                    return true;

                // -------------------------
                // AVG – sterk vereenvoudigd
                // -------------------------
                case "AVG-a":
                    // Excel C6: DPIA_Quickscan!E2. Hier: als er überhaupt een DPIA-beoordeling is gedaan,
                    // nemen we aan dat AVG relevant is.
                    return assessment.DpiaRequired.HasValue ? true : (bool?)null;

                case "AVG-b":
                case "AVG-c":
                case "AVG-d":
                case "AVG-e":
                case "AVG-f":
                case "AVG-g":
                case "AVG-h":
                case "AVG-i":
                    // In Excel zijn dit DPIA-afgeleide formules; hier hebben we geen per-vraag DPIA-answers,
                    // dus laten we deze op Onbekend.
                    return null;

                // -------------------------
                // AI Act
                // -------------------------
                case "AIAct-a":
                    if (string.IsNullOrWhiteSpace(ai))
                        return null;
                    return !IsNoAiSystem();

                case "AIAct-b":
                    if (string.IsNullOrWhiteSpace(ai))
                        return null;
                    // Zelfde bron als AIAct-a; als er een AI-systeem is, zeggen we hier Ja.
                    return !IsNoAiSystem();

                case "AIAct-c":
                    if (string.IsNullOrWhiteSpace(ai) && string.IsNullOrWhiteSpace(mdr))
                        return null;

                    if (HasAiRisk("Hoog risico"))
                        return true;

                    if (IsHighRiskMedicalDevice())
                        return true;

                    return (string.IsNullOrWhiteSpace(ai) && string.IsNullOrWhiteSpace(mdr)) ? (bool?)null : false;

                case "AIAct-d":
                    // Excel C31: DPIA_Quickscan!E7; hier grof benaderd: als DpiaRequired = true, zetten we dit op Ja.
                    if (!assessment.DpiaRequired.HasValue)
                        return null;
                    return assessment.DpiaRequired.Value;

                case "AIAct-e":
                    if (string.IsNullOrWhiteSpace(ai))
                        return null;
                    return HasAiRisk("Hoog risico");

                case "AIAct-f":
                    if (string.IsNullOrWhiteSpace(ai))
                        return null;
                    return IsNoAiSystem();

                // -------------------------
                // MDR
                // -------------------------
                case "MDR-a":
                    if (string.IsNullOrWhiteSpace(mdr))
                        return null;
                    return !IsNoMedicalDevice();

                case "MDR-b":
                    if (string.IsNullOrWhiteSpace(mdr))
                        return null;
                    return HasMdrClass("Klasse I");

                case "MDR-c":
                    if (string.IsNullOrWhiteSpace(mdr))
                        return null;
                    return HasMdrClass("Klasse IIa");

                case "MDR-d":
                    if (string.IsNullOrWhiteSpace(mdr))
                        return null;
                    return HasMdrClass("Klasse IIb");

                case "MDR-e":
                    if (string.IsNullOrWhiteSpace(mdr))
                        return null;
                    return HasMdrClass("Klasse III");

                case "MDR-f":
                    if (string.IsNullOrWhiteSpace(mdr))
                        return null;
                    return IsNoMedicalDevice();

                // -------------------------
                // NEN / ISO
                // -------------------------
                case "NENISO-a":
                    // Excel C16: Securityprofiel C10; hier: Ja als er enig securityrisico of koppeling is.
                    if (securityProfileScore.HasValue && securityProfileScore.Value > 0)
                        return true;

                    if (!string.IsNullOrWhiteSpace(connectionsOverallRisk) &&
                        !connectionsOverallRisk.Equals("Geen", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (!securityProfileScore.HasValue &&
                        string.IsNullOrWhiteSpace(connectionsOverallRisk))
                        return null;

                    return false;

                case "NENISO-b":
                    // Excel C17: op basis van Securityprofiel C8; die hebben we niet als bron,
                    // dus hier laten we deze op Onbekend.
                    return null;

                // NENISO-c/d/e worden deels in ApplyNenIsoExtras gezet.

                // -------------------------
                // ISO13485 (afgeleid)
                // -------------------------
                case "ISO13485-a":
                    // Vereenvoudigd: Ja als er een medisch hulpmiddel is.
                    if (string.IsNullOrWhiteSpace(mdr))
                        return null;
                    return !IsNoMedicalDevice();

                case "ISO13485-c":
                    // Excel C44: medisch doel ja/nee; hier: zelfde als 'is medisch hulpmiddel'.
                    if (string.IsNullOrWhiteSpace(mdr))
                        return null;
                    return !IsNoMedicalDevice();

                case "ISO13485-d":
                    // Excel C45: draait in de praktijk om hoog-risico medisch hulpmiddel.
                    if (string.IsNullOrWhiteSpace(mdr))
                        return null;
                    return IsHighRiskMedicalDevice();

                // ISO13485-b wordt in ApplyIso13485Extras gezet.

                // -------------------------
                // CRA – directe afleidingen
                // -------------------------
                case "CRA-a":
                    // C50 = C57 (Koppeling-a); als we alleen Assessment hebben:
                    if (string.IsNullOrWhiteSpace(connectionsOverallRisk))
                        return null;

                    if (connectionsOverallRisk.Equals("Onbekend", StringComparison.OrdinalIgnoreCase))
                        return null;

                    if (connectionsOverallRisk.Equals("Geen", StringComparison.OrdinalIgnoreCase))
                        return false;

                    // Laag/Middel/Hoog → Ja
                    return true;

                case "CRA-b":
                    // C51 = C38 (MDR-d) – hier hergebruiken we de hoge MDR-klassen (IIb/III) als indicator.
                    if (string.IsNullOrWhiteSpace(mdr))
                        return null;
                    return HasMdrClass("Klasse IIb") || HasMdrClass("Klasse III");

                // CRA-d/e worden in ApplyCraLogic gezet.

                // -------------------------
                // Koppeling-a
                // -------------------------
                case "Koppeling-a":
                    // Excel C57: Onbekend → "", Geen → Nee, Laag/Middel/Hoog → Ja
                    if (string.IsNullOrWhiteSpace(connectionsOverallRisk))
                        return null;

                    if (connectionsOverallRisk.Equals("Onbekend", StringComparison.OrdinalIgnoreCase))
                        return null;

                    if (connectionsOverallRisk.Equals("Geen", StringComparison.OrdinalIgnoreCase))
                        return false;

                    // Laag/Middel/Hoog → Ja
                    return true;

                // -------------------------
                // Continuiteit-b – placeholder
                // -------------------------
                case "Continuiteit-b":
                    // Excel C61: =IF(C14="", "", C14) – C14 is AVG-i.
                    // We hebben geen AVG-i apart; benaderd via DpiaRequired als er iets rond AVG op tafel ligt.
                    if (!assessment.DpiaRequired.HasValue)
                        return null;
                    return assessment.DpiaRequired.Value;

                default:
                    return null;
            }
        }
    }
}
