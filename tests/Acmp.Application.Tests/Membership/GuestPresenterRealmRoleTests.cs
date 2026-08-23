using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Features.InviteUser;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Directory;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Membership;

// DEF-076 / SC-012 — AN INVITED GUEST PRESENTER MUST ARRIVE WITH THE Guest REALM ROLE, AND AN
// ORDINARY INVITEE MUST NOT.
//
// Both callers run one shared body (MemberInvitation.InviteAsync), which until now called
// CreateUserAsync and nothing else. That is DELIBERATE for FR-156 — an invited member is inert until
// an administrator assigns a role through AssignRoles — and it was a defect for FR-159, whose guest
// has no follow-up actor at all: the Secretary's single action from the meeting screen is the whole
// use case (DEC-037). The result was a guest whose committee_members row said Guest with a timed
// window, over a Keycloak account holding no role, so every AllowedRoles check refused them and
// GuestSurfaceMiddleware did not even classify them as a guest — IsGuestOnly reads the TOKEN.
//
// ⚠ THE ASYMMETRY IS THE FEATURE AND IS WHY BOTH DIRECTIONS ARE ASSERTED HERE. A later tidy-up that
// re-merges the two callers into one uniform behaviour would either restore this defect or strip
// FR-156's inert-invite property, depending on which way it merged — and only one of these two tests
// would notice each of those.
public class GuestPresenterRealmRoleTests
{
    private static MembershipDbContext NewDb()
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-secretary");
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>()
                .UseInMemoryDatabase("guest-role-" + Guid.NewGuid()).Options,
            clock, user);
    }

    private static IClock Clock()
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        return c;
    }

    private static IIdentityProvider Identity(string subject = "kc-new-guest")
    {
        var identity = Substitute.For<IIdentityProvider>();
        identity.CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new InvitedAccount(subject, "temp-Passw0rd!"));
        return identity;
    }

    [Fact]
    public async Task A_guest_presenter_invite_grants_the_Guest_realm_role()
    {
        await using var db = NewDb();
        var identity = Identity();
        var clock = Clock();

        await new GuestProvisioner(db, identity, Substitute.For<IAuditSink>(), clock)
            .InviteGuestAsync("guest@outside.test", "A Guest", clock.UtcNow.AddDays(1));

        // The ROLE is asserted, not merely that some assignment happened: granting the wrong role
        // would leave the guest just as unable to reach /session, and just as invisible to the guest
        // gate, as granting none.
        await identity.Received(1).SetRealmRolesAsync(
            "kc-new-guest",
            Arg.Is<IReadOnlyCollection<string>>(r => r.Contains(AcmpRoles.Guest)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_ordinary_member_invite_stays_inert()
    {
        await using var db = NewDb();
        var identity = Identity("kc-new-member");

        await new InviteUserHandler(db, identity, Clock(), Substitute.For<IAuditSink>())
            .Handle(new InviteUserCommand("member@acmp.test", "A Member", Array.Empty<Guid>()), default);

        // FR-156's two-step flow: the account exists and can sign in, but holds nothing until an
        // administrator decides. This is the half SC-012 promises NOT to change, and it is the half a
        // careless de-duplication of the two callers would quietly take away.
        await identity.DidNotReceive().SetRealmRolesAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_realm_role_is_granted_BEFORE_the_member_row_is_written()
    {
        await using var db = NewDb();
        var identity = Identity();
        var clock = Clock();

        // ORDER IS A SAFETY PROPERTY, NOT A STYLE CHOICE, and it is invisible in the finished state —
        // both orders end with a role and a row, so nothing else here could catch a reordering.
        //
        // If the grant runs AFTER the insert and fails, what survives is exactly DEF-076: a member row
        // claiming Guest with a timed window, over a token holding nothing, and nothing ever re-reads
        // the row to re-derive the grant. If it runs BEFORE and fails, what survives is a Keycloak
        // account with no member row — which JIT provisioning already handles on first login, the same
        // failure the insert itself has always been allowed to leave behind.
        var rowExistedAtGrantTime = true;
        identity
            .When(i => i.SetRealmRolesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>()))
            .Do(_ => rowExistedAtGrantTime = db.Members.Any(m => m.KeycloakUserId == "kc-new-guest"));

        await new GuestProvisioner(db, identity, Substitute.For<IAuditSink>(), clock)
            .InviteGuestAsync("guest@outside.test", "A Guest", clock.UtcNow.AddDays(1));

        rowExistedAtGrantTime.Should().BeFalse(
            "the grant must precede the member row, so a failure leaves no row claiming access the token does not have");
        (await db.Members.CountAsync(m => m.KeycloakUserId == "kc-new-guest")).Should().Be(1);
    }

    [Fact]
    public async Task The_guest_row_still_carries_the_Guest_role_and_the_access_window()
    {
        await using var db = NewDb();
        var clock = Clock();
        var expires = clock.UtcNow.AddDays(2);

        await new GuestProvisioner(db, Identity(), Substitute.For<IAuditSink>(), clock)
            .InviteGuestAsync("guest@outside.test", "A Guest", expires);

        // The local half of the contract, asserted so the fix cannot be read as having moved the
        // guest's identity into Keycloak and out of the register: the window is what ADR-0039's
        // per-request refusal and the hourly sweep both read.
        var member = await db.Members.SingleAsync(m => m.KeycloakUserId == "kc-new-guest");
        member.Role.Should().Be(CommitteeRole.Guest);
        member.AccessExpiresAt.Should().Be(expires);
    }
}
