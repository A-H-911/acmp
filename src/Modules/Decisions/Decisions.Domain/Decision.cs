using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Modules.Decisions.Domain.Events;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Decisions.Domain;

// The Decision aggregate root (docs/11 §Decisions, docs/12 §W12/W21) — the committee's recorded outcome
// on a topic. Identity to other modules is by id only (TopicId = Topic.PublicId; MeetingId =
// Meeting.PublicId; VoteId = the P9 ballot) — never an EF navigation, so the modular-monolith boundary
// holds (ADR-0001). The decisive immutability rule (AC-027): once Issued, NOTHING is editable — there are
// no public mutators for outcome/rationale/conditions, and re-issuing or superseding from the wrong state
// throws. A correction is a NEW decision that supersedes the prior one (W21, AC-028), never an edit.
public sealed class Decision : AuditableEntity
{
    private readonly List<DecisionCondition> _conditions = new();

    private Decision() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → API 409 (docs/16 §1.5, ADR-0018).
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty;   // DECN-YYYY-### (human-readable display key)
    public Guid TopicId { get; private set; }                  // Topic.PublicId
    public Guid? MeetingId { get; private set; }               // Meeting.PublicId (a decision may be recorded outside a meeting)
    public DecisionOutcome Outcome { get; private set; }
    public DecisionStatus Status { get; private set; }
    public LocalizedString Title { get; private set; } = null!; // short bilingual headline (design-faithful detail header)
    public LocalizedString Statement { get; private set; } = null!; // bilingual full-sentence "what was decided" (design: Decision statement block)
    public LocalizedString Rationale { get; private set; } = null!;
    public LocalizedString? Alternatives { get; private set; }
    public Guid? VoteId { get; private set; }                  // P9 ballot — nullable placeholder for now

    // Chair attribution recorded at issue time (ADR-0010 — decisions are attributed by name).
    public string? ChairApprovedByUserId { get; private set; } // Keycloak subject
    public string? ChairApprovedByName { get; private set; }   // display snapshot
    public bool ChairOverride { get; private set; }
    public LocalizedString? OverrideJustification { get; private set; }
    public DateTimeOffset? IssuedAt { get; private set; }

    // Supersession back-link (AC-028): the decision that replaced this one + why.
    public Guid? SupersededByDecisionId { get; private set; }
    public LocalizedString? SupersessionReason { get; private set; }

    public IReadOnlyCollection<DecisionCondition> Conditions => _conditions.AsReadOnly();

    // W12: draft a decision. Drafts are editable only by being replaced (no field setters) until issued.
    // Domain guards: a topic + rationale are required; a ConditionallyApproved outcome needs ≥1 condition.
    public static Decision Draft(string key, Guid topicId, Guid? meetingId, DecisionOutcome outcome,
        LocalizedString title, LocalizedString statement, LocalizedString rationale, LocalizedString? alternatives,
        Guid? voteId, IEnumerable<DecisionConditionInput> conditions, string actorSub, DateTimeOffset now)
    {
        if (topicId == Guid.Empty) throw new InvalidOperationException("A decision must reference a topic.");
        if (title is null) throw new InvalidOperationException("A decision title is required.");
        if (statement is null) throw new InvalidOperationException("A decision statement is required.");
        if (rationale is null) throw new InvalidOperationException("A decision rationale is required.");

        var decision = new Decision
        {
            Key = key.Trim(),
            TopicId = topicId,
            MeetingId = meetingId,
            Outcome = outcome,
            Status = DecisionStatus.Draft,
            Title = title,
            Statement = statement,
            Rationale = rationale,
            Alternatives = alternatives,
            VoteId = voteId,
        };

        foreach (var c in conditions ?? Enumerable.Empty<DecisionConditionInput>())
            decision._conditions.Add(new DecisionCondition(c.Text, c.DueDate));

        if (outcome == DecisionOutcome.ConditionallyApproved && decision._conditions.Count == 0)
            throw new InvalidOperationException("A conditionally-approved decision requires at least one condition.");

        decision.Raise(new DecisionDraftedEvent(decision.PublicId, decision.Key, topicId, now));
        return decision;
    }

    // W12: issue the decision (Chairman). Draft → Issued; immutable thereafter (AC-027). A chair override
    // (issuing against the vote, SoD-3) must carry a justification (AC-016). Re-issuing throws.
    public void Issue(string chairUserSub, string chairName, bool chairOverride,
        LocalizedString? overrideJustification, DateTimeOffset now)
    {
        RequireStatus(DecisionStatus.Draft);
        if (chairOverride && overrideJustification is null)
            throw new InvalidOperationException("A chair override requires a justification.");

        Status = DecisionStatus.Issued;
        IssuedAt = now;
        ChairApprovedByUserId = chairUserSub;
        ChairApprovedByName = chairName.Trim();
        ChairOverride = chairOverride;
        OverrideJustification = chairOverride ? overrideJustification : null;
        Raise(new DecisionIssuedEvent(PublicId, Key, TopicId, chairOverride, now));
    }

    // W21 (decision half): supersede an issued decision with its replacement. Issued → Superseded; the
    // back-link + reason are recorded immutably; the prior decision's content is left untouched (AC-028).
    public void Supersede(Guid supersededByDecisionId, LocalizedString reason, DateTimeOffset now)
    {
        RequireStatus(DecisionStatus.Issued);
        if (supersededByDecisionId == Guid.Empty)
            throw new InvalidOperationException("A superseding decision is required.");
        if (reason is null) throw new InvalidOperationException("A supersession reason is required.");

        Status = DecisionStatus.Superseded;
        SupersededByDecisionId = supersededByDecisionId;
        SupersessionReason = reason;
        Raise(new DecisionSupersededEvent(PublicId, Key, supersededByDecisionId, now));
    }

    private void RequireStatus(params DecisionStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"This operation is not allowed while the decision is {Status}.");
    }
}

// Input shape for drafting a condition (text + optional due date). Lives in the domain so the factory
// signature is stable; the application layer maps request data into it.
public sealed record DecisionConditionInput(LocalizedString Text, DateTimeOffset? DueDate);
