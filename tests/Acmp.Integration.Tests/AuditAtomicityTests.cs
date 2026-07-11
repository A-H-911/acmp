using System.Diagnostics;
using Acmp.Modules.Decisions.Application.Features.CastBallot;
using Acmp.Modules.Decisions.Application.Features.CloseVote;
using Acmp.Modules.Decisions.Domain;
using Acmp.Modules.Decisions.Infrastructure;
using Acmp.Shared;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Integration.Tests;

// NFR-042 / ADR-0026 §Same-transaction atomicity. These are the tests EF-InMemory CANNOT run: it ignores
// transactions, so the whole point — that a decision/vote state change and its audit append commit or roll
// back TOGETHER on one shared connection — is only observable against real SQL Server. The suite drives a
// FULLY composed DI graph through IMediator (shared connection, auto-applied interceptors, TransactionBehavior,
// the real SqlAuditSink), exactly as the API host wires it, so it also proves the DI wiring end to end.
//
// Three scenarios, one per invariant the design promises:
//   1. Happy path      — CastBallot commits BOTH the ballot and its BallotCast audit row.
//   2. Audit failure   — an append that throws AFTER writing rolls the ballot back (no orphan mutation/row).
//   3. Denial survival — a double-cast's BallotDenied row PERSISTS even though the command then throws, because
//                        a denial writes no module entity → begins no transaction → autocommits.
[Collection(SqlBackstopCollection.Name)]
public sealed class AuditAtomicityTests
{
    private readonly SqlBackstopFixture _fixture;

    public AuditAtomicityTests(SqlBackstopFixture fixture)
    {
        _fixture = fixture;
        // The fixture migrates the module schemas; the audit schema is provisioned here (idempotent) on its own
        // connection so this suite is self-contained.
        using var audit = NewAuditContext();
        audit.Database.Migrate();
    }

    [Fact]
    public async Task CastBallot_commits_both_the_ballot_and_its_audit_row()
    {
        var voteId = SeedOpenVote("VOTE-ATOM-1");
        await using var provider = BuildProvider(faultAudit: false);

        await Send(provider, new CastBallotCommand(voteId, "Approve", null));

        BallotWasCast(voteId).Should().BeTrue("the command completed cleanly");
        AuditRowCount("Decisions.BallotCast").Should().BeGreaterThan(0, "the paired audit row committed with it");

        // The enriched row is self-describing. Casting mutates the Ballot CHILD, not the Vote's own scalars, so
        // the aggregate-root subject has no scalar delta — before/after is null. This is the known, documented
        // limitation of the "subject = aggregate root" convention for child-entity mutations (not a stray null).
        var row = LatestAuditRow("Decisions.BallotCast")!;
        row.SubjectType.Should().Be(nameof(Vote));
        row.SubjectId.Should().Be(voteId.ToString());
        row.ActorUserId.Should().Be("voter-1", "the actor comes from ICurrentUser, not a call argument");
        row.AfterJson.Should().BeNull("a ballot cast changes the child Ballot, not the Vote's own scalars");
    }

    [Fact]
    public async Task CloseVote_persists_a_populated_before_after_on_the_enriched_row()
    {
        // The decisive end-to-end check (advisor): that before/after actually DRAINS through a real handler + DI
        // + SaveChanges — the interceptor keyed a delta by (ClrType.Name, PublicId) and the handler emitted the
        // matching (nameof(Vote), PublicId). CloseVote mutates the Vote's OWN scalars (Status/ClosedAt/Tally), so
        // AfterJson must be non-null. Seed an Open vote with one cast ballot so the MinCast=1 quorum is met.
        var voteId = SeedOpenVote("VOTE-CLOSE-1", withPriorCastByVoter: true);
        await using var provider = BuildProvider(faultAudit: false, sub: "chair-1", role: "Chairman");

        using var activity = new Activity("audit-e2e");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        await Send(provider, new CloseVoteCommand(voteId));

        var row = LatestAuditRow("Decisions.VoteClosed")!;
        row.SubjectType.Should().Be(nameof(Vote));
        row.SubjectId.Should().Be(voteId.ToString());
        row.ActorUserId.Should().Be("chair-1", "the actor comes from ICurrentUser");
        row.Outcome.Should().Be(AuditOutcome.Success);
        row.AfterJson.Should().NotBeNull("CloseVote mutates the Vote's OWN scalars — before/after MUST populate (AC-017)");
        row.AfterJson.Should().Contain("Status", "the Closed status transition is in the delta");
        row.CorrelationId.Should().Be(activity.TraceId.ToString(), "the ambient OTel trace id is captured (NFR-044)");
    }

    [Fact]
    public async Task Audit_append_failure_rolls_the_ballot_back()
    {
        var voteId = SeedOpenVote("VOTE-ATOM-2");
        var before = AuditRowCount("Decisions.BallotCast");
        await using var provider = BuildProvider(faultAudit: true);

        var act = () => Send(provider, new CastBallotCommand(voteId, "Approve", null));

        await act.Should().ThrowAsync<InvalidOperationException>();
        BallotWasCast(voteId).Should().BeFalse("the audit append threw, so the state change must roll back (NFR-042)");
        AuditRowCount("Decisions.BallotCast").Should().Be(before, "the audit row rolled back too — no orphan");
    }

    [Fact]
    public async Task Double_cast_denial_audit_survives_the_thrown_command()
    {
        var voteId = SeedOpenVote("VOTE-ATOM-3", withPriorCastByVoter: true);
        var before = AuditRowCount("Decisions.BallotDenied");
        await using var provider = BuildProvider(faultAudit: false);

        var act = () => Send(provider, new CastBallotCommand(voteId, "Approve", null));

        await act.Should().ThrowAsync<InvalidOperationException>();
        AuditRowCount("Decisions.BallotDenied").Should().Be(before + 1,
            "a denial writes no module entity → no transaction → its audit autocommits and survives the throw");
    }

    // ---- composition (mirrors the API host wiring, scoped to Decisions + the shared kernel) ----

    private ServiceProvider BuildProvider(bool faultAudit, string sub = "voter-1", string role = "Member")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Acmp"] = _fixture.ConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedKernel(config);
        services.AddDecisionsModule(config);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
            typeof(SharedKernelExtensions).Assembly,
            typeof(CastBallotCommand).Assembly));

        // The acting principal (role configurable) attributes the audit actor.
        services.AddScoped<ICurrentUser>(_ => new StubVoter(sub, role));

        // Scenario 2: wrap the real sink so the success append happens IN the transaction and THEN throws —
        // proving both the ballot and the audit row roll back together.
        if (faultAudit)
        {
            services.AddScoped<SqlAuditSink>();
            services.AddScoped<IAuditSink>(sp => new ThrowAfterAppendSink(sp.GetRequiredService<SqlAuditSink>()));
        }

        return services.BuildServiceProvider();
    }

    private static async Task Send(ServiceProvider provider, IRequest command)
    {
        // Async scope: the request scope owns the IAsyncDisposable AmbientTransaction, exactly as ASP.NET Core
        // disposes the per-request scope asynchronously.
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
    }

    // ---- seeding + assertions on independent connections (see committed state, never the pipeline's) ----

    private Guid SeedOpenVote(string key, bool withPriorCastByVoter = false)
    {
        var now = _fixture.Clock.UtcNow;
        var vote = Vote.Configure(key, Guid.NewGuid(), null, new[] { "Approve", "Reject" },
            allowAbstain: false, new QuorumRule(0, 1),
            new[] { new VoteEligibleVoter("voter-1", "Voter One") }, "seed-actor", now);
        vote.Open("seed-actor", presentEligibleCount: 1, now);
        if (withPriorCastByVoter)
            vote.Cast("voter-1", "Approve", null, now);

        using var db = _fixture.NewDecisionsSql();
        db.Votes.Add(vote);
        db.SaveChanges();
        return vote.PublicId;
    }

    private bool BallotWasCast(Guid voteId)
    {
        using var db = _fixture.NewDecisionsSql();
        var vote = db.Votes.AsNoTracking().Single(v => v.PublicId == voteId);
        return vote.Ballots.Any(b => b.VoterUserId == "voter-1" && b.HasCast);
    }

    private int AuditRowCount(string eventType)
    {
        using var audit = NewAuditContext();
        return audit.AuditEvents.AsNoTracking().Count(e => e.EventType == eventType);
    }

    private AuditEvent? LatestAuditRow(string eventType)
    {
        using var audit = NewAuditContext();
        return audit.AuditEvents.AsNoTracking()
            .Where(e => e.EventType == eventType).OrderByDescending(e => e.Sequence).FirstOrDefault();
    }

    private AuditDbContext NewAuditContext() => new(
        new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer(_fixture.ConnectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.Schema))
            .Options);
}

// An authenticated committee member (role configurable) whose sub attributes the audit actor.
internal sealed class StubVoter : ICurrentUser
{
    private readonly string _sub;
    private readonly string _role;
    public StubVoter(string sub, string role = "Member")
    {
        _sub = sub;
        _role = role;
    }
    public bool IsAuthenticated => true;
    public string? UserId => _sub;
    public string? UserName => _sub;
    public string? Email => $"{_sub}@acmp.gov";
    public string? DisplayName => "Voter One";
    public IReadOnlyCollection<string> Roles => new[] { _role };
    public bool IsInRole(string role) => role == _role;
}

// Calls the real sink (real append into the ambient transaction) then throws — the "audit fails after write"
// injection for the rollback test.
internal sealed class ThrowAfterAppendSink : IAuditSink
{
    private readonly IAuditSink _inner;
    public ThrowAfterAppendSink(IAuditSink inner) => _inner = inner;

    public async Task EmitAsync(string eventType, string? subject, object? data = null, CancellationToken ct = default)
    {
        await _inner.EmitAsync(eventType, subject, data, ct);
        throw new InvalidOperationException("Injected audit-append failure after the row was written.");
    }

    public async Task EmitEnrichedAsync(string action, string subjectType, string? subjectId,
        string outcome = AuditOutcome.Success, CancellationToken ct = default)
    {
        await _inner.EmitEnrichedAsync(action, subjectType, subjectId, outcome, ct);
        throw new InvalidOperationException("Injected audit-append failure after the row was written.");
    }
}
