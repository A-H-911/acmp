using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Features.ProvisionCurrentUser;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Stream = Acmp.Modules.Membership.Domain.Stream;

namespace Acmp.Application.Tests.Membership;

// DEF-075 — TWO CONCURRENT FIRST-LOGINS FOR ONE SUBJECT. POST /api/members/me is documented idempotent
// by the SPA and by the e2e helpers, and it was not: the existence check and the insert are not atomic,
// so two requests that both find no row both Add, and the unique index refuses the loser with a 500.
//
// ⚠ WHY THE RACE IS SIMULATED RATHER THAN RUN, and it is the honest trade rather than the convenient
// one: the InMemory provider does NOT enforce unique indexes, so a genuinely concurrent test here would
// pass whether or not the fix existed — the exact shape of DEF-066, where four green suites sat over a
// write that could never work on SQL Server. So these tests do not pretend to reproduce the race. They
// reproduce THE STATE THE LOSER FINDS ITSELF IN — a save that fails with DbUpdateException while the
// winner's row is already committed and readable — and assert the recovery from there.
//
// The other half — that SQL Server really does block the second insert until the winner commits, and
// really does leave the transaction usable afterwards — is proven where it can only be proven: on a
// real database, by the AC-011 e2e leg whose first run produced this defect.
public class ProvisionCurrentUserConcurrencyTests
{
    private const string Sub = "kc-racing-subject";

    /// <summary>
    /// Delegates everything to a real context except the FIRST SaveChanges, which lands the winning row
    /// and then fails the way SQL Server does — the duplicate is only raised once the other transaction
    /// has committed and released its key lock, so the winner is already visible when we are refused.
    /// </summary>
    private sealed class LosesTheRaceOnce : IMembershipDbContext
    {
        private readonly MembershipDbContext _inner;
        private readonly Func<CancellationToken, Task>? _landTheWinner;
        private bool _fired;

        public LosesTheRaceOnce(MembershipDbContext inner, Func<CancellationToken, Task>? landTheWinner)
        {
            _inner = inner;
            _landTheWinner = landTheWinner;
        }

        public DbSet<CommitteeMember> Members => _inner.Members;
        public DbSet<Stream> Streams => _inner.Streams;
        public DbSet<TopicCapabilityGrant> TopicCapabilities => _inner.TopicCapabilities;
        public DbSet<Delegation> Delegations => _inner.Delegations;

        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            if (_fired)
                return await _inner.SaveChangesAsync(ct);

            _fired = true;
            if (_landTheWinner is not null)
                await _landTheWinner(ct);

            throw new DbUpdateException(
                "Cannot insert duplicate key row in object 'membership.committee_members' with unique " +
                "index 'IX_committee_members_KeycloakUserId'.");
        }
    }

    private static ICurrentUser CurrentUser(string sub, string name, string email, params string[] roles)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(sub);
        user.DisplayName.Returns(name);
        user.Email.Returns(email);
        user.Roles.Returns(roles);
        return user;
    }

    private static IClock Clock()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return clock;
    }

    private static MembershipDbContext Context(string store, IClock clock, ICurrentUser user) =>
        new(new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase(store).Options, clock, user);

    /// A context, and the loser's view of it with the winner landing mid-save.
    private static (LosesTheRaceOnce Db, CommitteeMember Winner, MembershipDbContext Inner) Racing(
        string store, IClock clock, ICurrentUser user)
    {
        var winner = CommitteeMember.Provision(Sub, "Racing Person", "racer@acmp.test",
            CommitteeRole.Secretary, clock.UtcNow);

        var inner = Context(store, clock, user);

        // Landed through a SEPARATE context on the same InMemory store, so it arrives the way another
        // request's committed row would rather than riding on this context's pending changes.
        var db = new LosesTheRaceOnce(inner, async ct =>
        {
            await using var other = Context(store, clock, user);
            other.Members.Add(winner);
            await other.SaveChangesAsync(ct);
        });

        return (db, winner, inner);
    }

    [Fact]
    public async Task The_loser_of_a_concurrent_first_provision_returns_the_winning_row_instead_of_failing()
    {
        var user = CurrentUser(Sub, "Racing Person", "racer@acmp.test", "Secretary");
        var clock = Clock();
        var (db, winner, inner) = Racing("race-" + Guid.NewGuid(), clock, user);
        await using var _ = inner;

        var profile = await new ProvisionCurrentUserHandler(db, user, clock, Substitute.For<IAuditSink>())
            .Handle(new ProvisionCurrentUserCommand(), CancellationToken.None);

        // THE CALLER GETS A PROFILE, NOT A 500 — the endpoint keeps the idempotency contract it
        // advertises. Asserted on the WINNER's PublicId specifically: a profile built from our own
        // detached, never-persisted entity would satisfy a weaker assertion while handing the caller an
        // id that exists in no table.
        profile.PublicId.Should().Be(winner.PublicId);
        profile.Role.Should().Be(nameof(CommitteeRole.Secretary));
    }

    [Fact]
    public async Task The_loser_leaves_no_second_member_row_behind()
    {
        var user = CurrentUser(Sub, "Racing Person", "racer@acmp.test", "Secretary");
        var clock = Clock();
        var store = "race-" + Guid.NewGuid();
        var (db, _, inner) = Racing(store, clock, user);
        await using var __ = inner;

        await new ProvisionCurrentUserHandler(db, user, clock, Substitute.For<IAuditSink>())
            .Handle(new ProvisionCurrentUserCommand(), CancellationToken.None);

        // The detach is the load-bearing step and it is invisible from the returned DTO. Without it the
        // loser's own Added entity is still pending, and the next SaveChanges re-submits the very insert
        // that was just refused — so this reads the STORE rather than the response.
        await using var reader = Context(store, clock, user);
        (await reader.Members.CountAsync(m => m.KeycloakUserId == Sub))
            .Should().Be(1, "the losing request must not add a second row for the same subject");
    }

    [Fact]
    public async Task The_loser_does_not_claim_it_provisioned_the_member()
    {
        var user = CurrentUser(Sub, "Racing Person", "racer@acmp.test", "Secretary");
        var clock = Clock();
        var (db, _, inner) = Racing("race-" + Guid.NewGuid(), clock, user);
        await using var __ = inner;
        var audit = Substitute.For<IAuditSink>();

        await new ProvisionCurrentUserHandler(db, user, clock, audit)
            .Handle(new ProvisionCurrentUserCommand(), CancellationToken.None);

        // The audit log is the committee's record of what happened, and only ONE of these two requests
        // provisioned anybody. A loser that still emits MemberProvisioned writes a permanent,
        // append-only row (INV-005) asserting an event that did not occur.
        await audit.DidNotReceive().EmitEnrichedAsync("Membership.MemberProvisioned",
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_save_failure_that_is_NOT_the_race_still_fails_loudly()
    {
        // The recovery is scoped to the one situation it understands. A DbUpdateException with no
        // winning row behind it is a different fault wearing the same exception type, and absorbing it
        // would turn every future write failure on this path into a silent success — which is how a
        // recovery path becomes a bug nobody can see.
        var user = CurrentUser("kc-no-winner", "Nobody", "nobody@acmp.test", "Secretary");
        var clock = Clock();
        await using var inner = Context("race-" + Guid.NewGuid(), clock, user);
        var db = new LosesTheRaceOnce(inner, landTheWinner: null);

        var act = () => new ProvisionCurrentUserHandler(db, user, clock, Substitute.For<IAuditSink>())
            .Handle(new ProvisionCurrentUserCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
