using Acmp.Modules.Research.Domain.Enums;
using Acmp.Modules.Research.Domain.Events;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Research.Domain;

// The ResearchMission aggregate root (RMS-YYYY-###; P15a, FR-111/113/114/115) — a first-class, auditable home
// for a research/discovery mission and its owned Findings + Recommendations (manual entry in v1). Identity to
// other modules is by value only: SourceTopicId (the topic that prompted the mission) is stored as a plain
// Guid, never an EF navigation and never a traceability edge here — the Mission→Topic / Finding→Decision /
// Recommendation→Action graph edges are DEFERRED to P15c (ADR-0001). Keystone import (FR-112) is deferred
// (D-05): KeystonePackageRef is stored only, never imported.
//
// Lifecycle (FR-111): Proposed (author/revise fields) → Active (capture findings + recommendations) →
// Completed (terminal, immutable), with a side exit → Cancelled (reason recorded; terminal). Completed and
// Cancelled freeze the mission — no field edits and no child mutations thereafter.
public sealed class ResearchMission : AuditableEntity
{
    private readonly List<Finding> _findings = new();
    private readonly List<Recommendation> _recommendations = new();

    private ResearchMission() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → 409.
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty;   // RMS-YYYY-###
    public ResearchMissionStatus Status { get; private set; }
    public LocalizedString Title { get; private set; } = null!;
    public LocalizedString Question { get; private set; } = null!;   // FR-111's "description" = the research question

    // Owner attribution snapshot (Keycloak sub + display name). Attribution only — NOT an enforced ownership
    // gate (the AiO nuance is resolved by the endpoint ResearchManage policy; a non-topic-scoped mission has
    // no topic relationship to resolve, so Member/Reviewer are denied at a bare create exactly like ADRs).
    public string OwnerUserId { get; private set; } = string.Empty;
    public string OwnerName { get; private set; } = string.Empty;

    // Deferred references — stored values only (no import, no graph edge in P15a).
    public string? KeystonePackageRef { get; private set; }   // FR-112 deferred (D-05): reference only
    public Guid? SourceTopicId { get; private set; }          // FR-115 deferred: field only, no edge (P15c)

    // Terminal evidence.
    public DateTimeOffset? CompletedAt { get; private set; }
    public LocalizedString? CancellationReason { get; private set; }

    public IReadOnlyCollection<Finding> Findings => _findings.AsReadOnly();
    public IReadOnlyCollection<Recommendation> Recommendations => _recommendations.AsReadOnly();

    // FR-111: propose a new mission in Proposed. Title + Question + owner are required.
    public static ResearchMission Propose(string key, LocalizedString title, LocalizedString question,
        string ownerUserId, string ownerName, string? keystonePackageRef, Guid? sourceTopicId, DateTimeOffset now)
    {
        if (title is null) throw new InvalidOperationException("A mission title is required.");
        if (question is null) throw new InvalidOperationException("A research question is required.");
        if (string.IsNullOrWhiteSpace(ownerUserId)) throw new InvalidOperationException("A mission owner is required.");

        var mission = new ResearchMission
        {
            Key = key.Trim(),
            Status = ResearchMissionStatus.Proposed,
            Title = title,
            Question = question,
            OwnerUserId = ownerUserId,
            OwnerName = (ownerName ?? string.Empty).Trim(),
            KeystonePackageRef = string.IsNullOrWhiteSpace(keystonePackageRef) ? null : keystonePackageRef.Trim(),
            SourceTopicId = sourceTopicId,
        };
        mission.Raise(new ResearchProposedEvent(mission.PublicId, mission.Key, ownerUserId, now));
        return mission;
    }

    // Revise the mission's own fields. Allowed ONLY while Proposed — once Active the question is locked and
    // once terminal the mission is immutable.
    public void UpdateDraft(LocalizedString title, LocalizedString question, string? keystonePackageRef, Guid? sourceTopicId)
    {
        RequireStatus(ResearchMissionStatus.Proposed);
        Title = title ?? throw new InvalidOperationException("A mission title is required.");
        Question = question ?? throw new InvalidOperationException("A research question is required.");
        KeystonePackageRef = string.IsNullOrWhiteSpace(keystonePackageRef) ? null : keystonePackageRef.Trim();
        SourceTopicId = sourceTopicId;
    }

    // FR-111: start the mission. Proposed → Active (findings + recommendations may now be captured).
    public void Activate(DateTimeOffset now)
    {
        RequireStatus(ResearchMissionStatus.Proposed);
        Status = ResearchMissionStatus.Active;
        Raise(new ResearchActivatedEvent(PublicId, Key, now));
    }

    // FR-111: complete the mission (the state transition only — no Keystone import, D-05). Active → Completed
    // (terminal, immutable).
    public void Complete(DateTimeOffset now)
    {
        RequireStatus(ResearchMissionStatus.Active);
        Status = ResearchMissionStatus.Completed;
        CompletedAt = now;
        Raise(new ResearchCompletedEvent(PublicId, Key, now));
    }

    // FR-111: cancel the mission (side exit). Proposed/Active → Cancelled (terminal); rationale recorded.
    public void Cancel(LocalizedString reason, DateTimeOffset now)
    {
        RequireStatus(ResearchMissionStatus.Proposed, ResearchMissionStatus.Active);
        CancellationReason = reason ?? throw new InvalidOperationException("A cancellation reason is required.");
        Status = ResearchMissionStatus.Cancelled;
        Raise(new ResearchCancelledEvent(PublicId, Key, now));
    }

    // FR-113: capture a finding. Allowed only while the mission is Active. Key is a per-mission ordinal (FND-###).
    public Finding AddFinding(LocalizedString summary, LocalizedString? detail, Confidence confidence)
    {
        RequireStatus(ResearchMissionStatus.Active);
        var finding = Finding.Create($"FND-{_findings.Count + 1:D3}", summary, detail, confidence);
        _findings.Add(finding);
        return finding;
    }

    public void UpdateFinding(Guid findingPublicId, LocalizedString summary, LocalizedString? detail, Confidence confidence)
    {
        RequireStatus(ResearchMissionStatus.Active);
        FindingOf(findingPublicId).Update(summary, detail, confidence);
    }

    public void VerifyFinding(Guid findingPublicId)
    {
        RequireStatus(ResearchMissionStatus.Active);
        FindingOf(findingPublicId).Verify();
    }

    // FR-113: capture a recommendation. Allowed only while the mission is Active. Key is a per-mission ordinal.
    public Recommendation AddRecommendation(LocalizedString statement, LocalizedString? rationale,
        RecommendationPriority priority, Guid? linkedTopicId)
    {
        RequireStatus(ResearchMissionStatus.Active);
        var recommendation = Recommendation.Create($"REC-{_recommendations.Count + 1:D3}", statement, rationale, priority, linkedTopicId);
        _recommendations.Add(recommendation);
        return recommendation;
    }

    public void UpdateRecommendation(Guid recommendationPublicId, LocalizedString statement, LocalizedString? rationale,
        RecommendationPriority priority, Guid? linkedTopicId)
    {
        RequireStatus(ResearchMissionStatus.Active);
        RecommendationOf(recommendationPublicId).Update(statement, rationale, priority, linkedTopicId);
    }

    public void SetRecommendationStatus(Guid recommendationPublicId, RecommendationStatus status)
    {
        RequireStatus(ResearchMissionStatus.Active);
        RecommendationOf(recommendationPublicId).SetStatus(status);
    }

    // P15c / W16: mark a recommendation Converted after it has been promoted to an execution Topic. Permitted
    // while Active or (typically) Completed — the mission's own content stays immutable; this records only the
    // recommendation's downstream disposition + successor topic. Not allowed once Cancelled.
    public void ConvertRecommendation(Guid recommendationPublicId, Guid topicId)
    {
        RequireStatus(ResearchMissionStatus.Active, ResearchMissionStatus.Completed);
        RecommendationOf(recommendationPublicId).MarkConverted(topicId);
    }

    private Finding FindingOf(Guid publicId) =>
        _findings.FirstOrDefault(f => f.PublicId == publicId)
        ?? throw new KeyNotFoundException("Finding not found on this mission.");

    private Recommendation RecommendationOf(Guid publicId) =>
        _recommendations.FirstOrDefault(r => r.PublicId == publicId)
        ?? throw new KeyNotFoundException("Recommendation not found on this mission.");

    private void RequireStatus(params ResearchMissionStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"This operation is not allowed while the mission is {Status}.");
    }
}
