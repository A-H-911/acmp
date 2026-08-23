using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Features.ExpireGuestAccess;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Authorization;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Membership;

// DEC-057 d3 — THE DOMAIN PREDICATE AND THE SQL THAT SUPERSEDES IT MUST AGREE.
//
// WHY THIS FILE EXISTS. CommitteeMember.HasExpired and Delegation.IsActiveAt have NO production
// caller, and DEF-084 reported them as unreachable. They are not forgotten code: both enforcement
// points evaluate the same window INSIDE an EF Where/AnyAsync, where a domain method cannot be
// translated to SQL, so wiring them was never an available option. The tell is the sibling
// TopicCapabilityGrant.IsActiveAt, which IS called — because it filters an already-materialised
// list rather than composing a query.
//
// So the rule is written twice by construction, and the operator chose to KEEP both copies and
// guard the duplication rather than delete the readable one. That is what this file is: the thing
// that makes "they agree" a measured fact instead of a comment.
//
// ⚠ IT COMPARES BEHAVIOUR, NOT TEXT. Each test runs the REAL production query — the actual
// ExpireGuestAccessHandler, the actual DelegationResolver — against the same rows the domain method
// judges, at instants placed exactly ON the boundary and one tick either side. A test that
// re-stated the SQL predicate inline would agree with itself forever and prove nothing; that is the
// vacuous-control shape this codebase has already paid for twice (DEF-056's NotContain assertions
// over a column of nulls, and the i18n scan that searched for the token it had just renamed).
//
// If either side's comparison drifts — a `<` becoming `<=`, an inclusive bound becoming exclusive —
// exactly one of these assertions fails and names which instant disagreed.
public class PredicateAgreementTests
{
    private static readonly DateTimeOffset Boundary = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    // ON the boundary and one tick either side. The tick matters: a whole-second offset would pass
    // against BOTH `<` and `<=`, so it could not tell the two apart — which is the only thing the
    // test is here to do.
    public static TheoryData<int> Offsets => new() { -1, 0, 1 };

    private static MembershipDbContext NewDb(DateTimeOffset now)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns("system");
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>()
                .UseInMemoryDatabase("agreement-" + Guid.NewGuid()).Options,
            clock, user);
    }

    private static IClock ClockAt(DateTimeOffset now)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    [Theory]
    [MemberData(nameof(Offsets))]
    public async Task HasExpired_agrees_with_the_sweep_query_at_the_boundary(int tickOffset)
    {
        var now = Boundary.AddTicks(tickOffset);

        using var db = NewDb(now);
        var member = CommitteeMember.Provision("kc-agree", "Guest", "guest@ext.example", CommitteeRole.Guest, Boundary);
        member.SetAccessWindow(Boundary);
        db.Members.Add(member);
        await db.SaveChangesAsync();

        // The domain's verdict.
        var domainSaysExpired = member.HasExpired(now);

        // The production query's verdict, obtained by RUNNING the sweep and observing whether it
        // acted on this member — the sweep's only observable output for a single row.
        await new ExpireGuestAccessHandler(
                db, ClockAt(now), Substitute.For<IAuditSink>(), Array.Empty<IIdentityProvider>())
            .Handle(new ExpireGuestAccessCommand(), default);

        var sweepSaysExpired = member.Status == MembershipStatus.Disabled;

        sweepSaysExpired.Should().Be(domainSaysExpired,
            "CommitteeMember.HasExpired and the ExpireGuestAccess query must judge the same instant "
            + $"identically (offset {tickOffset} ticks from the window end); a disagreement means the "
            + "readable predicate and the enforced one have drifted");
    }

    [Theory]
    [MemberData(nameof(Offsets))]
    public async Task IsActiveAt_agrees_with_the_delegation_resolver_at_the_start_boundary(int tickOffset) =>
        await AssertDelegationAgreement(Boundary.AddTicks(tickOffset), Boundary, Boundary.AddDays(1));

    [Theory]
    [MemberData(nameof(Offsets))]
    public async Task IsActiveAt_agrees_with_the_delegation_resolver_at_the_end_boundary(int tickOffset) =>
        await AssertDelegationAgreement(Boundary.AddTicks(tickOffset), Boundary.AddDays(-1), Boundary);

    private static async Task AssertDelegationAgreement(
        DateTimeOffset now, DateTimeOffset validFrom, DateTimeOffset validTo)
    {
        const string capability = "Topic.Triage";

        using var db = NewDb(now);
        var delegator = CommitteeMember.Provision("kc-from", "Delegator", "from@example", CommitteeRole.Chairman, Boundary);
        var delegatee = CommitteeMember.Provision("kc-to", "Delegatee", "to@example", CommitteeRole.Member, Boundary);
        db.Members.AddRange(delegator, delegatee);
        await db.SaveChangesAsync();

        var delegation = Delegation.Create(delegator.Id, delegatee.Id, capability, validFrom, validTo);
        db.Delegations.Add(delegation);
        await db.SaveChangesAsync();

        var domainSaysActive = delegation.IsActiveAt(now);

        var resolverSaysActive = await new DelegationResolver(db, ClockAt(now))
            .HasActiveDelegationAsync("kc-to", capability);

        resolverSaysActive.Should().Be(domainSaysActive,
            "Delegation.IsActiveAt and DelegationResolver's SQL window must judge the same instant "
            + $"identically (now={now:O}, window {validFrom:O}..{validTo:O}); both bounds are "
            + "INCLUSIVE and a drift to an exclusive one would widen or narrow every delegation "
            + "in the system silently");
    }
}
