using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Identity;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Membership;

// ADR-0039 / AC-090 / AC-092 — the per-request veto over an otherwise-valid token.
//
// Every refusal is FORCED and asserted as a verdict, never as "the collaborator was called". The
// ALLOW cases matter just as much: this sits on the path of every authenticated request, so a false
// refusal is an outage rather than a bug, and the first test below is the one that would cause it.
public class PrincipalRevalidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    private static MembershipDbContext NewDb()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns("kc-system");
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase("reval-" + Guid.NewGuid()).Options,
            clock, user);
    }

    private static PrincipalRevalidator Build(MembershipDbContext db, DateTimeOffset? now = null)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now ?? Now);
        return new PrincipalRevalidator(db, clock);
    }

    private static CommitteeMember Seed(MembershipDbContext db, string sub = "kc-1")
    {
        var member = CommitteeMember.Provision(sub, "Test Person", $"{sub}@acmp.gov", CommitteeRole.Member, Now);
        db.Members.Add(member);
        db.SaveChanges();
        return member;
    }

    // ⚠ THE MOST IMPORTANT TEST HERE. ADR-0004 provisions the local profile JUST IN TIME on first
    // login, so a valid token legitimately exists BEFORE its member row does. If this returned
    // anything but Allowed, every first login in the system would be refused — on the path of every
    // request — which is the worst consequence ADR-0039 records about itself.
    [Fact]
    public async Task A_subject_with_no_member_row_is_ALLOWED_so_JIT_provisioning_still_happens()
    {
        using var db = NewDb();

        var verdict = await Build(db).RevalidateAsync("kc-never-seen", Now);

        verdict.Should().Be(PrincipalVerdict.Allowed);
    }

    [Fact]
    public async Task Roles_changed_AFTER_the_token_was_issued_is_STALE()
    {
        using var db = NewDb();
        var member = Seed(db);
        member.ApplyAssignedRole(CommitteeRole.Reviewer, Now);
        db.SaveChanges();

        var verdict = await Build(db).RevalidateAsync("kc-1", Now.AddMinutes(-1));

        verdict.Should().Be(PrincipalVerdict.Stale, "the token still carries the roles it had a minute ago");
    }

    [Fact]
    public async Task Roles_changed_BEFORE_the_token_was_issued_is_allowed()
    {
        using var db = NewDb();
        var member = Seed(db);
        member.ApplyAssignedRole(CommitteeRole.Reviewer, Now.AddMinutes(-5));
        db.SaveChanges();

        var verdict = await Build(db).RevalidateAsync("kc-1", Now);

        // The token was minted after the change, so it already carries the new roles. Refusing here
        // would loop: every renewal would be refused for the same reason.
        verdict.Should().Be(PrincipalVerdict.Allowed);
    }

    [Fact]
    public async Task A_member_whose_roles_were_never_assigned_in_app_is_allowed()
    {
        using var db = NewDb();
        Seed(db);

        var verdict = await Build(db).RevalidateAsync("kc-1", Now);

        // RolesChangedAt is null for everyone predating FR-157 — i.e. the entire existing committee.
        verdict.Should().Be(PrincipalVerdict.Allowed);
    }

    [Fact]
    public async Task A_disabled_member_is_refused_even_with_a_fresh_token()
    {
        using var db = NewDb();
        var member = Seed(db);
        member.Deactivate();
        db.SaveChanges();

        var verdict = await Build(db).RevalidateAsync("kc-1", Now);

        verdict.Should().Be(PrincipalVerdict.Disabled, "AC-058 blocks access; a valid token must not bypass it");
    }

    // AC-092, forced by ADVANCING PAST THE EXPIRY — which is what the AC demands, rather than by
    // reading a countdown.
    [Fact]
    public async Task A_guest_past_their_access_window_is_EXPIRED()
    {
        using var db = NewDb();
        var member = Seed(db, "kc-guest");
        member.SetAccessWindow(Now);
        db.SaveChanges();

        var verdict = await Build(db, now: Now.AddSeconds(1)).RevalidateAsync("kc-guest", Now);

        verdict.Should().Be(PrincipalVerdict.Expired);
    }

    [Fact]
    public async Task A_guest_AT_the_expiry_instant_is_still_allowed()
    {
        using var db = NewDb();
        var member = Seed(db, "kc-guest");
        member.SetAccessWindow(Now);
        db.SaveChanges();

        var verdict = await Build(db, now: Now).RevalidateAsync("kc-guest", Now);

        // "Expires AFTER the meeting" — the boundary is exclusive, and it is decided in ONE place
        // (CommitteeMember.HasExpired) so banner and API cannot disagree about it (DEC-037).
        verdict.Should().Be(PrincipalVerdict.Allowed);
    }

    [Fact]
    public async Task A_member_with_no_expiry_is_never_expired()
    {
        using var db = NewDb();
        Seed(db);

        var verdict = await Build(db, now: Now.AddYears(5)).RevalidateAsync("kc-1", Now.AddYears(5));

        verdict.Should().Be(PrincipalVerdict.Allowed, "null AccessExpiresAt is every ordinary member");
    }

    // Ordering is behaviour, not incidental: an expired guest told "your roles changed" would be
    // sent to renew a token against a session that no longer exists — a silent-renewal loop.
    [Fact]
    public async Task Expiry_is_reported_BEFORE_staleness_when_a_member_is_both()
    {
        using var db = NewDb();
        var member = Seed(db, "kc-guest");
        member.SetAccessWindow(Now);
        member.ApplyAssignedRole(CommitteeRole.Guest, Now);
        db.SaveChanges();

        var verdict = await Build(db, now: Now.AddMinutes(1)).RevalidateAsync("kc-guest", Now.AddMinutes(-1));

        verdict.Should().Be(PrincipalVerdict.Expired);
    }

    [Fact]
    public async Task An_empty_subject_is_allowed_rather_than_refused()
    {
        using var db = NewDb();

        var verdict = await Build(db).RevalidateAsync(string.Empty, Now);

        // Nothing to look up. Token validation has already run; this narrows what a valid token may
        // do, it does not stand in for authentication.
        verdict.Should().Be(PrincipalVerdict.Allowed);
    }

    [Fact]
    public async Task The_expiry_boundary_is_decided_by_the_domain_not_re_implemented_here()
    {
        using var db = NewDb();
        var member = Seed(db, "kc-guest");
        member.SetAccessWindow(Now);

        // Same rule the /session banner will read (DEC-037: one value, one answer).
        member.HasExpired(Now).Should().BeFalse();
        member.HasExpired(Now.AddTicks(1)).Should().BeTrue();
        await Task.CompletedTask;
    }
}
