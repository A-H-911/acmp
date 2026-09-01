using Acmp.Application.Tests.Shared;
using Acmp.Modules.Meetings.Application.Features.GetAgendaProjection;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Topics;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Meetings;

// WBS-26.5 / DW-086 — THE CALENDAR AGENDA PROJECTION, AND THE TWO PROPERTIES THAT ARE NOT ABOUT ITS OUTPUT.
//
// The obvious tests here are "it returns the topics". The two that earn their place are the REDACTION — a bulk
// read over a month would otherwise hand every Restricted key and title to the whole committee on page load —
// and the CALL COUNT on ITopicConfidentiality. A projection that asked per meeting would be correct in its
// output and wrong in the way DEF-104 records, and no assertion about the returned DTOs could tell.
//
// ⚠ THE DATES HERE ARE NOT A CLOCK FIXTURE (LL-044). Every range is derived from the same Now constant the
// meetings are seeded against, so no case changes meaning when the wall clock moves. The failure LL-044
// describes needs a fixture pinned to an absolute date while the subject moves with the clock; nothing here
// reads the real clock at all.
public class AgendaProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MonthStart = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MonthEnd = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid OpenTopic = Guid.NewGuid();
    private static readonly Guid RestrictedTopic = Guid.NewGuid();
    private static readonly Guid GuestMemberId = Guid.NewGuid();

    private static IClock Clock()
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(Now);
        return c;
    }

    private static ICurrentUser Principal(string sub, params string[] roles)
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns(sub);
        u.Roles.Returns(roles);
        u.IsInRole(Arg.Any<string>()).Returns(call => roles.Contains(call.Arg<string>()));
        return u;
    }

    private static ICommitteeDirectory Directory(string sub, Guid memberId)
    {
        var d = Substitute.For<ICommitteeDirectory>();
        d.ResolveMemberAsync(sub, Arg.Any<CancellationToken>())
            .Returns(new CommitteeMemberRef(memberId, Now.AddDays(1)));
        return d;
    }

    private static ICommitteeDirectory NobodyResolves() => Substitute.For<ICommitteeDirectory>();

    /*
     * THREE MEETINGS, AND THE THIRD IS WHAT MAKES THE CALL-COUNT TEST MEAN ANYTHING. Two sit inside March and
     * one sits outside it. Both in-month meetings carry an agenda, so a projection that resolved
     * confidentiality per meeting would call the port twice and still return exactly the right topics.
     */
    private static async Task<MeetingsDbContext> SeedAsync(ICurrentUser user)
    {
        var db = new MeetingsDbContext(
            new DbContextOptionsBuilder<MeetingsDbContext>()
                .UseInMemoryDatabase("agenda-projection-" + Guid.NewGuid()).Options,
            Clock(), user);

        var first = Meeting.Schedule("MTG-2026-001", "First", Guid.NewGuid(), Guid.NewGuid(), "Chair",
            Now, Now.AddMinutes(60), MeetingType.Regular, MeetingMode.InPerson, null, null, Now);
        var second = Meeting.Schedule("MTG-2026-002", "Second", Guid.NewGuid(), Guid.NewGuid(), "Chair",
            Now.AddDays(5), Now.AddDays(5).AddMinutes(60), MeetingType.Regular, MeetingMode.InPerson, null, null, Now);
        var outside = Meeting.Schedule("MTG-2026-003", "Next month", Guid.NewGuid(), Guid.NewGuid(), "Chair",
            MonthEnd.AddDays(3), MonthEnd.AddDays(3).AddMinutes(60), MeetingType.Regular, MeetingMode.InPerson,
            null, null, Now);
        db.Meetings.AddRange(first, second, outside);

        var a1 = Agenda.Draft("AGN-2026-001", first.PublicId);
        a1.AddItem(OpenTopic, "TOP-2026-007", "An open topic", false, 15, GuestMemberId, "A Guest");
        a1.AddItem(RestrictedTopic, "TOP-2026-009", "A restricted topic", false, 15, Guid.NewGuid(), "Someone");

        var a2 = Agenda.Draft("AGN-2026-002", second.PublicId);
        a2.AddItem(Guid.NewGuid(), "TOP-2026-008", "Another topic", false, 15, Guid.NewGuid(), "Someone Else");

        var a3 = Agenda.Draft("AGN-2026-003", outside.PublicId);
        a3.AddItem(Guid.NewGuid(), "TOP-2026-010", "Out of range", false, 15, Guid.NewGuid(), "Someone Else");

        db.Agendas.AddRange(a1, a2, a3);
        await db.SaveChangesAsync();
        return db;
    }

    private static GetAgendaProjectionHandler Handler(
        MeetingsDbContext db, ICommitteeDirectory dir, ICurrentUser user, ITopicConfidentiality confidentiality) =>
        new(db, dir, user, confidentiality);

    private static GetAgendaProjectionQuery March => new(MonthStart, MonthEnd);

    /*
     * ASSERTED FIRST AND ON PURPOSE — LL-041 and the AC-010 lesson. If the projection returned nothing, every
     * redaction assertion below would pass over an empty result and prove only that the fixture is broken.
     */
    [Fact]
    public async Task The_projection_returns_the_months_meetings_with_their_topic_keys()
    {
        var member = Principal("kc-member", AcmpRoles.Member);
        await using var db = await SeedAsync(member);

        var result = await Handler(db, NobodyResolves(), member, TopicConfidentialityStub.SeesEverything())
            .Handle(March, default);

        result.Should().HaveCount(2, "the third meeting is outside the requested range");
        result[0].MeetingKey.Should().Be("MTG-2026-001");
        result[0].Items.Select(i => i.TopicKey).Should().Contain("TOP-2026-007");
        result[1].MeetingKey.Should().Be("MTG-2026-002");
    }

    /*
     * THE CONTROL FOR THE REDACTION CASE. Without it, "the restricted key is absent" would pass just as
     * happily against a projection that returned nothing at all for that meeting.
     */
    [Fact]
    public async Task An_unrestricted_caller_sees_the_restricted_topics_key_and_title()
    {
        var member = Principal("kc-member", AcmpRoles.Member);
        await using var db = await SeedAsync(member);

        var result = await Handler(db, NobodyResolves(), member, TopicConfidentialityStub.SeesEverything())
            .Handle(March, default);

        var item = result[0].Items.Single(i => i.TopicId == RestrictedTopic);
        item.TopicKey.Should().Be("TOP-2026-009");
        item.TopicTitle.Should().Be("A restricted topic");
    }

    /*
     * ⛔ THE GUARD. AgendaItem froze the topic key and title at build time, so without the redaction this
     * projection hands a Restricted topic key AND title to the whole committee — on page load, across a whole
     * month, with no selection. Removing the hidden-set lookup from the handler makes this fail.
     */
    [Fact]
    public async Task A_restricted_topic_is_redacted_to_empty_key_and_title_but_keeps_its_id()
    {
        var member = Principal("kc-member", AcmpRoles.Member);
        await using var db = await SeedAsync(member);

        var result = await Handler(db, NobodyResolves(), member,
            TopicConfidentialityStub.Hiding(RestrictedTopic)).Handle(March, default);

        var item = result[0].Items.Single(i => i.TopicId == RestrictedTopic);
        item.TopicKey.Should().BeEmpty("a server-side English word would break the EN+AR guardrail");
        item.TopicTitle.Should().BeEmpty();

        // AND THE SIBLING IS UNTOUCHED — a redaction that emptied everything would pass the two lines above.
        var open = result[0].Items.Single(i => i.TopicId == OpenTopic);
        open.TopicKey.Should().Be("TOP-2026-007");
    }

    /*
     * ⚠ THE PROPERTY NO ASSERTION ABOUT THE OUTPUT CAN SEE. A handler that resolved confidentiality inside the
     * per-meeting loop would return byte-identical DTOs and reintroduce the N+1 that DW-086 forbids by name,
     * through the back door of the very projection built to avoid it. The fixture puts TWO meetings in range
     * precisely so that "once" and "once per meeting" are different numbers.
     */
    [Fact]
    public async Task The_hidden_set_is_resolved_once_for_the_whole_range_not_once_per_meeting()
    {
        var member = Principal("kc-member", AcmpRoles.Member);
        await using var db = await SeedAsync(member);
        var confidentiality = TopicConfidentialityStub.Hiding(RestrictedTopic);

        var result = await Handler(db, NobodyResolves(), member, confidentiality).Handle(March, default);

        result.Should().HaveCount(2, "the guard is only meaningful with more than one meeting in range");
        await confidentiality.Received(1).GetHiddenTopicIdsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_guest_presenter_sees_only_the_meeting_they_present_at()
    {
        var guest = Principal("kc-guest", AcmpRoles.Guest);
        await using var db = await SeedAsync(guest);

        var result = await Handler(db, Directory("kc-guest", GuestMemberId), guest,
            TopicConfidentialityStub.SeesEverything()).Handle(March, default);

        result.Should().ContainSingle();
        result[0].MeetingKey.Should().Be("MTG-2026-001");
    }

    /*
     * BOUNDED, AND IT REFUSES RATHER THAN CLAMPING. A clamp is DEF-103 shape: the caller believes they
     * received the range they asked for. Both directions are forced, because an inverted range and an
     * over-wide one fail on different branches.
     */
    [Fact]
    public async Task An_inverted_range_is_refused()
    {
        var member = Principal("kc-member", AcmpRoles.Member);
        await using var db = await SeedAsync(member);

        var act = () => Handler(db, NobodyResolves(), member, TopicConfidentialityStub.SeesEverything())
            .Handle(new GetAgendaProjectionQuery(MonthEnd, MonthStart), default);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task A_range_wider_than_the_bound_is_refused_rather_than_narrowed()
    {
        var member = Principal("kc-member", AcmpRoles.Member);
        await using var db = await SeedAsync(member);
        var tooWide = new GetAgendaProjectionQuery(
            MonthStart, MonthStart.AddDays(GetAgendaProjectionQuery.MaxRangeDays + 1));

        var act = () => Handler(db, NobodyResolves(), member, TopicConfidentialityStub.SeesEverything())
            .Handle(tooWide, default);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
