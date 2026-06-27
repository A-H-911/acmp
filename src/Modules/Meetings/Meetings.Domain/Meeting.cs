using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Domain.Events;
using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Meetings.Domain;

// The Meeting aggregate (docs/11 §C, docs/12 §5) — a scheduled committee session that anchors
// attendance and discussion. The Agenda is a sibling aggregate referenced by id (MeetingId), never an
// EF navigation, so the meeting-start use case can load both and flip topic state in one transaction
// without breaching the module/aggregate boundary (ADR-0001). Identity to other modules is by id +
// display snapshots (chair name, attendee names) — Meetings never reads Membership/Topics tables.
public sealed class Meeting : AuditableEntity
{
    private readonly List<Attendance> _attendees = new();
    private readonly List<Discussion> _discussions = new();

    private Meeting() { }

    public string Key { get; private set; } = string.Empty;   // MTG-YYYY-### (human-readable display key)
    public string Title { get; private set; } = string.Empty;
    public Guid CommitteeId { get; private set; }
    public DateTimeOffset ScheduledStart { get; private set; }
    public DateTimeOffset ScheduledEnd { get; private set; }
    public MeetingStatus Status { get; private set; }
    public string? Location { get; private set; }
    public string? JoinUrl { get; private set; }
    public Guid ChairUserId { get; private set; }             // CommitteeMember.PublicId
    public string ChairName { get; private set; } = string.Empty; // display snapshot
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? HeldAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<Attendance> Attendees => _attendees.AsReadOnly();
    public IReadOnlyCollection<Discussion> Discussions => _discussions.AsReadOnly();

    // Present + Late count toward quorum; the required count is a query concern (read from the
    // committee quorum policy in the application layer), never hard-coded here.
    public int PresentCount => _attendees.Count(a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late);

    // W5: schedule a new meeting. Raises MeetingScheduled → the handler notifies committee members.
    public static Meeting Schedule(string key, string title, Guid committeeId,
        Guid chairUserId, string chairName,
        DateTimeOffset scheduledStart, DateTimeOffset scheduledEnd,
        string? location, string? joinUrl, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("A meeting title is required.");
        if (scheduledEnd <= scheduledStart) throw new InvalidOperationException("Meeting end must be after its start.");
        if (chairUserId == Guid.Empty) throw new InvalidOperationException("A chair must be assigned to the meeting.");

        var meeting = new Meeting
        {
            Key = key.Trim(),
            Title = title.Trim(),
            CommitteeId = committeeId,
            ChairUserId = chairUserId,
            ChairName = chairName.Trim(),
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledEnd,
            Location = Clean(location),
            JoinUrl = Clean(joinUrl),
            Status = MeetingStatus.Scheduled,
        };
        meeting.Raise(new MeetingScheduledEvent(meeting.PublicId, meeting.Key, scheduledStart, now));
        return meeting;
    }

    // W7: start the meeting (the caller also locks the agenda and moves each topic InCommittee).
    public void Start(DateTimeOffset now)
    {
        RequireStatus(MeetingStatus.Scheduled);
        Status = MeetingStatus.InProgress;
        StartedAt = now;
        Raise(new MeetingStartedEvent(PublicId, Key, now));
    }

    // W7: conclude the meeting.
    public void Hold(DateTimeOffset now)
    {
        RequireStatus(MeetingStatus.InProgress);
        Status = MeetingStatus.Held;
        HeldAt = now;
        Raise(new MeetingHeldEvent(PublicId, Key, now));
    }

    // W5: cancel a scheduled meeting (reason mandatory; participants notified by the handler).
    public void Cancel(string reason, DateTimeOffset now)
    {
        RequireStatus(MeetingStatus.Scheduled);
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A cancellation reason is required.");
        Status = MeetingStatus.Cancelled;
        CancelledAt = now;
        CancellationReason = reason.Trim();
        Raise(new MeetingCancelledEvent(PublicId, Key, reason.Trim(), now));
    }

    // ---- attendance (W8) ----

    // Seed the roster (idempotent) — names/roles come from the application layer (Membership contract).
    public Attendance SeedAttendee(Guid userId, string name, AttendanceRole role, bool isVotingEligible)
    {
        var existing = _attendees.FirstOrDefault(a => a.UserId == userId);
        if (existing is not null) return existing;
        var attendee = new Attendance(userId, name, role, isVotingEligible);
        _attendees.Add(attendee);
        return attendee;
    }

    // Mark presence/apologies. Allowed from Scheduled (pre-mark apologies) and InProgress (live roll-call).
    public void MarkAttendance(Guid userId, AttendanceStatus status, DateTimeOffset now)
    {
        RequireStatus(MeetingStatus.Scheduled, MeetingStatus.InProgress);
        var attendee = _attendees.FirstOrDefault(a => a.UserId == userId)
            ?? throw new InvalidOperationException("This participant is not on the attendance roster.");
        attendee.Mark(status, now);
    }

    // ---- discussion (W9) ----

    // Capture/curate the single Human note for an agenda topic (upsert — the workspace autosaves).
    public Discussion SetDiscussionNote(Guid topicId, string body, string authorSub, string authorName, DateTimeOffset now)
    {
        RequireStatus(MeetingStatus.InProgress);
        var existing = _discussions.FirstOrDefault(d => d.TopicId == topicId && d.Origin == DiscussionOrigin.Human);
        if (existing is not null)
        {
            existing.UpdateBody(body, now);
            return existing;
        }
        var discussion = new Discussion(topicId, body, authorSub, authorName, DiscussionOrigin.Human, isApproved: true, now);
        _discussions.Add(discussion);
        return discussion;
    }

    private void RequireStatus(params MeetingStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"This operation is not allowed while the meeting is {Status}.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
