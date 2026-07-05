using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Domain.Events;
using Acmp.Shared.Authorization.Abac;
using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Topics.Domain;

// The governed Topic aggregate — the heart of the core loop (intake → backlog → agenda → decision).
// One state machine (docs/domain/entity-lifecycles.md §1); "TopicRequest" is just its pre-Accepted projection. Identity to other
// modules is by stable key only: actor/author/submitter = the Keycloak subject (matches the audit log
// and ABAC resolvers); Owner = the assigned CommitteeMember.PublicId + a name snapshot for display.
// Never an EF navigation to another module, so the modular-monolith boundary holds (ADR-0001).
// Implements the shared ABAC contracts so the platform authorization handlers can scope writes by
// stream and by per-topic ownership (docs/domain/permission-role-matrix.md §E).
public sealed class Topic : AuditableEntity, IStreamScopedResource, ITopicScopedResource
{
    private readonly List<string> _streams = new();
    private readonly List<string> _systems = new();
    private readonly List<string> _tags = new();
    private readonly List<TopicAttachment> _attachments = new();
    private readonly List<TopicComment> _comments = new();
    private readonly List<TopicStatusEvent> _history = new();

    private Topic() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → API 409 (docs/domain/data-architecture.md §1.5, ADR-0018).
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty; // TOP-YYYY-### (human-readable display key)
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Justification { get; private set; } = string.Empty;
    public TopicType Type { get; private set; }
    public TopicUrgency Urgency { get; private set; }
    public TopicScope Scope { get; private set; }
    public TopicSource Source { get; private set; }
    public TopicStatus Status { get; private set; }
    public int Priority { get; private set; }
    // How many times this topic has been Deferred (W20). Governance signal for the chairman dashboard's
    // "Deferred ≥2×" list (AC-066) — a topic bouncing off the agenda repeatedly needs attention.
    public int TimesDeferred { get; private set; }
    public string SubmittedBySub { get; private set; } = string.Empty;
    public string SubmittedByName { get; private set; } = string.Empty;
    public Guid? OwnerId { get; private set; }            // CommitteeMember.PublicId
    public string? OwnerName { get; private set; }        // display snapshot
    public DateTimeOffset? RevisitOn { get; private set; }

    public IReadOnlyCollection<string> Systems => _systems.AsReadOnly();
    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();
    public IReadOnlyCollection<TopicAttachment> Attachments => _attachments.AsReadOnly();
    public IReadOnlyCollection<TopicComment> Comments => _comments.AsReadOnly();
    public IReadOnlyCollection<TopicStatusEvent> History => _history.AsReadOnly();

    // ABAC contracts (docs/domain/permission-role-matrix.md §E): write access is bounded by these.
    public IReadOnlyCollection<string> AffectedStreams => _streams.AsReadOnly();
    Guid ITopicScopedResource.TopicId => PublicId;

    // Factory — creates a Draft. Drafts may be incomplete (the form autosaves as the user types);
    // completeness is enforced at Submit (AC-030). Submitter attribution is fixed here.
    public static Topic Draft(string key, string title, string description, string justification,
        TopicType type, TopicUrgency urgency, TopicSource source,
        string submittedBySub, string submittedByName,
        IEnumerable<string> streams, IEnumerable<string> systems, IEnumerable<string> tags)
    {
        var topic = new Topic
        {
            Key = key.Trim(),
            Title = title.Trim(),
            Description = description.Trim(),
            Justification = justification.Trim(),
            Type = type,
            Urgency = urgency,
            Source = source,
            Scope = TopicScope.SingleStream,
            Status = TopicStatus.Draft,
            SubmittedBySub = submittedBySub.Trim(),
            SubmittedByName = submittedByName.Trim(),
        };
        topic.ReplaceStrings(topic._streams, streams);
        topic.ReplaceStrings(topic._systems, systems);
        topic.ReplaceStrings(topic._tags, tags);
        return topic;
    }

    // ---- lifecycle transitions (docs/domain/entity-lifecycles.md §1) ----

    // W1: submit for triage. Enforces the AC-030 required set; the stricter design-required fields
    // (justification) are enforced by the SubmitTopic validator at the application boundary.
    public void Submit(DateTimeOffset now)
    {
        RequireStatus(TopicStatus.Draft);
        if (string.IsNullOrWhiteSpace(Title)) throw new InvalidOperationException("Title is required to submit.");
        if (string.IsNullOrWhiteSpace(Description)) throw new InvalidOperationException("Description is required to submit.");
        if (_streams.Count == 0) throw new InvalidOperationException("At least one affected stream is required to submit.");
        Scope = DeriveScope();
        Transition(TopicStatus.Submitted, null, SubmittedBySub, SubmittedByName, now);
        Raise(new TopicSubmittedEvent(PublicId, Key, now));
    }

    // W2: Secretary picks the submission up for triage.
    public void BeginTriage(string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(TopicStatus.Submitted, TopicStatus.Reopened);
        Transition(TopicStatus.Triage, null, actorSub, actorName, now);
        Raise(new TopicTriagedEvent(PublicId, Key, now));
    }

    // W2: accept into the backlog and assign an Owner (CommitteeMember.PublicId + name snapshot).
    public void Accept(Guid ownerId, string ownerName, string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(TopicStatus.Triage);
        if (ownerId == Guid.Empty) throw new InvalidOperationException("An owner must be assigned on accept.");
        OwnerId = ownerId;
        OwnerName = ownerName.Trim();
        Transition(TopicStatus.Accepted, null, actorSub, actorName, now);
        Raise(new TopicAcceptedEvent(PublicId, Key, ownerId, now));
    }

    // W20: reject with a mandatory rationale (AC-031). The history row is immutable (AC-032, AC-033).
    public void Reject(string reason, string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(TopicStatus.Submitted, TopicStatus.Triage);
        RequireReason(reason, "A rejection reason is required.");
        Transition(TopicStatus.Rejected, reason, actorSub, actorName, now);
        Raise(new TopicRejectedEvent(PublicId, Key, reason.Trim(), now));
    }

    // W20: defer with a mandatory reason and an optional revisit date.
    public void Defer(string reason, DateTimeOffset? revisitOn, string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(TopicStatus.Triage, TopicStatus.Accepted, TopicStatus.Scheduled, TopicStatus.InCommittee);
        RequireReason(reason, "A defer reason is required.");
        RevisitOn = revisitOn;
        TimesDeferred++;
        Transition(TopicStatus.Deferred, reason, actorSub, actorName, now);
        Raise(new TopicDeferredEvent(PublicId, Key, reason.Trim(), revisitOn, now));
    }

    public void Reactivate(string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(TopicStatus.Deferred);
        Transition(TopicStatus.Triage, null, actorSub, actorName, now);
        Raise(new TopicTriagedEvent(PublicId, Key, now));
    }

    // W4: mark prepared once the Owner has completed preparation materials (AC-035).
    public void MarkPrepared(string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(TopicStatus.Accepted);
        Transition(TopicStatus.Prepared, null, actorSub, actorName, now);
        Raise(new TopicPreparedEvent(PublicId, Key, now));
    }

    public void Reopen(string justification, string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(TopicStatus.Rejected, TopicStatus.Closed);
        RequireReason(justification, "A reopen justification is required.");
        Transition(TopicStatus.Reopened, justification, actorSub, actorName, now);
        Raise(new TopicReopenedEvent(PublicId, Key, justification.Trim(), now));
    }

    // W5/W6 (caller lands in P6): place onto a published agenda.
    public void Schedule(Guid meetingId, string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(TopicStatus.Prepared);
        if (meetingId == Guid.Empty) throw new InvalidOperationException("A meeting is required to schedule.");
        Transition(TopicStatus.Scheduled, null, actorSub, actorName, now);
        Raise(new TopicScheduledEvent(PublicId, Key, meetingId, now));
    }

    public void EnterCommittee(string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(TopicStatus.Scheduled);
        Transition(TopicStatus.InCommittee, null, actorSub, actorName, now);
    }

    public void Decide(string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(TopicStatus.InCommittee);
        Transition(TopicStatus.Decided, null, actorSub, actorName, now);
        Raise(new TopicDecidedEvent(PublicId, Key, now));
    }

    public void Close(string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(TopicStatus.Decided);
        Transition(TopicStatus.Closed, null, actorSub, actorName, now);
        Raise(new TopicClosedEvent(PublicId, Key, now));
    }

    public void Convert(string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(TopicStatus.Decided);
        Transition(TopicStatus.Converted, null, actorSub, actorName, now);
        Raise(new TopicConvertedEvent(PublicId, Key, now));
    }

    // ---- edits (field-level lock rules, docs/domain/entity-lifecycles.md §1, AC-034) ----

    // Content (title/description/justification) is editable only before Acceptance — locked thereafter to
    // prevent retroactive modification (AC-034). The *who* (Owner vs Secretary) is enforced by ABAC.
    public void UpdateContent(string title, string description, string justification)
    {
        RequireStatus(TopicStatus.Draft, TopicStatus.Submitted, TopicStatus.Triage, TopicStatus.Reopened);
        if (!string.IsNullOrWhiteSpace(title)) Title = title.Trim();
        if (!string.IsNullOrWhiteSpace(description)) Description = description.Trim();
        Justification = justification.Trim();
    }

    // Reclassification during triage (W2). Type/source classify the work; allowed pre-Accept.
    public void Reclassify(TopicType type, TopicSource source)
    {
        RequireStatus(TopicStatus.Draft, TopicStatus.Submitted, TopicStatus.Triage, TopicStatus.Reopened);
        Type = type;
        Source = source;
    }

    // Metadata (streams/systems/tags/urgency/scope) stays editable post-Accept until Decided (AC-034).
    public void AssignStreams(IEnumerable<string> streams) { EnsureMutable(); ReplaceStrings(_streams, streams); }
    public void AssignSystems(IEnumerable<string> systems) { EnsureMutable(); ReplaceStrings(_systems, systems); }
    public void SetTags(IEnumerable<string> tags) { EnsureMutable(); ReplaceStrings(_tags, tags); }
    public void SetUrgency(TopicUrgency urgency) { EnsureMutable(); Urgency = urgency; }
    public void SetScope(TopicScope scope) { EnsureMutable(); Scope = scope; }

    // W3: backlog prioritization ordinal.
    public void SetPriority(int priority, DateTimeOffset now)
    {
        EnsureMutable();
        Priority = priority;
        Raise(new TopicPriorityChangedEvent(PublicId, Key, priority, now));
    }

    public void AddComment(string body, string authorSub, string authorName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(body)) throw new InvalidOperationException("A comment cannot be empty.");
        _comments.Add(new TopicComment(body, authorSub, authorName, now));
    }

    public TopicAttachment AddAttachment(string fileName, string contentType, long sizeBytes,
        string storageKey, string uploadedBySub, string uploadedByName, DateTimeOffset now)
    {
        EnsureMutable();
        var attachment = new TopicAttachment(fileName, contentType, sizeBytes, storageKey, uploadedBySub, uploadedByName, now);
        _attachments.Add(attachment);
        return attachment;
    }

    // ---- helpers ----

    private void Transition(TopicStatus to, string? reason, string actorSub, string actorName, DateTimeOffset now)
    {
        _history.Add(new TopicStatusEvent(Status, to, reason, actorSub, actorName, now));
        Status = to;
    }

    private void RequireStatus(params TopicStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"This operation is not allowed while the topic is {Status}.");
    }

    private static void RequireReason(string reason, string message)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(message);
    }

    // Field edits are blocked once Decided (docs/domain/entity-lifecycles.md §1): Decided/Closed/Converted are immutable.
    private void EnsureMutable()
    {
        if (Status is TopicStatus.Decided or TopicStatus.Closed or TopicStatus.Converted)
            throw new InvalidOperationException($"A {Status} topic is immutable; supersede the linked decision instead.");
    }

    private TopicScope DeriveScope() =>
        Scope is TopicScope.Platform or TopicScope.OrgWide
            ? Scope
            : _streams.Count >= 2 ? TopicScope.MultiStream : TopicScope.SingleStream;

    private void ReplaceStrings(List<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var v in values.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            target.Add(v);
    }
}
