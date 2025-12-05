using System;
using System.Collections.Generic;
using HlsCompliance.Api.Domain;
using HlsCompliance.Api.Services;
using Xunit;

namespace HlsCompliance.Tests
{
    public class DueDiligenceServiceTests
    {
        // --------------------------------------------------------------------
        //  Evidence-samenvatting (tab 9/11)
        // --------------------------------------------------------------------

        [Fact]
        public void SummarizeEvidenceStatus_AllApproved_ReturnsVoldoendeBewijs()
        {
            var requiredIds = new[] { "E1", "E2" };
            var evidence = new[]
            {
                new AssessmentEvidenceItem { EvidenceId = "E1", Status = "Goedgekeurd" },
                new AssessmentEvidenceItem { EvidenceId = "E2", Status = "Goedgekeurd" }
            };

            var result = DueDiligenceService.SummarizeEvidenceStatus(requiredIds, evidence);

            Assert.Equal("Voldoende bewijs", result);
        }

        [Fact]
        public void SummarizeEvidenceStatus_MixApprovedAndNotDelivered_ReturnsDeelsAangeleverd()
        {
            var requiredIds = new[] { "E1", "E2" };
            var evidence = new[]
            {
                new AssessmentEvidenceItem { EvidenceId = "E1", Status = "Goedgekeurd" },
                new AssessmentEvidenceItem { EvidenceId = "E2", Status = "Niet aangeleverd" }
            };

            var result = DueDiligenceService.SummarizeEvidenceStatus(requiredIds, evidence);

            Assert.Equal("Deels aangeleverd", result);
        }

        [Fact]
        public void SummarizeEvidenceStatus_AnyRejected_ReturnsOnvoldoendeBewijs()
        {
            var requiredIds = new[] { "E1", "E2" };
            var evidence = new[]
            {
                new AssessmentEvidenceItem { EvidenceId = "E1", Status = "Goedgekeurd" },
                new AssessmentEvidenceItem { EvidenceId = "E2", Status = "Afgekeurd" }
            };

            var result = DueDiligenceService.SummarizeEvidenceStatus(requiredIds, evidence);

            Assert.Equal("Onvoldoende bewijs", result);
        }

        [Fact]
        public void SummarizeEvidenceStatus_NoneRequired_ReturnsGeenBewijsVereist()
        {
            var requiredIds = Array.Empty<string>();
            var evidence = Array.Empty<AssessmentEvidenceItem>();

            var result = DueDiligenceService.SummarizeEvidenceStatus(requiredIds, evidence);

            Assert.Equal("Geen bewijs vereist", result);
        }

        [Fact]
        public void SummarizeEvidenceStatus_RequiredButNoneDelivered_ReturnsNogNietAangeleverd()
        {
            var requiredIds = new[] { "E1" };
            var evidence = Array.Empty<AssessmentEvidenceItem>();

            var result = DueDiligenceService.SummarizeEvidenceStatus(requiredIds, evidence);

            Assert.Equal("Nog niet aangeleverd", result);
        }

        [Fact]
        public void SummarizeEvidenceStatus_InReviewOnly_ReturnsInBeoordeling()
        {
            var requiredIds = new[] { "E1" };
            var evidence = new[]
            {
                new AssessmentEvidenceItem { EvidenceId = "E1", Status = "In beoordeling" }
            };

            var result = DueDiligenceService.SummarizeEvidenceStatus(requiredIds, evidence);

            Assert.Equal("In beoordeling", result);
        }

        // --------------------------------------------------------------------
        //  Einduitkomst due diligence (kolom L)
        // --------------------------------------------------------------------

        [Fact]
        public void EvaluateDueDiligenceOutcome_NotApplicable_ReturnsNietVanToepassing()
        {
            var outcome = DueDiligenceService.EvaluateDueDiligenceOutcome(
                isApplicable: false,
                answerEvaluation: null,
                evidenceResultLabel: null,
                negativeOutcomeAcceptable: false);

            Assert.Equal("Niet van toepassing", outcome);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_PositiveAnswerAndEvidenceOk_ReturnsVoldoet()
        {
            var outcome = DueDiligenceService.EvaluateDueDiligenceOutcome(
                isApplicable: true,
                answerEvaluation: "Goedgekeurd",
                evidenceResultLabel: "Voldoende bewijs",
                negativeOutcomeAcceptable: false);

            Assert.Equal("Voldoet", outcome);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_AnswerRejectedAndNotAcceptable_ReturnsVoldoetNiet()
        {
            var outcome = DueDiligenceService.EvaluateDueDiligenceOutcome(
                isApplicable: true,
                answerEvaluation: "Afgekeurd",
                evidenceResultLabel: "Onvoldoende bewijs",
                negativeOutcomeAcceptable: false);

            Assert.Equal("Voldoet niet", outcome);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_PartiallyApprovedAndGeenBewijsVereist_ReturnsVoldoet()
        {
            var outcome = DueDiligenceService.EvaluateDueDiligenceOutcome(
                isApplicable: true,
                answerEvaluation: "Deels goedgekeurd",
                evidenceResultLabel: "Geen bewijs vereist",
                negativeOutcomeAcceptable: false);

            Assert.Equal("Voldoet", outcome);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_EvidenceBadButAcceptable_ReturnsAfwijkingAcceptabel()
        {
            var outcome = DueDiligenceService.EvaluateDueDiligenceOutcome(
                isApplicable: true,
                answerEvaluation: null,
                evidenceResultLabel: "Onvoldoende bewijs",
                negativeOutcomeAcceptable: true);

            Assert.Equal("Afwijking acceptabel", outcome);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_EvidenceBadAndNotAcceptable_ReturnsVoldoetNiet()
        {
            var outcome = DueDiligenceService.EvaluateDueDiligenceOutcome(
                isApplicable: true,
                answerEvaluation: null,
                evidenceResultLabel: "Onvoldoende bewijs",
                negativeOutcomeAcceptable: false);

            Assert.Equal("Voldoet niet", outcome);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_EvidenceInReview_ReturnsNogTeBeoordelen()
        {
            var outcome = DueDiligenceService.EvaluateDueDiligenceOutcome(
                isApplicable: true,
                answerEvaluation: null,
                evidenceResultLabel: "In beoordeling",
                negativeOutcomeAcceptable: false);

            Assert.Equal("Nog te beoordelen", outcome);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_OnlyEvidenceLabelConvenience_ReturnsVoldoet()
        {
            var outcome = DueDiligenceService.EvaluateDueDiligenceOutcome("Voldoende bewijs");

            Assert.Equal("Voldoet", outcome);
        }

        [Fact]
        public void EvaluateDueDiligenceOutcome_RowOverload_UsesRowFields()
        {
            var row = new AssessmentChecklistRow
            {
                IsApplicable = true,
                AnswerEvaluation = "Goedgekeurd",
                NegativeOutcomeAcceptable = false
            };

            var outcome = DueDiligenceService.EvaluateDueDiligenceOutcome(row);

            // Omdat evidenceResultLabel in deze overload niet wordt meegegeven,
            // maar het antwoord "Goedgekeurd" is, verwachten we "Voldoet".
            Assert.Equal("Voldoet", outcome);
        }
    }
}
