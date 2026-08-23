using Acmp.Application.Tests.Shared;
using Acmp.Modules.Meetings.Application.Features.GetMeetingDetail;
using Acmp.Modules.Meetings.Application.Features.GetMeetings;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Meetings;

// DEF-073 / AC-011 — A GUEST PRESENTER SEES THE MEETINGS THEY PRESENT AT AND NO OTHERS.
//
// WHAT WAS BROKEN: GuestSurfaceMiddleware admits GET on the /api/meetings PREFIX, because the design's
// role matrix grants Guest "agenda (view)". A path gate cannot turn a display key into a meeting, so
// admitting the prefix admitted every meeting the committee had ever scheduled — AC-011's "any action
// outside that meeting scope" answering 200 where the AC says 403.
//
// ⚠ WHY NO EXISTING TEST COULD SEE IT, which is the reusable half: every principal in the Meetings
// suite is a committee member, and MeetingHandlerTests builds ICurrentUser as a bare substitute whose
// IsInRole answers false for everything. A guest-only caller had never been constructed anywhere, so
// the branch that discriminates did not exist to be wrong. The suite was not weak about this rule; it
// had never been asked the question.
public class GuestMeetingScopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid TheirTopic = Guid.NewGuid();
    private static readonly Guid OtherTopic = Guid.NewGuid();

    /// <summary>The guest's own member PublicId — what AgendaItem.PresenterUserId actually stores.</summary>
    private static readonly Guid GuestMemberId = Guid.NewGuid();

    private static IClock Clock()
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(Now);
        return c;
    }

    /// <summary>A caller holding exactly the roles named — nothing else answers true.</summary>
    private static ICurrentUser Principal(string sub, params string[] roles)
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns(sub);
        u.Roles.Returns(roles);
        u.IsInRole(Arg.Any<string>()).Returns(call => roles.Contains(call.Arg<string>()));
        return u;
    }

    /// <summary>Resolves exactly one subject to a member row; anybody else has none.</summary>
    private static ICommitteeDirectory Directory(string sub, Guid memberId)
    {
        var d = Substitute.For<ICommitteeDirectory>();
        d.ResolveMemberAsync(sub, Arg.Any<CancellationToken>())
            .Returns(new CommitteeMemberRef(memberId, Now.AddDays(1)));
        return d;
    }

    private static ICommitteeDirectory NobodyResolves() => Substitute.For<ICommitteeDirectory>();

    /*
     * Two meetings. The guest presents TheirTopic at MTG-2026-001 and has no relationship at all with
     * MTG-2026-002 — which carries an agenda item of its own, so the two meetings differ ONLY in who
     * presents. A fixture whose other meeting had no agenda would let "returns nothing because there
     * is nothing there" pass as scoping.
     */
    private static async Task<MeetingsDbContext> SeedAsync(ICurrentUser user)
    {
        var db = new MeetingsDbContext(
            new DbContextOptionsBuilder<MeetingsDbContext>()
                .UseInMemoryDatabase("guest-scope-" + Guid.NewGuid()).Options,
            Clock(), user);

        var theirs = Meeting.Schedule("MTG-2026-001", "Theirs", Guid.NewGuid(), Guid.NewGuid(), "Chair",
            Now, Now.AddMinutes(60), MeetingType.Regular, MeetingMode.InPerson, null, null, Now);
        var others = Meeting.Schedule("MTG-2026-002", "Another committee session", Guid.NewGuid(), Guid.NewGuid(), "Chair",
            Now.AddDays(1), Now.AddDays(1).AddMinutes(60), MeetingType.Regular, MeetingMode.InPerson, null, null, Now);
        db.Meetings.AddRange(theirs, others);

        var theirAgenda = Agenda.Draft("AGN-2026-001", theirs.PublicId);
        theirAgenda.AddItem(TheirTopic, "TOP-2026-007", "Their topic", false, 15, GuestMemberId, "A Guest");

        var otherAgenda = Agenda.Draft("AGN-2026-002", others.PublicId);
        otherAgenda.AddItem(OtherTopic, "TOP-2026-008", "Another topic", false, 15, Guid.NewGuid(), "Someone Else");

        db.Agendas.AddRange(theirAgenda, otherAgenda);
        await db.SaveChangesAsync();
        return db;
    }

    private static GetMeetingsHandler List(MeetingsDbContext db, ICommitteeDirectory dir, ICurrentUser user) =>
        new(db, dir, user);

    private static GetMeetingDetailHandler Detail(MeetingsDbContext db, ICommitteeDirectory dir, ICurrentUser user) =>
        new(db, dir, user, TopicConfidentialityStub.SeesEverything());

    /*
     * ASSERTED FIRST AND ON PURPOSE — the AC-010 lesson. If the guest could reach NOTHING, every
     * refusal below would pass while proving only that the fixture is broken.
     */
    [Fact]
    public async Task A_guest_presenter_can_read_the_meeting_they_present_at()
    {
        var guest = Principal("kc-guest", AcmpRoles.Guest);
        await using var db = await SeedAsync(guest);

        var detail = await Detail(db, Directory("kc-guest", GuestMemberId), guest)
            .Handle(new GetMeetingDetailQuery("MTG-2026-001"), default);

        detail.Should().NotBeNull();
        detail!.Key.Should().Be("MTG-2026-001");
    }

    [Fact]
    public async Task A_guest_presenter_is_refused_a_meeting_they_do_not_present_at()
    {
        var guest = Principal("kc-guest", AcmpRoles.Guest);
        await using var db = await SeedAsync(guest);

        // Same caller, same route, same request shape — only the MEETING differs. That is what makes
        // this scope rather than a blanket refusal, and it is why the positive control above exists.
        var refuse = () => Detail(db, Directory("kc-guest", GuestMemberId), guest)
            .Handle(new GetMeetingDetailQuery("MTG-2026-002"), default);

        await refuse.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task A_guest_presenter_lists_only_their_own_meetings()
    {
        var guest = Principal("kc-guest", AcmpRoles.Guest);
        await using var db = await SeedAsync(guest);

        var list = await List(db, Directory("kc-guest", GuestMemberId), guest)
            .Handle(new GetMeetingsQuery(), default);

        // The KEY is asserted, not the count. A count-based assertion on a shared fixture cannot
        // discriminate between "filtered correctly" and "filtered to the wrong one".
        list.Should().ContainSingle().Which.Key.Should().Be("MTG-2026-001");
    }

    /*
     * THE UNKNOWN KEY TAKES THE SAME ANSWER AS THE OUT-OF-SCOPE ONE. Answering 404 here and 403 above
     * would turn the route into an existence oracle for a guest, who can no longer see the list that
     * would have told them. Without this test, "return null when the meeting is missing" reads like an
     * obvious tidy-up and silently reintroduces the oracle.
     */
    [Fact]
    public async Task A_guest_presenter_cannot_tell_a_missing_meeting_from_a_forbidden_one()
    {
        var guest = Principal("kc-guest", AcmpRoles.Guest);
        await using var db = await SeedAsync(guest);

        var refuse = () => Detail(db, Directory("kc-guest", GuestMemberId), guest)
            .Handle(new GetMeetingDetailQuery("MTG-2026-999"), default);

        await refuse.Should().ThrowAsync<ForbiddenAccessException>();
    }

    /*
     * FAIL-CLOSED. Null and empty mean opposite things in GuestPresenterScope: null is "not a guest,
     * do not filter". A guest whose member row has not resolved must get the EMPTY set, so a mutation
     * turning that into null — which looks harmless, since "we could not resolve them" reads a lot
     * like "no scoping applies" — hands them the committee-wide read this change exists to remove.
     */
    [Fact]
    public async Task A_guest_whose_member_row_does_not_resolve_sees_nothing()
    {
        var guest = Principal("kc-guest", AcmpRoles.Guest);
        await using var db = await SeedAsync(guest);

        var list = await List(db, NobodyResolves(), guest).Handle(new GetMeetingsQuery(), default);

        list.Should().BeEmpty();
    }

    [Fact]
    public async Task A_committee_member_still_sees_every_meeting()
    {
        var member = Principal("kc-member", AcmpRoles.Member);
        await using var db = await SeedAsync(member);

        var list = await List(db, NobodyResolves(), member).Handle(new GetMeetingsQuery(), default);

        // NobodyResolves() is deliberate: an insider must never reach the directory at all, so if a
        // refactor started resolving every caller this returns null and the count drops to zero.
        list.Should().HaveCount(2);
    }

    /*
     * A principal holding Guest AND a committee role is an INSIDER who happens to be listed as a guest
     * somewhere, and must not be locked out of their own committee — GuestSurfaceMiddleware has always
     * said so, and the row-level half must agree, or the two halves disagree about who a guest is.
     */
    [Fact]
    public async Task A_guest_who_also_holds_a_committee_role_is_treated_as_an_insider()
    {
        var insider = Principal("kc-both", AcmpRoles.Guest, AcmpRoles.Member);
        await using var db = await SeedAsync(insider);

        var list = await List(db, NobodyResolves(), insider).Handle(new GetMeetingsQuery(), default);

        list.Should().HaveCount(2);
    }
}
