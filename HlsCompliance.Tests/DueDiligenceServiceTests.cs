using System;
using System.Collections.Generic;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Xunit;

namespace HlsCompliance.Tests
{
    public class DueDiligenceServiceTests
    {
        // --------------------------
        // Tests voor BewijsResultaat
        // --------------------------

        [Fact]
        public void SummarizeEvidenceStatus_NoItems_ReturnsGeenBewijsVereist()
        {
            // Arrange
            IReadOnlyCollection<AssessmentEvidenceItem>? items = new List<AssessmentEvidenceItem>();

            // Act
            var result = DueDiligenceService.SummarizeEvidenceStatus(items);

            // Assert
            Assert.Equal("Geen bewijs vereist", result);
        }

        [Fact]
        public void SummarizeEvidenceStatus_AllApproved_ReturnsCompleet()
        {
            // Arrange
            IReadOnlyCollection<AssessmentEvidenceItem> items = new List<AssessmentEvidenceItem>
            {
                NewEvidenceItem("Goedgekeurd"),
                NewEvidenceItem("Goedgekeurd")
            };

            // Act
            var result = DueDiligenceService.SummarizeEvidenceStatus(items);

            // Assert
            Assert.Equal("Compleet (alles goedgekeurd)", result);
        }

        [Fact]
        public void SummarizeEvidenceStatus_MixApprovedAndNotDelivered_ReturnsNietAangeleverd()
        {
            // Arrange
            IReadOnlyCollection<AssessmentEvidenceItem> items = new List<AssessmentEvidenceItem>
            {
                NewEvidenceItem("Goedgekeurd"),
                NewEvidenceItem("Niet aangeleverd"),
                NewEvidenceItem(null) // lege status => ook Niet aangeleverd
            };

            // Act
            var result = DueDiligenceService.SummarizeEvidenceStatus(items);

            // Assert
            Assert.Equal("Niet aangeleverd", result);
        }

        [Fact]
        public void SummarizeEvidenceStatus_AnyRejected_ReturnsOnvoldoende()
        {
            // Arrange
            IReadOnlyCollection<AssessmentEvidenceItem> items = new List<AssessmentEvidenceItem>
            {
                NewEvidenceItem("Goedgekeurd"),
                NewEvidenceItem("Afgekeurd"),
                NewEvidenceItem("In beoordeling")
            };

            // Act
            var result = DueDiligenceService.SummarizeEvidenceStatus(items);

            // Assert
            Assert.Equal("Onvoldoende (afgekeurd)", result);
        }

        [Fact]
        public void SummarizeEvidenceStatus_NoRejectedButInReview_ReturnsInBeoordeling()
        {
            // Arrange
            IReadOnlyCollection<AssessmentEvidenceItem> items = new List<AssessmentEvidenceItem>
            {
                NewEvidenceItem("In beoordeling"),
                NewEvidenceItem("Goedgekeurd")
            };

            // Act
            var result = DueDiligenceService.SummarizeEvidenceStatus(items);

            // Assert
            Assert.Equal("In beoordeling", result);
        }

        // -------------------------------
        // Tests voor Resultaat due diligence
        // -------------------------------

        [Fact]
        public void EvaluateDueDiligenceOutcome_NotApplicable_ReturnsNull()
        {
            // Arrange
            var row = NewRow(
                isApplicable: false,
                answerEvaluation: null,
                evidenceSummary: "Geen bewijs vereist",
                negativeOutcomeAcceptable: false);

            // Act
            var result = DueDiligenceService.EvaluateDueDiligenceOutcome(row);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_PositiveAnswerAndEvidenceOk_ReturnsOk()
        {
            // Arrange
            var row = NewRow(
                isApplicable: true,
                answerEvaluation: "Goedgekeurd",
                evidenceSummary: "Compleet (alles goedgekeurd)",
                negativeOutcomeAcceptable: false);

            // Act
            var result = DueDiligenceService.EvaluateDueDiligenceOutcome(row);

            // Assert
            Assert.Equal("OK", result);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_PartiallyApprovedAndGeenBewijsVereist_ReturnsOk()
        {
            // Arrange
            var row = NewRow(
                isApplicable: true,
                answerEvaluation: "Deels goedgekeurd",
                evidenceSummary: "Geen bewijs vereist",
                negativeOutcomeAcceptable: false);

            // Act
            var result = DueDiligenceService.EvaluateDueDiligenceOutcome(row);

            // Assert
            Assert.Equal("OK", result);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_AnswerRejectedAndNotAcceptable_ReturnsNietAcceptabel()
        {
            // Arrange
            var row = NewRow(
                isApplicable: true,
                answerEvaluation: "Afgekeurd",
                evidenceSummary: "Compleet (alles goedgekeurd)",
                negativeOutcomeAcceptable: false);

            // Act
            var result = DueDiligenceService.EvaluateDueDiligenceOutcome(row);

            // Assert
            Assert.Equal("Niet acceptabel", result);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_EvidenceBadButAcceptable_ReturnsAfwijkingAcceptabel()
        {
            // Arrange
            var row = NewRow(
                isApplicable: true,
                answerEvaluation: null,
                evidenceSummary: "Onvoldoende (afgekeurd)",
                negativeOutcomeAcceptable: true);

            // Act
            var result = DueDiligenceService.EvaluateDueDiligenceOutcome(row);

            // Assert
            Assert.Equal("Afwijking acceptabel", result);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_EvidenceBadAndNotAcceptable_ReturnsNietAcceptabel()
        {
            // Arrange
            var row = NewRow(
                isApplicable: true,
                answerEvaluation: null,
                evidenceSummary: "Onvoldoende (afgekeurd)",
                negativeOutcomeAcceptable: false);

            // Act
            var result = DueDiligenceService.EvaluateDueDiligenceOutcome(row);

            // Assert
            Assert.Equal("Niet acceptabel", result);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_NoClearPositiveOrNegative_ReturnsNogTeBeoordelen()
        {
            // Arrange
            var row = NewRow(
                isApplicable: true,
                answerEvaluation: null,
                evidenceSummary: "In beoordeling",
                negativeOutcomeAcceptable: false);

            // Act
            var result = DueDiligenceService.EvaluateDueDiligenceOutcome(row);

            // Assert
            Assert.Equal("Nog te beoordelen", result);
        }

        // --------------------------
        // Helpers
        // --------------------------

        private static AssessmentEvidenceItem NewEvidenceItem(string? status)
        {
            return new AssessmentEvidenceItem
            {
                AssessmentId = Guid.NewGuid(),
                ChecklistId = "Test",
                EvidenceId = Guid.NewGuid().ToString(),
                EvidenceName = "Test-bewijs",
                Status = status,
                Comment = null
            };
        }

        private static AssessmentChecklistRow NewRow(
            bool isApplicable,
            string? answerEvaluation,
            string? evidenceSummary,
            bool negativeOutcomeAcceptable)
        {
            return new AssessmentChecklistRow
            {
                AssessmentId = Guid.NewGuid(),
                ChecklistId = "Test",
                IsApplicable = isApplicable,
                Answer = null,
                AnswerEvaluation = answerEvaluation,
                EvidenceSummary = evidenceSummary,
                NegativeOutcomeAcceptable = negativeOutcomeAcceptable,
                DueDiligenceOutcome = null,
                DeviationText = null
            };
        }
    }
}
