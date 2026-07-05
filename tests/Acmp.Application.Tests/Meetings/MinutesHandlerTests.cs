using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Features.ApproveMinutes;
using Acmp.Modules.Meetings.Application.Features.DraftMinutes;
using Acmp.Modules.Meetings.Application.Features.GetMinutesAwaiting;
using Acmp.Modules.Meetings.Application.Features.GetMinutesByKey;
using Acmp.Modules.Meetings.Application.Features.GetMinutesForMeeting;
using Acmp.Modules.Meetings.Application.Features.PublishMinutes;
using Acmp.Modules.Meetings.Application.Features.RequestMinutesChanges;
using Acmp.Modules.Meetings.Application.Features.ReviseMinutes;
using Acmp.Modules.Meetings.Application.Features.SubmitMinutesForReview;
using Acmp.Modules.Meetings.Application.Features.SupersedeMinutes;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Behaviors;
using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Meetings;

// Round-trips the W10 MoM flow through the real MeetingsDbContext (InMemory): EF mapping (owned bilingual
// Summary, the (Key,Version) shape), the MIN key generator, the 5-state lifecycle, version-preserving
// supersede, the soft-SoD-2 sole-author flag (AC-014), and the notification fan-outs. The committee
// directory + channel are faked so we assert the module's outbound calls without touching other modules.
public class MinutesHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly LocalizedString Summary = LocalizedString.Create("Roadmap discussed", "نوقشت خارطة الطريق");

    private sealed class RecordingChannel : INotificationChannel
    {
        public List<NotificationMessage> Sent { get; } = new();
        public Task PublishAsync(NotificationMessage message, CancellationToken ct = default) { Sent.Add(message); return Task.CompletedTask; }
    }

    private static MeetingsDbContext Db(string name, ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<MeetingsDbContext>().UseInMemoryDatabase(name).Options, clock, user);

    private static ICurrentUser User(string sub = "kc-sec", string name = "Sam Secretary")
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

    private static ICommitteeDirectory Dir(params string[] subs)
    {
        var members = (subs.Length == 0 ? new[] { "kc-a", "kc-b" } : subs).Select(s => new CommitteeRecipient(s, s)).ToList();
        var d = Substitute.For<ICommitteeDirectory>();
        d.GetActiveMembersAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyCollection<CommitteeRecipient>)members);
        return d;
    }

    private static INotificationChannel NoNotify() => Substitute.For<INotificationChannel>();

    private static Meeting NewMeeting(MeetingStatus status)
    {
        var m = Meeting.Schedule("MTG-2026-001", "Weekly Committee", Meeting.SingleCommitteeId, Guid.NewGuid(), "Chair",
            Now, Now.AddHours(1), MeetingType.Regular, MeetingMode.InPerson, null, null, Now);
        if (status is MeetingStatus.InProgress or MeetingStatus.Held) m.Start(Now);
        if (status is MeetingStatus.Held) m.Hold(Now);
        return m;
    }

    // Seed a held meeting, then draft its minutes — the common arrange for the review/approve/publish tests.
    private static async Task<(string DbName, Guid MinutesId)> DraftedAsync(ICurrentUser user, IClock clock, string? dbName = null)
    {
        var name = dbName ?? "mom-" + Guid.NewGuid();
        await using (var db = Db(name, user, clock))
        {
            db.Meetings.Add(NewMeeting(MeetingStatus.Held));
            await db.SaveChangesAsync();
        }
        MinutesSummaryDto summary;
        await using (var db = Db(name, user, clock))
            summary = await new DraftMinutesHandler(db, new MeetingKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
                .Handle(new DraftMinutesCommand(GuidOfMeeting(name, user, clock).Result, Summary), default);
        return (name, summary.Id);
    }

    private static async Task<Guid> GuidOfMeeting(string name, ICurrentUser user, IClock clock)
    {
        await using var db = Db(name, user, clock);
        return (await db.Meetings.FirstAsync()).PublicId;
    }

    // P12 — committee-wide approval queue feeding the secretary dashboard (AC-065).
    [Fact]
    public async Task GetMinutesAwaiting_lists_only_InReview_records()
    {
        var user = User(); var clock = Clock(Now);
        var (name, minutesId) = await DraftedAsync(user, clock);   // a Draft — not yet awaiting

        await using var read1 = Db(name, user, clock);
        (await new GetMinutesAwaitingHandler(read1).Handle(new GetMinutesAwaitingQuery(), default))
            .Should().BeEmpty();

        await using (var db = Db(name, user, clock))               // Draft → InReview
            await new SubmitMinutesForReviewHandler(db, user, clock, Substitute.For<IAuditSink>())
                .Handle(new SubmitMinutesForReviewCommand(minutesId), default);

        await using var read2 = Db(name, user, clock);
        (await new GetMinutesAwaitingHandler(read2).Handle(new GetMinutesAwaitingQuery(), default))
            .Should().ContainSingle().Which.Status.Should().Be("InReview");
    }

    [Fact]
    public async Task Draft_creates_a_MIN_keyed_draft_snapshotting_the_meeting()
    {
        var user = User(); var clock = Clock(Now);
        var name = "draft-" + Guid.NewGuid();
        Guid meetingId;
        await using (var db = Db(name, user, clock))
        {
            var m = NewMeeting(MeetingStatus.Held);
            db.Meetings.Add(m);
            await db.SaveChangesAsync();
            meetingId = m.PublicId;
        }
        var audit = Substitute.For<IAuditSink>();

        await using (var db = Db(name, user, clock))
        {
            var summary = await new DraftMinutesHandler(db, new MeetingKeyGenerator(db), user, clock, audit)
                .Handle(new DraftMinutesCommand(meetingId, Summary), default);
            summary.Key.Should().Be("MIN-2026-001");
            summary.Version.Should().Be(1);
            summary.Status.Should().Be("Draft");
            summary.MeetingKey.Should().Be("MTG-2026-001");
        }
        await audit.Received(1).EmitAsync("Meetings.MinutesDrafted", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Draft_throws_not_found_for_an_unknown_meeting()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db("nf-" + Guid.NewGuid(), user, clock);
        var act = () => new DraftMinutesHandler(db, new MeetingKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
            .Handle(new DraftMinutesCommand(Guid.NewGuid(), Summary), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact] // docs/12 §6: minutes can only be drafted for a meeting InProgress/Held → 409 for a Scheduled one
    public async Task Draft_rejects_a_meeting_that_is_not_in_progress_or_held()
    {
        var user = User(); var clock = Clock(Now);
        var name = "sched-" + Guid.NewGuid();
        Guid meetingId;
        await using (var db = Db(name, user, clock))
        {
            var m = NewMeeting(MeetingStatus.Scheduled);
            db.Meetings.Add(m);
            await db.SaveChangesAsync();
            meetingId = m.PublicId;
        }
        await using (var db = Db(name, user, clock))
        {
            var act = () => new DraftMinutesHandler(db, new MeetingKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
                .Handle(new DraftMinutesCommand(meetingId, Summary), default);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*in progress or held*");
        }
    }

    [Fact] // one MoM per meeting — a second draft-from-scratch is rejected (corrections go through supersede)
    public async Task Draft_rejects_a_second_minutes_for_the_same_meeting()
    {
        var user = User(); var clock = Clock(Now);
        var (name, _) = await DraftedAsync(user, clock);
        var meetingId = await GuidOfMeeting(name, user, clock);

        await using var db = Db(name, user, clock);
        var act = () => new DraftMinutesHandler(db, new MeetingKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
            .Handle(new DraftMinutesCommand(meetingId, Summary), default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exist*");
    }

    [Fact]
    public async Task Revise_updates_the_draft_body()
    {
        var user = User(); var clock = Clock(Now);
        var (name, id) = await DraftedAsync(user, clock);
        var edited = LocalizedString.Create("Revised body", "نص منقّح");

        await using (var db = Db(name, user, clock))
            await new ReviseMinutesHandler(db, user, clock, Substitute.For<IAuditSink>())
                .Handle(new ReviseMinutesCommand(id, edited), default);

        await using var read = Db(name, user, clock);
        var detail = await new GetMinutesByKeyHandler(read).Handle(new GetMinutesByKeyQuery("MIN-2026-001"), default);
        detail!.Summary.Should().Be(edited);
    }

    [Fact] // AC-014: approving your own sole-authored minutes is allowed but flagged (soft SoD-2)
    public async Task Approve_by_the_sole_author_is_allowed_and_flagged()
    {
        var author = User("kc-sec"); var clock = Clock(Now);
        var (name, id) = await DraftedAsync(author, clock);
        await using (var db = Db(name, author, clock))
            await new SubmitMinutesForReviewHandler(db, author, clock, Substitute.For<IAuditSink>())
                .Handle(new SubmitMinutesForReviewCommand(id), default);

        var audit = Substitute.For<IAuditSink>();
        await using (var db = Db(name, author, clock)) // same subject approves → sole author
            await new ApproveMinutesHandler(db, author, clock, audit).Handle(new ApproveMinutesCommand(id), default);

        await using var read = Db(name, author, clock);
        var detail = await new GetMinutesByKeyHandler(read).Handle(new GetMinutesByKeyQuery("MIN-2026-001"), default);
        detail!.Status.Should().Be("Approved");
        detail.ApprovedBySoleAuthor.Should().BeTrue();
        await audit.Received(1).EmitAsync("Meetings.MinutesApprovedBySoleAuthor", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact] // a DIFFERENT approver clears the sole-author flag (four-eyes satisfied)
    public async Task Approve_by_a_different_actor_is_not_flagged()
    {
        var author = User("kc-sec"); var clock = Clock(Now);
        var (name, id) = await DraftedAsync(author, clock);
        await using (var db = Db(name, author, clock))
            await new SubmitMinutesForReviewHandler(db, author, clock, Substitute.For<IAuditSink>())
                .Handle(new SubmitMinutesForReviewCommand(id), default);

        var chair = User("kc-chair", "Sara Chair");
        var audit = Substitute.For<IAuditSink>();
        await using (var db = Db(name, chair, clock))
            await new ApproveMinutesHandler(db, chair, clock, audit).Handle(new ApproveMinutesCommand(id), default);

        await using var read = Db(name, chair, clock);
        var detail = await new GetMinutesByKeyHandler(read).Handle(new GetMinutesByKeyQuery("MIN-2026-001"), default);
        detail!.ApprovedBySoleAuthor.Should().BeFalse();
        detail.ApprovedByName.Should().Be("Sara Chair");
        await audit.DidNotReceive().EmitAsync("Meetings.MinutesApprovedBySoleAuthor", Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact] // AC-037: a change-request bounces to Draft and notifies the author (targeted, not fanned out)
    public async Task RequestChanges_returns_to_draft_and_notifies_the_author()
    {
        var author = User("kc-sec"); var clock = Clock(Now);
        var (name, id) = await DraftedAsync(author, clock);
        await using (var db = Db(name, author, clock))
            await new SubmitMinutesForReviewHandler(db, author, clock, Substitute.For<IAuditSink>())
                .Handle(new SubmitMinutesForReviewCommand(id), default);

        var chair = User("kc-chair"); var channel = new RecordingChannel();
        await using (var db = Db(name, chair, clock))
            await new RequestMinutesChangesHandler(db, chair, clock, Substitute.For<IAuditSink>(), channel)
                .Handle(new RequestMinutesChangesCommand(id), default);

        await using var read = Db(name, chair, clock);
        var detail = await new GetMinutesByKeyHandler(read).Handle(new GetMinutesByKeyQuery("MIN-2026-001"), default);
        detail!.Status.Should().Be("Draft");
        channel.Sent.Should().ContainSingle()
            .Which.Should().Match<NotificationMessage>(m => m.RecipientUserId == "kc-sec" && m.Category == "MinutesChangesRequested");
    }

    [Fact] // AC-038: publish seals the record and fans out one notification per active member with a deep link
    public async Task Publish_notifies_every_member_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        var (name, id) = await DraftedAsync(user, clock);
        await using (var db = Db(name, user, clock))
            await new SubmitMinutesForReviewHandler(db, user, clock, Substitute.For<IAuditSink>())
                .Handle(new SubmitMinutesForReviewCommand(id), default);
        await using (var db = Db(name, user, clock))
            await new ApproveMinutesHandler(db, user, clock, Substitute.For<IAuditSink>()).Handle(new ApproveMinutesCommand(id), default);

        var channel = new RecordingChannel(); var audit = Substitute.For<IAuditSink>();
        await using (var db = Db(name, user, clock))
            await new PublishMinutesHandler(db, user, clock, audit, Dir("kc-a", "kc-b", "kc-c"), channel)
                .Handle(new PublishMinutesCommand(id), default);

        channel.Sent.Should().HaveCount(3);
        channel.Sent.Should().OnlyContain(m => m.Category == "MinutesPublished" && m.DeepLink == "/meetings/MTG-2026-001/minutes");
        await audit.Received(1).EmitAsync("Meetings.MinutesPublished", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());

        await using var read = Db(name, user, clock);
        (await new GetMinutesByKeyHandler(read).Handle(new GetMinutesByKeyQuery("MIN-2026-001"), default))!.Status.Should().Be("Published");
    }

    [Fact] // AC-036: supersede keeps the SAME key, bumps to v2 (Published), and links the prior (now Superseded)
    public async Task Supersede_creates_v2_under_the_same_key_and_supersedes_the_prior()
    {
        var user = User(); var clock = Clock(Now);
        var (name, id) = await DraftedAsync(user, clock);
        await using (var db = Db(name, user, clock))
            await new SubmitMinutesForReviewHandler(db, user, clock, Substitute.For<IAuditSink>())
                .Handle(new SubmitMinutesForReviewCommand(id), default);
        await using (var db = Db(name, user, clock))
            await new ApproveMinutesHandler(db, user, clock, Substitute.For<IAuditSink>()).Handle(new ApproveMinutesCommand(id), default);
        await using (var db = Db(name, user, clock))
            await new PublishMinutesHandler(db, user, clock, Substitute.For<IAuditSink>(), Dir(), NoNotify())
                .Handle(new PublishMinutesCommand(id), default);

        var channel = new RecordingChannel(); var audit = Substitute.For<IAuditSink>();
        var reason = LocalizedString.Create("Fixed attendance", "تصحيح الحضور");
        var corrected = LocalizedString.Create("Corrected minutes", "محضر مصحح");
        MinutesSummaryDto successor;
        await using (var db = Db(name, user, clock))
            successor = await new SupersedeMinutesHandler(db, user, clock, audit, Dir("kc-a", "kc-b"), channel)
                .Handle(new SupersedeMinutesCommand(id, corrected, reason), default);

        successor.Key.Should().Be("MIN-2026-001"); // SAME key
        successor.Version.Should().Be(2);
        successor.Status.Should().Be("Published");

        await using var read = Db(name, user, clock);
        var prior = await new GetMinutesByKeyHandler(read).Handle(new GetMinutesByKeyQuery("MIN-2026-001", Version: 1), default);
        prior!.Status.Should().Be("Superseded");
        prior.SupersededByMinutesId.Should().Be(successor.Id);
        prior.SupersessionReason.Should().Be(reason);

        var current = await new GetMinutesByKeyHandler(read).Handle(new GetMinutesByKeyQuery("MIN-2026-001"), default);
        current!.Version.Should().Be(2); // by-key with no version returns the head
        channel.Sent.Should().HaveCount(2); // the new published version notifies members too
        await audit.Received(1).EmitAsync("Meetings.MinutesSuperseded", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Supersede_throws_not_found_for_an_unknown_prior()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db("nf-" + Guid.NewGuid(), user, clock);
        var act = () => new SupersedeMinutesHandler(db, user, clock, Substitute.For<IAuditSink>(), Dir(), NoNotify())
            .Handle(new SupersedeMinutesCommand(Guid.NewGuid(), Summary, LocalizedString.Create("r", "ر")), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetMinutesForMeeting_lists_versions_newest_first()
    {
        var user = User(); var clock = Clock(Now);
        var (name, id) = await DraftedAsync(user, clock);
        var meetingId = await GuidOfMeeting(name, user, clock);
        await using (var db = Db(name, user, clock))
            await new SubmitMinutesForReviewHandler(db, user, clock, Substitute.For<IAuditSink>())
                .Handle(new SubmitMinutesForReviewCommand(id), default);
        await using (var db = Db(name, user, clock))
            await new ApproveMinutesHandler(db, user, clock, Substitute.For<IAuditSink>()).Handle(new ApproveMinutesCommand(id), default);
        await using (var db = Db(name, user, clock))
            await new SupersedeMinutesHandler(db, user, clock, Substitute.For<IAuditSink>(), Dir(), NoNotify())
                .Handle(new SupersedeMinutesCommand(id, Summary, LocalizedString.Create("r", "ر")), default);

        await using var read = Db(name, user, clock);
        var list = await new GetMinutesForMeetingHandler(read).Handle(new GetMinutesForMeetingQuery(meetingId), default);
        list.Should().HaveCount(2);
        list[0].Version.Should().Be(2); // newest first
        list.Should().OnlyContain(m => m.MeetingKey == "MTG-2026-001");
    }

    // ── validators ──────────────────────────────────────────────────────────
    [Fact]
    public void Draft_validator_requires_both_languages()
    {
        var v = new DraftMinutesValidator();
        v.Validate(new DraftMinutesCommand(Guid.NewGuid(), Summary)).IsValid.Should().BeTrue();
        v.Validate(new DraftMinutesCommand(Guid.NewGuid(), new LocalizedString("EN only", ""))).IsValid.Should().BeFalse();
        v.Validate(new DraftMinutesCommand(Guid.Empty, Summary)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Supersede_validator_requires_both_languages_for_summary_and_reason()
    {
        var v = new SupersedeMinutesValidator();
        var ok = new SupersedeMinutesCommand(Guid.NewGuid(), Summary, LocalizedString.Create("why", "لماذا"));
        v.Validate(ok).IsValid.Should().BeTrue();
        v.Validate(ok with { Reason = new LocalizedString("EN only", "") }).IsValid.Should().BeFalse();
        v.Validate(ok with { Summary = new LocalizedString("", "AR only") }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Revise_validator_requires_both_languages()
    {
        var v = new ReviseMinutesValidator();
        v.Validate(new ReviseMinutesCommand(Guid.NewGuid(), Summary)).IsValid.Should().BeTrue();
        v.Validate(new ReviseMinutesCommand(Guid.NewGuid(), new LocalizedString("", ""))).IsValid.Should().BeFalse();
    }

    // ── authorization pipeline (guardrail 4) ────────────────────────────────
    [Fact]
    public async Task Pipeline_forbids_a_member_from_drafting_minutes()
    {
        var member = Substitute.For<ICurrentUser>();
        member.IsAuthenticated.Returns(true);
        member.UserId.Returns("kc-member");
        member.IsInRole("Member").Returns(true);
        var behavior = new AuthorizationBehavior<DraftMinutesCommand, MinutesSummaryDto>(member, Substitute.For<IAuditSink>());

        var act = () => behavior.Handle(new DraftMinutesCommand(Guid.NewGuid(), Summary),
            () => Task.FromResult<MinutesSummaryDto>(null!), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
