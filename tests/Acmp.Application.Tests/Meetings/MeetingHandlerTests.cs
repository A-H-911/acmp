using Acmp.Modules.Meetings.Application.Features.AgendaBuilder;
using Acmp.Modules.Meetings.Application.Features.CancelMeeting;
using Acmp.Modules.Meetings.Application.Features.ConductMeeting;
using Acmp.Modules.Meetings.Application.Features.GetMeetingDetail;
using Acmp.Modules.Meetings.Application.Features.GetMeetings;
using Acmp.Modules.Meetings.Application.Features.PublishAgenda;
using Acmp.Modules.Meetings.Application.Features.ScheduleMeeting;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Meetings;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Contracts.Topics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Meetings;

// Round-trips through the real MeetingsDbContext (InMemory): EF mapping for the owned agenda-item,
// attendance, and discussion child tables; the key generator; and the W5–W9 command flow. The
// cross-module ITopicScheduler is faked so we assert Meetings asks Topics to advance state (it never
// touches Topics' tables itself — ADR-0001).
public class MeetingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 18, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Chair = Guid.NewGuid();
    private static readonly Guid Presenter = Guid.NewGuid();

    private sealed class FakeScheduler : ITopicScheduler
    {
        public List<Guid> Scheduled { get; } = new();
        public List<Guid> Entered { get; } = new();
        public Task ScheduleAsync(Guid topicId, Guid meetingId, CancellationToken ct = default) { Scheduled.Add(topicId); return Task.CompletedTask; }
        public Task EnterCommitteeAsync(Guid topicId, CancellationToken ct = default) { Entered.Add(topicId); return Task.CompletedTask; }
    }

    private static MeetingsDbContext NewDb(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<MeetingsDbContext>().UseInMemoryDatabase("meetings-" + Guid.NewGuid()).Options, clock, user);

    private static ICurrentUser User(string sub = "kc-khalid", string name = "Khalid A.")
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns(sub);
        u.DisplayName.Returns(name);
        return u;
    }

    private static IClock Clock(DateTimeOffset now)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(now);
        return c;
    }

    // Active committee roster the notification fan-out delivers to (defaults to two members).
    private static ICommitteeDirectory Dir(params string[] subs)
    {
        var members = (subs.Length == 0 ? new[] { "kc-a", "kc-b" } : subs)
            .Select(s => new CommitteeRecipient(s, s)).ToList();
        var d = Substitute.For<ICommitteeDirectory>();
        d.GetActiveMembersAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyCollection<CommitteeRecipient>)members);
        return d;
    }

    // Captures every message the handlers fan out, so we can assert the AC-051 content contract.
    private sealed class RecordingChannel : INotificationChannel
    {
        public List<NotificationMessage> Sent { get; } = new();
        public Task PublishAsync(NotificationMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static INotificationChannel NoNotify() => Substitute.For<INotificationChannel>();

    private static ScheduleMeetingCommand ScheduleCmd() => new(
        "Weekly Architecture Committee", Chair, "S. M.", Now, Now.AddMinutes(90),
        MeetingType.Regular, MeetingMode.InPerson, null, null);

    // Schedules a meeting and returns (db, meetingId).
    private static async Task<(MeetingsDbContext Db, Guid MeetingId)> ScheduledMeetingAsync(ICurrentUser user, IClock clock)
    {
        var db = NewDb(user, clock);
        var summary = await new ScheduleMeetingHandler(db, new MeetingKeyGenerator(db), user, clock, Substitute.For<IAuditSink>(), Dir(), NoNotify(), Substitute.For<IWebexMeetingProvisioner>())
            .Handle(ScheduleCmd(), CancellationToken.None);
        return (db, summary.Id);
    }

    [Fact]
    public async Task Schedule_creates_meeting_with_key_and_a_draft_agenda()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = NewDb(user, clock);

        var summary = await new ScheduleMeetingHandler(db, new MeetingKeyGenerator(db), user, clock, Substitute.For<IAuditSink>(), Dir(), NoNotify(), Substitute.For<IWebexMeetingProvisioner>())
            .Handle(ScheduleCmd(), CancellationToken.None);

        summary.Key.Should().Be("MTG-2026-001");
        summary.Status.Should().Be("Scheduled");
        var agenda = await db.Agendas.SingleAsync();
        agenda.Key.Should().Be("AGN-2026-001");
        agenda.Status.Should().Be(AgendaStatus.Draft);
        agenda.MeetingId.Should().Be(summary.Id);
    }

    [Fact]
    public async Task Agenda_builder_adds_reorders_timeboxes_and_assigns_presenter()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);
        await using var _ = db;
        var t1 = Guid.NewGuid(); var t2 = Guid.NewGuid();

        await new AddAgendaItemHandler(db).Handle(new AddAgendaItemCommand(meetingId, t1, "TOP-2026-001", "A", false, 15, Presenter, "Omar H."), default);
        var afterAdd = await new AddAgendaItemHandler(db).Handle(new AddAgendaItemCommand(meetingId, t2, "TOP-2026-002", "B", true, 20, Presenter, "Omar H."), default);
        afterAdd.Items.Should().HaveCount(2);

        var moved = await new MoveAgendaItemHandler(db).Handle(new MoveAgendaItemCommand(meetingId, t1, +1), default);
        moved.Items.Select(i => i.TopicTitle).Should().ContainInOrder("B", "A");

        var timeboxed = await new SetAgendaItemTimeboxHandler(db).Handle(new SetAgendaItemTimeboxCommand(meetingId, t1, 30), default);
        timeboxed.Items.Single(i => i.TopicId == t1).TimeboxMinutes.Should().Be(30);

        var newPresenter = Guid.NewGuid();
        var assigned = await new AssignPresenterHandler(db).Handle(new AssignPresenterCommand(meetingId, t1, newPresenter, "Noura P."), default);
        assigned.Items.Single(i => i.TopicId == t1).PresenterName.Should().Be("Noura P.");
    }

    [Fact]
    public async Task Publish_publishes_the_agenda_and_schedules_each_topic_via_the_seam()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);
        await using var _ = db;
        var topic = Guid.NewGuid();
        await new AddAgendaItemHandler(db).Handle(new AddAgendaItemCommand(meetingId, topic, "TOP-2026-001", "A", false, 15, Presenter, "Omar H."), default);
        var scheduler = new FakeScheduler();

        var agenda = await new PublishAgendaHandler(db, scheduler, user, clock, Substitute.For<IAuditSink>(), Dir(), NoNotify())
            .Handle(new PublishAgendaCommand(meetingId), default);

        agenda.Status.Should().Be("Published");
        agenda.Version.Should().Be(1);
        scheduler.Scheduled.Should().ContainSingle().Which.Should().Be(topic);
    }

    [Fact] // AC-051: publishing fans out one in-app notification per active committee member, each
           // carrying the meeting date, the agenda title, and a deep link to the agenda view.
    public async Task Publish_notifies_every_committee_member_with_date_title_and_deeplink()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);
        await using var _ = db;
        await new AddAgendaItemHandler(db).Handle(new AddAgendaItemCommand(meetingId, Guid.NewGuid(), "TOP-2026-001", "A", false, 15, Presenter, "Omar H."), default);
        var channel = new RecordingChannel();

        await new PublishAgendaHandler(db, new FakeScheduler(), user, clock, Substitute.For<IAuditSink>(), Dir("kc-a", "kc-b"), channel)
            .Handle(new PublishAgendaCommand(meetingId), default);

        channel.Sent.Should().HaveCount(2);
        channel.Sent.Select(m => m.RecipientUserId).Should().BeEquivalentTo(new[] { "kc-a", "kc-b" });
        channel.Sent.Should().OnlyContain(m =>
            m.Category == "AgendaPublished" &&
            m.DeepLink == "/meetings/MTG-2026-001" &&
            m.Body.En.Contains("Weekly Architecture Committee") && m.Body.En.Contains("2026-02-18") &&
            m.Body.Ar.Contains("Weekly Architecture Committee") && m.Body.Ar.Contains("2026-02-18"));
    }

    [Fact]
    public async Task Start_locks_the_agenda_and_moves_each_topic_into_committee()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);
        await using var _ = db;
        var topic = Guid.NewGuid();
        await new AddAgendaItemHandler(db).Handle(new AddAgendaItemCommand(meetingId, topic, "TOP-2026-001", "A", false, 15, Presenter, "Omar H."), default);
        var scheduler = new FakeScheduler();
        await new PublishAgendaHandler(db, scheduler, user, clock, Substitute.For<IAuditSink>(), Dir(), NoNotify()).Handle(new PublishAgendaCommand(meetingId), default);

        await new StartMeetingHandler(db, scheduler, user, clock, Substitute.For<IAuditSink>()).Handle(new StartMeetingCommand(meetingId), default);

        var detail = await new GetMeetingDetailHandler(db).Handle(new GetMeetingDetailQuery("MTG-2026-001"), default);
        detail!.Status.Should().Be("InProgress");
        detail.Agenda!.Status.Should().Be("Locked");
        scheduler.Entered.Should().ContainSingle().Which.Should().Be(topic);
    }

    [Fact]
    public async Task Attendance_is_seeded_on_first_touch_then_marked()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);
        await using var _ = db;
        var scheduler = new FakeScheduler();
        // A meeting can only start once its agenda is published (W7), which needs ≥1 item.
        await new AddAgendaItemHandler(db).Handle(new AddAgendaItemCommand(meetingId, Guid.NewGuid(), "TOP-2026-001", "A", false, 15, Presenter, "Omar H."), default);
        await new PublishAgendaHandler(db, scheduler, user, clock, Substitute.For<IAuditSink>(), Dir(), NoNotify()).Handle(new PublishAgendaCommand(meetingId), default);
        await new StartMeetingHandler(db, scheduler, user, clock, Substitute.For<IAuditSink>()).Handle(new StartMeetingCommand(meetingId), default);
        var member = Guid.NewGuid();

        await new MarkAttendanceHandler(db, clock, Substitute.For<IAuditSink>(), user)
            .Handle(new MarkAttendanceCommand(meetingId, member, "Omar H.", AttendanceRole.Member, AttendanceStatus.Present, true), default);

        var detail = await new GetMeetingDetailHandler(db).Handle(new GetMeetingDetailQuery("MTG-2026-001"), default);
        detail!.Attendance.Should().ContainSingle(a => a.UserId == member && a.Status == "Present");
    }

    [Fact]
    public async Task Discussion_note_and_actual_time_are_captured()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);
        await using var _ = db;
        var topic = Guid.NewGuid();
        await new AddAgendaItemHandler(db).Handle(new AddAgendaItemCommand(meetingId, topic, "TOP-2026-001", "A", false, 20, Presenter, "Omar H."), default);
        var scheduler = new FakeScheduler();
        await new PublishAgendaHandler(db, scheduler, user, clock, Substitute.For<IAuditSink>(), Dir(), NoNotify()).Handle(new PublishAgendaCommand(meetingId), default);
        await new StartMeetingHandler(db, scheduler, user, clock, Substitute.For<IAuditSink>()).Handle(new StartMeetingCommand(meetingId), default);

        await new CaptureDiscussionHandler(db, clock, user).Handle(new CaptureDiscussionCommand(meetingId, topic, "Consensus on direction."), default);
        await new RecordActualTimeHandler(db).Handle(new RecordActualTimeCommand(meetingId, topic, 12, AgendaItemOutcome.Discussed), default);

        var detail = await new GetMeetingDetailHandler(db).Handle(new GetMeetingDetailQuery("MTG-2026-001"), default);
        detail!.Discussions.Should().ContainSingle(d => d.TopicId == topic && d.Body == "Consensus on direction.");
        detail.Agenda!.Items.Single().ActualMinutes.Should().Be(12);
        detail.Agenda.Items.Single().Outcome.Should().Be("Discussed");
    }

    [Fact]
    public async Task GetMeetings_lists_with_item_count()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);
        await using var _ = db;
        await new AddAgendaItemHandler(db).Handle(new AddAgendaItemCommand(meetingId, Guid.NewGuid(), "TOP-2026-001", "A", false, 15, Presenter, "Omar H."), default);

        var list = await new GetMeetingsHandler(db).Handle(new GetMeetingsQuery(), default);
        list.Should().ContainSingle();
        list[0].ItemCount.Should().Be(1);

        // The query record's synthesized copy-constructor (the decl line the coverage basis counts).
        (new GetMeetingsQuery() with { }).AllowedRoles.Should().BeEmpty();
    }

    // ───────────────────────── S1 adversarial: conduct/cancel/agenda-edit (ADR-0016) ─────────────────────────
    // The role-gate on these commands is the MediatR AuthorizationBehavior (IAuthorizedRequest), which the
    // direct-handler construction here deliberately bypasses — so authz-denial belongs to the pipeline test,
    // not this layer. What IS assertable at the handler: 404 lookups, domain status guards, and audit emission.

    // A meeting walked to InProgress with a Locked, single-item agenda (the live-meeting starting point).
    private static async Task<(MeetingsDbContext Db, Guid MeetingId, Guid TopicId)> StartedMeetingAsync(ICurrentUser user, IClock clock)
    {
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);
        var topic = Guid.NewGuid();
        await new AddAgendaItemHandler(db).Handle(new AddAgendaItemCommand(meetingId, topic, "TOP-2026-001", "A", false, 15, Presenter, "Omar H."), default);
        var scheduler = new FakeScheduler();
        await new PublishAgendaHandler(db, scheduler, user, clock, Substitute.For<IAuditSink>(), Dir(), NoNotify()).Handle(new PublishAgendaCommand(meetingId), default);
        await new StartMeetingHandler(db, scheduler, user, clock, Substitute.For<IAuditSink>()).Handle(new StartMeetingCommand(meetingId), default);
        return (db, meetingId, topic);
    }

    // ---- EndMeetingHandler (W7) ----

    [Fact]
    public async Task End_throws_not_found_when_the_meeting_is_missing()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = NewDb(user, clock);

        var act = () => new EndMeetingHandler(db, clock, Substitute.For<IAuditSink>(), user)
            .Handle(new EndMeetingCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("Meeting not found.");
    }

    [Fact]
    public async Task End_throws_not_found_when_the_meeting_has_no_agenda()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = NewDb(user, clock);
        // A meeting persisted directly (no sibling agenda) — exercises the second 404 branch.
        var meeting = Meeting.Schedule("MTG-2026-009", "Orphan", Meeting.SingleCommitteeId, Chair, "S. M.",
            Now, Now.AddMinutes(60), MeetingType.Regular, MeetingMode.InPerson, null, null, Now);
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var act = () => new EndMeetingHandler(db, clock, Substitute.For<IAuditSink>(), user)
            .Handle(new EndMeetingCommand(meeting.PublicId), default);

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("Agenda not found*");
    }

    [Fact]
    public async Task End_on_a_meeting_that_never_started_trips_the_domain_guard()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);   // Scheduled, not InProgress
        await using var _ = db;

        var act = () => new EndMeetingHandler(db, clock, Substitute.For<IAuditSink>(), user)
            .Handle(new EndMeetingCommand(meetingId), default);

        await act.Should().ThrowAsync<InvalidOperationException>();        // Hold requires InProgress
    }

    [Fact]
    public async Task End_holds_the_meeting_closes_the_agenda_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId, _) = await StartedMeetingAsync(user, clock);
        await using var _ = db;
        var audit = Substitute.For<IAuditSink>();

        await new EndMeetingHandler(db, clock, audit, user).Handle(new EndMeetingCommand(meetingId), default);

        var detail = await new GetMeetingDetailHandler(db).Handle(new GetMeetingDetailQuery("MTG-2026-001"), default);
        detail!.Status.Should().Be("Held");
        detail.Agenda!.Status.Should().Be("Closed");
        await audit.Received(1).EmitEnrichedAsync("Meetings.MeetingHeld", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- CancelMeetingHandler (W5) ----

    [Fact]
    public async Task Cancel_throws_not_found_when_the_meeting_is_missing()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = NewDb(user, clock);

        var act = () => new CancelMeetingHandler(db, clock, Substitute.For<IAuditSink>(), user)
            .Handle(new CancelMeetingCommand(Guid.NewGuid(), "Quorum lost"), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Cancel_on_an_in_progress_meeting_trips_the_domain_guard()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId, _) = await StartedMeetingAsync(user, clock);   // InProgress — Cancel allows Scheduled only
        await using var _ = db;

        var act = () => new CancelMeetingHandler(db, clock, Substitute.For<IAuditSink>(), user)
            .Handle(new CancelMeetingCommand(meetingId, "Too late"), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Cancel_with_a_blank_reason_trips_the_domain_guard()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);
        await using var _ = db;

        var act = () => new CancelMeetingHandler(db, clock, Substitute.For<IAuditSink>(), user)
            .Handle(new CancelMeetingCommand(meetingId, "   "), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Cancel_records_the_reason_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);
        await using var _ = db;
        var audit = Substitute.For<IAuditSink>();

        await new CancelMeetingHandler(db, clock, audit, user)
            .Handle(new CancelMeetingCommand(meetingId, "Quorum will not be met"), default);

        var meeting = await db.Meetings.SingleAsync();
        meeting.Status.Should().Be(MeetingStatus.Cancelled);
        meeting.CancellationReason.Should().Be("Quorum will not be met");
        await audit.Received(1).EmitEnrichedAsync("Meetings.MeetingCancelled", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- RemoveAgendaItemHandler (W6) ----

    [Fact]
    public async Task Remove_agenda_item_throws_not_found_when_the_agenda_is_missing()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = NewDb(user, clock);

        var act = () => new RemoveAgendaItemHandler(db)
            .Handle(new RemoveAgendaItemCommand(Guid.NewGuid(), Guid.NewGuid()), default);

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("Agenda not found*");
    }

    [Fact]
    public async Task Remove_agenda_item_that_is_not_on_the_agenda_trips_the_domain_guard()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);
        await using var _ = db;
        await new AddAgendaItemHandler(db).Handle(new AddAgendaItemCommand(meetingId, Guid.NewGuid(), "TOP-2026-001", "A", false, 15, Presenter, "Omar H."), default);

        var act = () => new RemoveAgendaItemHandler(db)
            .Handle(new RemoveAgendaItemCommand(meetingId, Guid.NewGuid()), default);   // unknown topic

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Remove_agenda_item_on_a_locked_agenda_is_blocked()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId, topic) = await StartedMeetingAsync(user, clock);   // agenda Locked at start
        await using var _ = db;

        var act = () => new RemoveAgendaItemHandler(db)
            .Handle(new RemoveAgendaItemCommand(meetingId, topic), default);

        await act.Should().ThrowAsync<InvalidOperationException>();            // RequireEditable
    }

    [Fact]
    public async Task Remove_agenda_item_drops_it_and_renumbers_the_rest()
    {
        var user = User(); var clock = Clock(Now);
        var (db, meetingId) = await ScheduledMeetingAsync(user, clock);
        await using var _ = db;
        var keep = Guid.NewGuid(); var drop = Guid.NewGuid();
        await new AddAgendaItemHandler(db).Handle(new AddAgendaItemCommand(meetingId, drop, "TOP-2026-001", "Drop", false, 15, Presenter, "P"), default);
        await new AddAgendaItemHandler(db).Handle(new AddAgendaItemCommand(meetingId, keep, "TOP-2026-002", "Keep", false, 15, Presenter, "P"), default);

        var agenda = await new RemoveAgendaItemHandler(db).Handle(new RemoveAgendaItemCommand(meetingId, drop), default);

        agenda.Items.Should().ContainSingle().Which.TopicId.Should().Be(keep);
        agenda.Items.Single().Order.Should().Be(1);                           // renumbered from the gap
    }
}
