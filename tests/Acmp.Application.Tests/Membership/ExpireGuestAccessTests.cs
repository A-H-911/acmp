using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Features.ExpireGuestAccess;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Membership;

// FR-159 / AC-092 — the guest-expiry sweep (defence in depth behind ADR-0039's per-request refusal).
//
// The sweep is asserted by its EFFECT on state, never by "DisableUserAsync was called" alone: the
// local status is what the revalidation middleware reads, so a test that only checked the Keycloak
// call would pass on a sweep that left the member enabled in ACMP.
public class ExpireGuestAccessTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    private static MembershipDbContext NewDb()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns("system");
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase("expiry-" + Guid.NewGuid()).Options,
            clock, user);
    }

    private static ExpireGuestAccessHandler Build(
        MembershipDbContext db, IIdentityProvider? identity, IAuditSink? audit = null, DateTimeOffset? now = null)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now ?? Now);
        return new ExpireGuestAccessHandler(
            db, clock, audit ?? Substitute.For<IAuditSink>(),
            identity is null ? Array.Empty<IIdentityProvider>() : new[] { identity });
    }

    private static CommitteeMember Guest(MembershipDbContext db, string sub, DateTimeOffset? expiresAt)
    {
        var member = CommitteeMember.Provision(sub, "Guest " + sub, $"{sub}@ext.example", CommitteeRole.Guest, Now);
        member.SetAccessWindow(expiresAt);
        db.Members.Add(member);
        db.SaveChanges();
        return member;
    }

    [Fact]
    public async Task A_guest_past_their_window_is_disabled_locally_and_in_keycloak()
    {
        using var db = NewDb();
        var member = Guest(db, "kc-guest", Now.AddMinutes(-1));
        var identity = Substitute.For<IIdentityProvider>();

        var result = await Build(db, identity).Handle(new ExpireGuestAccessCommand(), default);

        result.Should().Be(new GuestAccessSweepResult(1, 1));
        db.Members.Single(m => m.PublicId == member.PublicId).Status.Should().Be(MembershipStatus.Disabled);
        await identity.Received(1).DisableUserAsync("kc-guest", Arg.Any<CancellationToken>());
        // Disable, never delete — deleting strands the member row forever (DEF-029).
        await identity.DidNotReceive().SetRealmRolesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_guest_still_inside_their_window_is_untouched()
    {
        using var db = NewDb();
        var member = Guest(db, "kc-guest", Now.AddHours(1));
        var identity = Substitute.For<IIdentityProvider>();

        var result = await Build(db, identity).Handle(new ExpireGuestAccessCommand(), default);

        result.Expired.Should().Be(0);
        db.Members.Single(m => m.PublicId == member.PublicId).Status.Should().Be(MembershipStatus.Active);
        await identity.DidNotReceive().DisableUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_ordinary_member_with_no_expiry_is_never_swept()
    {
        using var db = NewDb();
        var member = Guest(db, "kc-member", expiresAt: null);
        var identity = Substitute.For<IIdentityProvider>();

        var result = await Build(db, identity, now: Now.AddYears(5)).Handle(new ExpireGuestAccessCommand(), default);

        // The whole committee has a null window. A sweep that treated null as "expired" would
        // disable every member on its first run.
        result.Expired.Should().Be(0);
        db.Members.Single(m => m.PublicId == member.PublicId).Status.Should().Be(MembershipStatus.Active);
    }

    // Idempotence is what makes a recurring job safe to re-run, and it is asserted by RUNNING IT
    // TWICE rather than by reading the query.
    [Fact]
    public async Task Running_the_sweep_twice_disables_and_audits_once()
    {
        using var db = NewDb();
        Guest(db, "kc-guest", Now.AddMinutes(-1));
        var identity = Substitute.For<IIdentityProvider>();
        var audit = Substitute.For<IAuditSink>();
        var handler = Build(db, identity, audit);

        var first = await handler.Handle(new ExpireGuestAccessCommand(), default);
        var second = await handler.Handle(new ExpireGuestAccessCommand(), default);

        first.Expired.Should().Be(1);
        second.Expired.Should().Be(0, "an already-disabled member must not be swept again");
        await identity.Received(1).DisableUserAsync("kc-guest", Arg.Any<CancellationToken>());
        await audit.Received(1).EmitEnrichedAsync(
            "Membership.GuestAccessExpired", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ⚠ THE CASE THAT MUST NOT SILENTLY DO NOTHING. IIdentityProvider is registered only when
    // KeycloakAdmin is configured, which is NO environment by default — so if the sweep depended on
    // it, guest expiry would be a no-op everywhere while looking healthy.
    [Fact]
    public async Task With_no_identity_provider_the_member_is_STILL_disabled_locally()
    {
        using var db = NewDb();
        var member = Guest(db, "kc-guest", Now.AddMinutes(-1));

        var result = await Build(db, identity: null).Handle(new ExpireGuestAccessCommand(), default);

        // Local disable is the half ADR-0039's revalidation reads, so access is closed even though
        // the Keycloak login is not. The counts differ on purpose, so a caller can SEE that.
        result.Should().Be(new GuestAccessSweepResult(1, 0));
        db.Members.Single(m => m.PublicId == member.PublicId).Status.Should().Be(MembershipStatus.Disabled);
    }

    [Fact]
    public async Task An_empty_sweep_touches_nothing()
    {
        using var db = NewDb();
        var identity = Substitute.For<IIdentityProvider>();
        var audit = Substitute.For<IAuditSink>();

        var result = await Build(db, identity, audit).Handle(new ExpireGuestAccessCommand(), default);

        result.Should().Be(new GuestAccessSweepResult(0, 0));
        await audit.DidNotReceiveWithAnyArgs().EmitEnrichedAsync(default!, default!, default!);
    }

    [Fact]
    public async Task A_guest_at_EXACTLY_the_expiry_instant_is_not_yet_swept()
    {
        using var db = NewDb();
        var member = Guest(db, "kc-guest", Now);

        var result = await Build(db, identity: null, now: Now).Handle(new ExpireGuestAccessCommand(), default);

        // Same exclusive boundary the request-time check uses (CommitteeMember.HasExpired), so the
        // sweep and the API cannot disagree about whether access has ended — DEC-037's requirement
        // that the banner and the server read one value applies to this third reader too.
        result.Expired.Should().Be(0);
        db.Members.Single(m => m.PublicId == member.PublicId).Status.Should().Be(MembershipStatus.Active);
    }
}
