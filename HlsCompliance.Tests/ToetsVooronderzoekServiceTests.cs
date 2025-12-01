using System;
using System.Linq;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Xunit;

namespace HlsCompliance.Tests
{
    public class ToetsVooronderzoekServiceTests
    {
        /// <summary>
        /// Scenario 1:
        /// Geen input in Assessment, geen handmatige antwoorden.
        /// Verwachting: alle samenvattende velden en kern-afgeleide vragen staan op null (Onbekend),
        /// behalve ALG-a dat altijd Ja is.
        /// </summary>
        [Fact]
        public void Scenario1_NoData_AllDerivedAreNullExceptAlgA()
        {
            // Arrange
            var assessmentId = Guid.NewGuid();
            var result = ToetsVooronderzoekService.CreateEmptyResultForTest(assessmentId);
            var assessment = new Assessment(); // alle velden null/standaard

            // Act
            ToetsVooronderzoekService.RecalculateWithAssessment(result, assessment);

            // Assert – samenvattende velden
            Assert.Null(result.DpiaApplicable);
            Assert.Null(result.NenIsoApplicable);
            Assert.Null(result.Nis2Applicable);
            Assert.Null(result.AiActRiskLevel);
            Assert.Null(result.MdrRiskClass);
            Assert.Null(result.Iso13485Applicable);
            Assert.Null(result.CraApplicable);

            // ALG-a is hardcoded Ja
            AssertQuestion(result, "ALG-a", true);

            // Enkele kern-afgeleide vragen moeten null zijn
            AssertQuestion(result, "AVG-a", null);
            AssertQuestion(result, "AIAct-a", null);
            AssertQuestion(result, "MDR-a", null);
            AssertQuestion(result, "NENISO-a", null);
            AssertQuestion(result, "NIS2-a", null);
            AssertQuestion(result, "Koppeling-a", null);
            AssertQuestion(result, "CRA-a", null);
        }

        /// <summary>
        /// Scenario 2:
        /// - Laag/minimaal risico AI-systeem
        /// - Geen medisch hulpmiddel
        /// - Wel securityprofielscore en koppelingen
        /// Verwachting:
        /// - AIAct-a = Ja, AIAct-f = Nee
        /// - Koppeling-a = Ja
        /// - NENISO-a = Ja, NENISO-e = Ja
        /// - NenIsoApplicable = Ja (omdat er minstens één Ja in NENISO-a..e zit)
        /// - CRA-a = Ja (koppelingen), CRA-e = Ja (kritieke dienst of koppeling)
        /// - CraApplicable = Ja
        /// - NIS2 nog Onbekend (geen b/c/e ingevuld)
        /// </summary>
        [Fact]
        public void Scenario2_LowRiskAi_WithSecurityAndConnections()
        {
            // Arrange
            var assessmentId = Guid.NewGuid();
            var result = ToetsVooronderzoekService.CreateEmptyResultForTest(assessmentId);

            var assessment = new Assessment
            {
                DpiaRequired = true,
                AiActRiskLevel = "Laag/minimaal risico",
                MdrClass = "Geen medisch hulpmiddel",
                SecurityProfileRiskScore = 2.0,
                ConnectionsOverallRisk = "Middel"
            };

            // Act
            ToetsVooronderzoekService.RecalculateWithAssessment(result, assessment);

            // Assert – AI Act
            Assert.Equal("Laag/minimaal risico", result.AiActRiskLevel);
            AssertQuestion(result, "AIAct-a", true);   // er is een AI-systeem
            AssertQuestion(result, "AIAct-e", false);  // niet expliciet hoog risico
            AssertQuestion(result, "AIAct-f", false);  // niet "buiten AI Act"

            // Assert – MDR: geen medisch hulpmiddel
            Assert.Equal("Geen medisch hulpmiddel", result.MdrRiskClass);
            AssertQuestion(result, "MDR-a", false);
            AssertQuestion(result, "MDR-f", true);

            // Assert – Koppeling
            AssertQuestion(result, "Koppeling-a", true); // ConnectionsOverallRisk = Middel

            // Assert – NEN/ISO: a & e op Ja, aggregate Ja
            AssertQuestion(result, "NENISO-a", true);
            AssertQuestion(result, "NENISO-e", true);
            Assert.True(result.NenIsoApplicable);

            // NIS2 nog niet ingevuld → samenvatting Onbekend
            Assert.Null(result.Nis2Applicable);
            AssertQuestion(result, "NIS2-a", null);

            // CRA: a en e op Ja → CRA toepasbaar
            AssertQuestion(result, "CRA-a", true);
            AssertQuestion(result, "CRA-e", true);
            Assert.True(result.CraApplicable);
        }

        /// <summary>
        /// Scenario 3:
        /// NIS2: één criterium (omvang) op Ja, de rest Nee.
        /// Volgens Excel C22:
        /// - NIS2-a = Ja als één van b/c/e Ja is
        /// - NIS2-d = Nee (want NENISO-b = Onbekend en NIS2-a = Ja)
        /// Volgens F27:
        /// - Nis2Applicable = Ja (minstens één Ja in a..e)
        /// </summary>
        [Fact]
        public void Scenario3_Nis2_AnyCriteriaTrue_MakesNis2Applicable()
        {
            // Arrange
            var assessmentId = Guid.NewGuid();
            var result = ToetsVooronderzoekService.CreateEmptyResultForTest(assessmentId);

            var assessment = new Assessment
            {
                DpiaRequired = false,
                AiActRiskLevel = null,
                MdrClass = "Geen medisch hulpmiddel"
            };

            // Handmatig NIS2-criteria invullen
            SetQuestion(result, "NIS2-b", false); // sector Nee
            SetQuestion(result, "NIS2-c", true);  // omvang Ja
            SetQuestion(result, "NIS2-e", false); // keten Nee

            // Act
            ToetsVooronderzoekService.RecalculateWithAssessment(result, assessment);

            // Assert – NIS2-a: Ja (één van b/c/e = Ja)
            AssertQuestion(result, "NIS2-a", true);

            // NIS2-d: Nee (NENISO-b = Onbekend, NIS2-a = Ja, dus niet beide Ja)
            AssertQuestion(result, "NIS2-d", false);

            // NIS2 Toepasselijk? – F27: Ja (minstens één Ja in NIS2-a..e)
            Assert.True(result.Nis2Applicable);
        }

        /// <summary>
        /// Scenario 4:
        /// NIS2: alle drie criteria b/c/e op Nee.
        /// Volgens Excel C22:
        /// - NIS2-a = Nee (alle drie Nee)
        /// Volgens F27:
        /// - Nis2Applicable = Nee (alle NIS2-a..e Nee)
        /// </summary>
        [Fact]
        public void Scenario4_Nis2_AllCriteriaNo_MakesNis2NotApplicable()
        {
            // Arrange
            var assessmentId = Guid.NewGuid();
            var result = ToetsVooronderzoekService.CreateEmptyResultForTest(assessmentId);

            var assessment = new Assessment
            {
                DpiaRequired = false,
                AiActRiskLevel = null,
                MdrClass = "Geen medisch hulpmiddel"
            };

            // Handmatig NIS2-criteria invullen
            SetQuestion(result, "NIS2-b", false);
            SetQuestion(result, "NIS2-c", false);
            SetQuestion(result, "NIS2-e", false);

            // Act
            ToetsVooronderzoekService.RecalculateWithAssessment(result, assessment);

            // Assert – NIS2-a: Nee (alle drie Nee)
            AssertQuestion(result, "NIS2-a", false);

            // NIS2-d: Nee (waarden bekend/onbekend maar niet beide Ja)
            AssertQuestion(result, "NIS2-d", false);

            // Nis2Applicable: Nee (alle NIS2-a..e Nee)
            Assert.False(result.Nis2Applicable);
        }

        /// <summary>
        /// Scenario 5:
        /// Medisch hulpmiddel klasse IIb.
        /// Verwachting:
        /// - MDR-a = Ja
        /// - ISO13485-a/c/d = Ja
        /// - Iso13485Applicable (aggregatie a..g) = Ja
        /// </summary>
        [Fact]
        public void Scenario5_MedicalDevice_ClassIIb_MakesIso13485Applicable()
        {
            // Arrange
            var assessmentId = Guid.NewGuid();
            var result = ToetsVooronderzoekService.CreateEmptyResultForTest(assessmentId);

            var assessment = new Assessment
            {
                MdrClass = "Klasse IIb"
            };

            // Act
            ToetsVooronderzoekService.RecalculateWithAssessment(result, assessment);

            // Assert – MDR
            Assert.Equal("Klasse IIb", result.MdrRiskClass);
            AssertQuestion(result, "MDR-a", true);
            AssertQuestion(result, "MDR-d", true); // Klasse IIb

            // ISO13485 afgeleide vragen
            AssertQuestion(result, "ISO13485-a", true); // medisch hulpmiddel
            AssertQuestion(result, "ISO13485-c", true); // medisch doel
            AssertQuestion(result, "ISO13485-d", true); // hoog-risico medisch hulpmiddel

            // Aggregatie over ISO13485-a..g – minst één Ja => toepasbaar
            Assert.True(result.Iso13485Applicable);
        }

        // ------------------------
        // Helpers
        // ------------------------

        private static void AssertQuestion(ToetsVooronderzoekResult result, string toetsId, bool? expected)
        {
            var question = Assert.Single(result.Questions, q => q.ToetsId == toetsId);
            Assert.Equal(expected, question.Answer);
        }

        private static void SetQuestion(ToetsVooronderzoekResult result, string toetsId, bool? value)
        {
            var question = Assert.Single(result.Questions, q => q.ToetsId == toetsId);
            Assert.False(question.IsDerived); // we zetten alleen handmatige vragen
            question.Answer = value;
        }
    }
}
