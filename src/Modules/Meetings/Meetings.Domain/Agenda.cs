using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Domain.Events;
using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Meetings.Domain;

// The Agenda aggregate (docs/domain/domain-model.md §C, W6) — the ordered, time-boxed set of topics planned for a meeting.
// Belongs to one Meeting by id (MeetingId), never an EF navigation (ADR-0001). Items are editable while
// Draft/Published; the agenda Locks at meeting start and Closes when the meeting is held. Publishing is
// versioned and re-publishable. Topic display data is snapshotted onto each item at add time.
public sealed class Agenda : AuditableEntity
{
    private readonly List<AgendaItem> _items = new();

    private Agenda() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → API 409 (docs/domain/data-architecture.md §1.5, ADR-0018).
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty;  // AGN-YYYY-### (human-readable display key)
    public Guid MeetingId { get; private set; }
    public AgendaStatus Status { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public IReadOnlyCollection<AgendaItem> Items => _items.OrderBy(i => i.Order).ToList().AsReadOnly();

    public int TotalTimeboxMinutes => _items.Sum(i => i.TimeboxMinutes);

    public static Agenda Draft(string key, Guid meetingId)
    {
        if (meetingId == Guid.Empty) throw new InvalidOperationException("An agenda must belong to a meeting.");
        return new Agenda { Key = key.Trim(), MeetingId = meetingId, Status = AgendaStatus.Draft, Version = 0 };
    }

    // W6: place a prepared topic onto the agenda (no duplicates). Display data is snapshotted here.
    public AgendaItem AddItem(Guid topicId, string topicKey, string topicTitle, bool urgent,
        int timeboxMinutes, Guid? presenterUserId, string? presenterName)
    {
        RequireEditable();
        if (_items.Any(i => i.TopicId == topicId))
            throw new InvalidOperationException("This topic is already on the agenda.");
        var order = _items.Count == 0 ? 1 : _items.Max(i => i.Order) + 1;
        var item = new AgendaItem(topicId, topicKey, topicTitle, urgent, order, timeboxMinutes, presenterUserId, presenterName);
        _items.Add(item);
        return item;
    }

    public void RemoveItem(Guid topicId)
    {
        RequireEditable();
        _items.Remove(Find(topicId));
        Renumber();
    }

    // W6 / AC-044: keyboard move-up(-1)/move-down(+1) and pointer drag both route here. No-op at the ends.
    public void MoveItem(Guid topicId, int delta)
    {
        RequireEditable();
        var ordered = _items.OrderBy(i => i.Order).ToList();
        var index = ordered.FindIndex(i => i.TopicId == topicId);
        if (index < 0) throw new InvalidOperationException("Agenda item not found.");
        var target = index + delta;
        if (target < 0 || target >= ordered.Count) return;
        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        for (var i = 0; i < ordered.Count; i++) ordered[i].SetOrder(i + 1);
    }

    public void SetTimebox(Guid topicId, int minutes)
    {
        RequireEditable();
        Find(topicId).SetTimebox(minutes);
    }

    public void AssignPresenter(Guid topicId, Guid presenterUserId, string presenterName)
    {
        RequireEditable();
        Find(topicId).AssignPresenter(presenterUserId, presenterName);
    }

    // W6: publish (or re-publish) the agenda. Versioned; raises AgendaPublished → topics flip to
    // Scheduled and committee members are notified by the handler (AC-051).
    public void Publish(DateTimeOffset now)
    {
        if (Status is not (AgendaStatus.Draft or AgendaStatus.Published))
            throw new InvalidOperationException($"An agenda cannot be published while {Status}.");
        if (_items.Count == 0)
            throw new InvalidOperationException("Add at least one item before publishing the agenda.");
        if (_items.Any(i => i.PresenterUserId is null))
            throw new InvalidOperationException("Every agenda item needs a presenter before publishing.");
        Status = AgendaStatus.Published;
        Version += 1;
        PublishedAt = now;
        Raise(new AgendaPublishedEvent(PublicId, MeetingId, Key, _items.Count, Version, now));
    }

    // W7: lock at meeting start; close when the meeting is held.
    public void Lock()
    {
        RequireStatus(AgendaStatus.Published);
        Status = AgendaStatus.Locked;
    }

    public void Close()
    {
        RequireStatus(AgendaStatus.Locked);
        Status = AgendaStatus.Closed;
    }

    // W7: during the live meeting, record per-item actual time spent and the discussion outcome.
    public void RecordActualMinutes(Guid topicId, int minutes)
    {
        RequireStatus(AgendaStatus.Locked);
        Find(topicId).RecordActualMinutes(minutes);
    }

    public void SetOutcome(Guid topicId, AgendaItemOutcome outcome)
    {
        RequireStatus(AgendaStatus.Locked);
        Find(topicId).SetOutcome(outcome);
    }

    private AgendaItem Find(Guid topicId) =>
        _items.FirstOrDefault(i => i.TopicId == topicId)
        ?? throw new InvalidOperationException("Agenda item not found.");

    private void Renumber()
    {
        var ordered = _items.OrderBy(i => i.Order).ToList();
        for (var i = 0; i < ordered.Count; i++) ordered[i].SetOrder(i + 1);
    }

    private void RequireEditable()
    {
        if (Status is not (AgendaStatus.Draft or AgendaStatus.Published))
            throw new InvalidOperationException($"An agenda cannot be edited while {Status}.");
    }

    private void RequireStatus(params AgendaStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"This operation is not allowed while the agenda is {Status}.");
    }
}
