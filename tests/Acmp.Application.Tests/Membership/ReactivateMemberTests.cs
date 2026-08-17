using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Features.DeactivateMember;
using Acmp.Modules.Membership.Application.Features.ExpireGuestAccess;
using Acmp.Modules.Membership.Application.Features.ReactivateMember;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Membership;

// FR-162 / AC-111 — the fix for DEF-085, where a disabled member was locked out PERMANENTLY: no
// reactivation path existed, re-inviting throws on the duplicate email, and the Keycloak user can
// never be deleted (DEF-029).
//
// ⚠ THE WINDOW IS THE HALF THAT MATTERS, and it is why "flip Status back" is not the fix.
// PrincipalRevalidator refuses a member whose AccessExpiresAt has passed INDEPENDENTLY of Status, so
// a reactivation that touches only Status yields an Active-but-refused member — a state that reads
// as repaired and is not. Every test below that restores a guest asserts the window too.
public class ReactivateMemberTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private static MembershipDbContext NewDb()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns("kc-admin");
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>()
                .UseInMemoryDatabase("reactivate-" + Guid.NewGuid()).Options,
            clock, user);
    }

    private static ReactivateMemberHandler Build(
        MembershipDbContext db, IIdentityProvider? identity = null, IAuditSink? audit = null) =>
        new(db, audit ?? Substitute.For<IAuditSink>(),
            identity is null ? Array.Empty<IIdentityProvider>() : new[] { identity });

    private static async Task<CommitteeMember> Member(
        MembershipDbContext db, CommitteeRole role, DateTimeOffset? expiresAt)
    {
        var member = CommitteeMember.Provision("kc-bob", "Bob B.", "bob@example.com", role, Now);
        member.SetAccessWindow(expiresAt);
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    [Fact]
    public async Task An_admin_deactivated_member_is_restored_to_active_and_audited()
    {
        await using var db = NewDb();
        var member = await Member(db, CommitteeRole.Member, expiresAt: null);
        var audit = Substitute.For<IAuditSink>();

        // Disabled through the real admin path, not by poking Status — so this test breaks if
        // deactivation ever changes shape.
        await new DeactivateMemberHandler(db, Substitute.For<ICurrentUser>(), Substitute.For<IAuditSink>())
            .Handle(new DeactivateMemberCommand(member.PublicId), CancellationToken.None);
        member.Status.Should().Be(MembershipStatus.Disabled, "precondition");

        await Build(db, audit: audit).Handle(new ReactivateMemberCommand(member.PublicId), CancellationToken.None);

        (await db.Members.SingleAsync()).Status.Should().Be(MembershipStatus.Active);
        await audit.Received(1).EmitEnrichedAsync(
            "Membership.MemberReactivated", nameof(CommitteeMember), member.PublicId.ToString(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ⚠ THE CASE THE WHOLE DEFECT IS ABOUT. The hourly sweep disables guests automatically, so this
    // is the state a repeat presenter arrives in through NORMAL OPERATION, not admin error.
    [Fact]
    public async Task A_guest_the_sweep_disabled_comes_back_with_the_expired_window_CLEARED()
    {
        await using var db = NewDb();
        var guest = await Member(db, CommitteeRole.Guest, expiresAt: Now.AddHours(-1));
        var identity = Substitute.For<IIdentityProvider>();

        await new ExpireGuestAccessHandler(
                db, ClockAt(Now), Substitute.For<IAuditSink>(), new[] { identity })
            .Handle(new ExpireGuestAccessCommand(), CancellationToken.None);
        guest.Status.Should().Be(MembershipStatus.Disabled, "precondition: the sweep disabled them");

        await Build(db, identity).Handle(new ReactivateMemberCommand(guest.PublicId), CancellationToken.None);

        var stored = await db.Members.SingleAsync();
        stored.Status.Should().Be(MembershipStatus.Active);
        stored.AccessExpiresAt.Should().BeNull(
            "an omitted window CLEARS it — leaving the stale expiry would keep PrincipalRevalidator "
            + "refusing them, which is the Active-but-refused state AC-111 forbids");
        stored.HasExpired(Now).Should().BeFalse("the member can actually be admitted again");
    }

    // ⚠ THE KEYCLOAK LEG, ASSERTED POSITIVELY. The sweep calls DisableUserAsync, so the account's
    // login is dead; SC-017 added EnableUserAsync precisely so reactivation can undo that. Asserting
    // the CALL rather than inferring it from local state is the point — the local row would look
    // identical either way, and "they can sign in" is exactly what the local row cannot tell you.
    [Fact]
    public async Task Reactivation_re_enables_the_keycloak_account_the_sweep_disabled()
    {
        await using var db = NewDb();
        var guest = await Member(db, CommitteeRole.Guest, expiresAt: Now.AddHours(-1));
        var identity = Substitute.For<IIdentityProvider>();

        await Build(db, identity).Handle(new ReactivateMemberCommand(guest.PublicId), CancellationToken.None);

        await identity.Received(1).EnableUserAsync(guest.KeycloakUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_guest_can_be_re_windowed_for_another_meeting_instead_of_being_cleared()
    {
        await using var db = NewDb();
        var guest = await Member(db, CommitteeRole.Guest, expiresAt: Now.AddHours(-1));
        var newWindow = Now.AddDays(7);

        await Build(db).Handle(new ReactivateMemberCommand(guest.PublicId, newWindow), CancellationToken.None);

        var stored = await db.Members.SingleAsync();
        stored.Status.Should().Be(MembershipStatus.Active);
        stored.AccessExpiresAt.Should().Be(newWindow, "a guest stays time-boxed when a window is supplied");
        stored.HasExpired(Now).Should().BeFalse();
    }

    // Where no identity provider is configured — every environment with in-app user management off —
    // the handler must still work rather than fail at composition. IEnumerable<IIdentityProvider> is
    // what makes that true, and this asserts it instead of trusting the DI shape.
    [Fact]
    public async Task Reactivation_works_with_no_identity_provider_configured()
    {
        await using var db = NewDb();
        var member = await Member(db, CommitteeRole.Member, expiresAt: null);
        member.Deactivate();
        await db.SaveChangesAsync();

        await Build(db, identity: null).Handle(new ReactivateMemberCommand(member.PublicId), CancellationToken.None);

        (await db.Members.SingleAsync()).Status.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task An_unknown_member_is_not_found()
    {
        await using var db = NewDb();

        var act = () => Build(db).Handle(new ReactivateMemberCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void Only_an_administrator_may_reactivate()
        => new ReactivateMemberCommand(Guid.NewGuid()).AllowedRoles
            .Should().ContainSingle().Which.Should().Be(nameof(CommitteeRole.Administrator));

    private static IClock ClockAt(DateTimeOffset now)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        return clock;
    }
}
