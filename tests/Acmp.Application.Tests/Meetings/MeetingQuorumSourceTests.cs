using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Infrastructure.Directory;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Acmp.Application.Tests.Meetings;

// Direct coverage for the Vote present-quorum seam impl (mocked away in the Vote handler tests): it must
// count ONLY voting-eligible attendees who are Present or Late, and return 0 for an unknown meeting.
public class MeetingQuorumSourceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static MeetingsDbContext Db(string name)
    {
        var clock = Substitute.For<IClock>(); clock.UtcNow.Returns(Now);
        var user = Substitute.For<ICurrentUser>(); user.IsAuthenticated.Returns(true); user.UserId.Returns("kc-sec");
        return new MeetingsDbContext(
            new DbContextOptionsBuilder<MeetingsDbContext>().UseInMemoryDatabase(name).Options, clock, user);
    }

    [Fact]
    public async Task Counts_only_present_or_late_voting_eligible_attendees()
    {
        var meeting = Meeting.Schedule("MTG-2026-050", "Committee", Meeting.SingleCommitteeId, Guid.NewGuid(), "Chair",
            Now, Now.AddMinutes(60), MeetingType.Regular, MeetingMode.InPerson, null, null, Now);
        var present = meeting.SeedAttendee(Guid.NewGuid(), "Present eligible", AttendanceRole.Member, true);
        var late = meeting.SeedAttendee(Guid.NewGuid(), "Late eligible", AttendanceRole.Member, true);
        meeting.SeedAttendee(Guid.NewGuid(), "Invited eligible", AttendanceRole.Member, true);           // not marked → excluded
        var ineligible = meeting.SeedAttendee(Guid.NewGuid(), "Present ineligible", AttendanceRole.Member, false);
        meeting.MarkAttendance(present.UserId, AttendanceStatus.Present, Now);
        meeting.MarkAttendance(late.UserId, AttendanceStatus.Late, Now);
        meeting.MarkAttendance(ineligible.UserId, AttendanceStatus.Present, Now);

        await using var db = Db("quorum-" + Guid.NewGuid());
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var source = new MeetingQuorumSource(db);
        (await source.GetPresentEligibleCountAsync(meeting.PublicId)).Should().Be(2);
        (await source.GetPresentEligibleCountAsync(Guid.NewGuid())).Should().Be(0);   // unknown meeting
    }
}
