using Acmp.Modules.Decisions.Application.Features.CastBallot;
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

    private ServiceProvider BuildProvider(bool faultAudit)
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

        // The voter is an authenticated Member matching the seeded ballot's sub.
        services.AddScoped<ICurrentUser>(_ => new StubVoter("voter-1"));

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

    private AuditDbContext NewAuditContext() => new(
        new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer(_fixture.ConnectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.Schema))
            .Options);
}

// An authenticated committee Member whose sub matches the seeded eligible voter.
internal sealed class StubVoter : ICurrentUser
{
    private readonly string _sub;
    public StubVoter(string sub) => _sub = sub;
    public bool IsAuthenticated => true;
    public string? UserId => _sub;
    public string? UserName => _sub;
    public string? Email => $"{_sub}@acmp.gov";
    public string? DisplayName => "Voter One";
    public IReadOnlyCollection<string> Roles => new[] { "Member" };
    public bool IsInRole(string role) => role == "Member";
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
