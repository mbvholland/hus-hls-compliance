using System;
using System.Collections.Generic;
using System.Linq;
using HlsCompliance.Api.Domain;

namespace HlsCompliance.Api.Services
{
    /// <summary>
    /// Service die op basis van de statische mappings (tab 9/10/11)
    /// de bewijsuitvraag voor een assessment opbouwt.
    ///
    /// - ChecklistDefinition (tab 7)
    /// - ChecklistEvidenceLink (tab 9/10): koppeling vraag -> BewijsID
    /// - EvidenceDefinition (tab 10): metadata per BewijsID
    /// - AssessmentEvidenceItem (tab 11): bewijslast per assessment+ChecklistId+BewijsID
    /// </summary>
    public class EvidenceRequestService
    {
        private readonly IChecklistDefinitionRepository _checklistDefinitions;
        private readonly IChecklistEvidenceLinkRepository _checklistEvidenceLinks;
        private readonly IEvidenceDefinitionRepository _evidenceDefinitions;
        private readonly IAssessmentEvidenceRepository _assessmentEvidenceRepository;

        public EvidenceRequestService(
            IChecklistDefinitionRepository checklistDefinitions,
            IChecklistEvidenceLinkRepository checklistEvidenceLinks,
            IEvidenceDefinitionRepository evidenceDefinitions,
            IAssessmentEvidenceRepository assessmentEvidenceRepository)
        {
            _checklistDefinitions = checklistDefinitions ?? throw new ArgumentNullException(nameof(checklistDefinitions));
            _checklistEvidenceLinks = checklistEvidenceLinks ?? throw new ArgumentNullException(nameof(checklistEvidenceLinks));
            _evidenceDefinitions = evidenceDefinitions ?? throw new ArgumentNullException(nameof(evidenceDefinitions));
            _assessmentEvidenceRepository = assessmentEvidenceRepository ?? throw new ArgumentNullException(nameof(assessmentEvidenceRepository));
        }

        /// <summary>
        /// Initialiseert / ververst alle bewijslast-rijen (tab 11) voor een assessment
        /// op basis van de checklistdefinities en de statische links (tab 9/10).
        ///
        /// Bestaande AssessmentEvidenceItem-records worden hergebruikt,
        /// nieuwe worden aangemaakt met:
        /// - Status = "Niet aangeleverd"
        /// - Comment = null
        /// </summary>
        public IReadOnlyList<AssessmentEvidenceItem> InitializeEvidenceForAssessment(Guid assessmentId)
        {
            if (assessmentId == Guid.Empty)
                throw new ArgumentException("AssessmentId is required", nameof(assessmentId));

            var definitions   = _checklistDefinitions.GetAll().ToList();
            var links         = _checklistEvidenceLinks.GetAll().ToList();
            var evidenceDefs  = _evidenceDefinitions.GetAll().ToList();
            var existingItems = _assessmentEvidenceRepository.GetByAssessment(assessmentId).ToList();

            // Key: "ChecklistId||EvidenceId" → bestaand assessment-item
            var byKey = existingItems.ToDictionary(
                e => BuildKey(e.ChecklistId, e.EvidenceId),
                e => e,
                StringComparer.OrdinalIgnoreCase);

            var result = new List<AssessmentEvidenceItem>();

            foreach (var def in definitions)
            {
                var checklistId = def.ChecklistId;

                var linksForChecklist = links
                    .Where(l => string.Equals(l.ChecklistId, checklistId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!linksForChecklist.Any())
                    continue;

                foreach (var link in linksForChecklist)
                {
                    var key = BuildKey(checklistId, link.EvidenceId);

                    if (!byKey.TryGetValue(key, out var item))
                    {
                        var evidenceDef = evidenceDefs.FirstOrDefault(d =>
                            string.Equals(d.EvidenceId, link.EvidenceId, StringComparison.OrdinalIgnoreCase));

                        item = new AssessmentEvidenceItem
                        {
                            AssessmentId = assessmentId,
                            ChecklistId  = checklistId,
                            EvidenceId   = link.EvidenceId,
                            EvidenceName = evidenceDef?.Name ?? link.EvidenceId,
                            Status       = "Niet aangeleverd",
                            Comment      = null
                        };

                        // persistenter maken
                        _assessmentEvidenceRepository.UpsertEvidence(item);
                        byKey[key] = item;
                    }

                    result.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// Zorgt dat de bewijslast voor één specifieke checklist-vraag aanwezig is.
        /// Handig wanneer je alleen voor een subset van de vragen de uitvraag wilt doen.
        /// </summary>
        public IReadOnlyList<AssessmentEvidenceItem> EnsureEvidenceForChecklist(
            Guid assessmentId,
            string checklistId)
        {
            if (assessmentId == Guid.Empty)
                throw new ArgumentException("AssessmentId is required", nameof(assessmentId));

            if (string.IsNullOrWhiteSpace(checklistId))
                throw new ArgumentException("ChecklistId is required", nameof(checklistId));

            var linksForChecklist = _checklistEvidenceLinks.GetAll()
                .Where(l => string.Equals(l.ChecklistId, checklistId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!linksForChecklist.Any())
            {
                return Array.Empty<AssessmentEvidenceItem>();
            }

            var evidenceDefs  = _evidenceDefinitions.GetAll().ToList();
            var existingItems = _assessmentEvidenceRepository.GetByAssessment(assessmentId).ToList();

            var byKey = existingItems.ToDictionary(
                e => BuildKey(e.ChecklistId, e.EvidenceId),
                e => e,
                StringComparer.OrdinalIgnoreCase);

            var result = new List<AssessmentEvidenceItem>();

            foreach (var link in linksForChecklist)
            {
                var key = BuildKey(checklistId, link.EvidenceId);

                if (!byKey.TryGetValue(key, out var item))
                {
                    var evidenceDef = evidenceDefs.FirstOrDefault(d =>
                        string.Equals(d.EvidenceId, link.EvidenceId, StringComparison.OrdinalIgnoreCase));

                    item = new AssessmentEvidenceItem
                    {
                        AssessmentId = assessmentId,
                        ChecklistId  = checklistId,
                        EvidenceId   = link.EvidenceId,
                        EvidenceName = evidenceDef?.Name ?? link.EvidenceId,
                        Status       = "Niet aangeleverd",
                        Comment      = null
                    };

                    _assessmentEvidenceRepository.UpsertEvidence(item);
                    byKey[key] = item;
                }

                result.Add(item);
            }

            return result;
        }

        private static string BuildKey(string? checklistId, string? evidenceId)
            => $"{checklistId ?? string.Empty}||{evidenceId ?? string.Empty}";
    }
}
