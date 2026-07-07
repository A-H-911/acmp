using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Infrastructure.Directory;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Meetings;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Webex;

// The inbound Webex write seam: correlate a recording to the meeting whose WebexMeetingId matches, store the
// reference, and emit an audit event (INV-005). An uncorrelated recording returns false (log-and-drop).
public class MeetingWebexWriterTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static MeetingsDbContext Db(string name)
    {
        var clock = Substitute.For<IClock>(); clock.UtcNow.Returns(Now);
        var user = Substitute.For<ICurrentUser>(); user.IsAuthenticated.Returns(true); user.UserId.Returns("kc-sec");
        return new MeetingsDbContext(
            new DbContextOptionsBuilder<MeetingsDbContext>().UseInMemoryDatabase(name).Options, clock, user);
    }

    private static Meeting Scheduled(string key, string? webexMeetingId)
    {
        var meeting = Meeting.Schedule(key, "Committee", Meeting.SingleCommitteeId, Guid.NewGuid(), "Chair",
            Now, Now.AddMinutes(60), MeetingType.Regular, MeetingMode.Remote, null, "https://webex/join", Now);
        if (webexMeetingId is not null) meeting.SetWebexMeeting(webexMeetingId, null);
        return meeting;
    }

    [Fact]
    public async Task Attaches_a_recording_to_the_correlated_meeting_and_audits()
    {
        await using var db = Db("writer-attach");
        db.Meetings.Add(Scheduled("MTG-2026-001", "webex-abc"));
        await db.SaveChangesAsync();
        var audit = Substitute.For<IAuditSink>();

        var attached = await new MeetingWebexWriter(db, audit).AttachRecordingAsync(
            "webex-abc", new RecordingReference("https://play", "https://dl", 1800));

        attached.Should().BeTrue();
        var meeting = await db.Meetings.SingleAsync();
        meeting.RecordingUrl.Should().Be("https://play");
        meeting.RecordingDownloadUrl.Should().Be("https://dl");
        meeting.RecordingDurationSeconds.Should().Be(1800);
        await audit.Received(1).EmitAsync("Meetings.RecordingAttached", "system:webex", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_false_for_an_uncorrelated_recording()
    {
        await using var db = Db("writer-nomatch");
        db.Meetings.Add(Scheduled("MTG-2026-002", "webex-known"));
        await db.SaveChangesAsync();
        var audit = Substitute.For<IAuditSink>();

        var attached = await new MeetingWebexWriter(db, audit).AttachRecordingAsync(
            "webex-UNKNOWN", new RecordingReference("p", "d", 1));

        attached.Should().BeFalse();
        await audit.DidNotReceive().EmitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sets_the_webex_meeting_id_for_correlation()
    {
        await using var db = Db("writer-setid");
        var meeting = Scheduled("MTG-2026-003", null);
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        var audit = Substitute.For<IAuditSink>();

        await new MeetingWebexWriter(db, audit).SetWebexMeetingAsync(meeting.PublicId, "webex-new", "https://webex/j2");

        var saved = await db.Meetings.SingleAsync();
        saved.WebexMeetingId.Should().Be("webex-new");
        saved.JoinUrl.Should().Be("https://webex/j2");
    }
}
