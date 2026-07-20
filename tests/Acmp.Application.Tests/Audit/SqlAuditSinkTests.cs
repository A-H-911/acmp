using System.Reflection;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Acmp.Application.Tests.Audit;

// BL-066 (ADR-0009) — the durable, immutable, hash-chained AuditEvent store behind IAuditSink.
[Trait("Category", "Security")]
public class SqlAuditSinkTests
{
    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 7, 2, 10, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeUser : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public string? UserId { get; init; } = "kc-1";
        public string? UserName => UserId;
        public string? Email => null;
        public string? DisplayName => UserId;
        public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private static AuditDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(name).Options);

    private static SqlAuditSink Sink(AuditDbContext db, AuditChangeBuffer? buffer = null, ICurrentUser? user = null) =>
        new(db, new FixedClock(), user ?? new FakeUser(), buffer ?? new AuditChangeBuffer(),
            NullLogger<SqlAuditSink>.Instance);

    [Fact]
    public async Task First_emit_chains_off_genesis()
    {
        await using var db = NewDb(nameof(First_emit_chains_off_genesis));
        var sink = Sink(db);

        await sink.EmitAsync("Test.Created", "kc-1", new { X = 1 });

        // Re-read from a FRESH context so EF materializes the row via its parameterless ctor.
        await using var reader = NewDb(nameof(First_emit_chains_off_genesis));
        var row = await reader.AuditEvents.SingleAsync();
        row.PreviousHash.Should().Be(AuditEvent.Genesis);
        row.EventType.Should().Be("Test.Created");
        row.Subject.Should().Be("kc-1");
        row.DataJson.Should().Contain("1");
        row.Hash.Should().NotBeNullOrEmpty();
        row.Sequence.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Second_emit_links_to_previous_hash_and_verifies()
    {
        await using var db = NewDb(nameof(Second_emit_links_to_previous_hash_and_verifies));
        var sink = Sink(db);

        await sink.EmitAsync("A", "kc-1", new { X = 1 });
        await sink.EmitAsync("B", null, null);   // exercises the null-subject + null-data path

        var rows = await db.AuditEvents.OrderBy(e => e.Sequence).ToListAsync();
        rows.Should().HaveCount(2);
        rows[1].PreviousHash.Should().Be(rows[0].Hash);
        rows[1].DataJson.Should().BeNull();

        var result = AuditChainVerifier.Verify(rows);
        result.IsValid.Should().BeTrue();
        result.BrokenAtSequence.Should().BeNull();
    }

    [Fact]
    public void Verifier_flags_a_broken_chain_link()
    {
        var e1 = AuditEvent.CreateNext(AuditEvent.Genesis, DateTimeOffset.UnixEpoch, "A", "s", null);
        var e2 = AuditEvent.CreateNext("not-the-tip-hash", DateTimeOffset.UnixEpoch, "B", "s", null);

        var result = AuditChainVerifier.Verify(new[] { e1, e2 });

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("chain");
    }

    [Fact]
    public void Verifier_flags_content_tampering()
    {
        var e1 = AuditEvent.CreateNext(AuditEvent.Genesis, DateTimeOffset.UnixEpoch, "A", "s", "orig");
        // Simulate a row edited directly in the DB: corrupt the content without recomputing the hash.
        typeof(AuditEvent).GetProperty(nameof(AuditEvent.DataJson))!
            .GetSetMethod(nonPublic: true)!.Invoke(e1, new object?[] { "tampered" });

        var result = AuditChainVerifier.Verify(new[] { e1 });

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("tampered");
    }

    [Fact]
    public void Empty_chain_is_valid()
    {
        AuditChainVerifier.Verify(Array.Empty<AuditEvent>()).IsValid.Should().BeTrue();
    }

    // ADR-0026 (PR1 step 5) — the enriched (v2) emit path.
    [Fact]
    public async Task Enriched_emit_writes_a_v2_row_draining_before_after_and_actor()
    {
        await using var db = NewDb(nameof(Enriched_emit_writes_a_v2_row_draining_before_after_and_actor));
        var pid = Guid.NewGuid().ToString();
        var buffer = new AuditChangeBuffer();
        buffer.Add(new AuditChange("Topic", pid, "{\"Status\":\"Accepted\"}", "{\"Status\":\"Prepared\"}"));
        var sink = Sink(db, buffer, new FakeUser { UserId = "kc-9", Roles = new[] { "Chairman" } });

        await sink.EmitEnrichedAsync("Topics.Prepared", "Topic", pid);

        await using var reader = NewDb(nameof(Enriched_emit_writes_a_v2_row_draining_before_after_and_actor));
        var row = await reader.AuditEvents.SingleAsync();
        row.HashVersion.Should().Be(2);
        row.Action.Should().Be("Topics.Prepared");
        row.SubjectType.Should().Be("Topic");
        row.SubjectId.Should().Be(pid);
        row.ActorUserId.Should().Be("kc-9");
        row.ActorRole.Should().Be("Chairman");
        row.Outcome.Should().Be(AuditOutcome.Success);
        row.BeforeJson.Should().Contain("Accepted");
        row.AfterJson.Should().Contain("Prepared");
        row.Hash.Should().Be(row.Recompute(), "the enriched row is internally consistent (v2 hash)");
    }

    [Fact]
    public async Task Enriched_emit_for_a_denial_has_no_before_after()
    {
        await using var db = NewDb(nameof(Enriched_emit_for_a_denial_has_no_before_after));
        var sink = Sink(db); // empty buffer — a denial/system event mutated no entity

        await sink.EmitEnrichedAsync("Decisions.BallotDenied", "Vote", Guid.NewGuid().ToString(), AuditOutcome.Denied);

        var row = await db.AuditEvents.SingleAsync();
        row.Outcome.Should().Be(AuditOutcome.Denied);
        row.BeforeJson.Should().BeNull();
        row.AfterJson.Should().BeNull();
    }

    // F-04 (D-23) — the tip-race retry branch (AppendAsync catch + IsTipRace) is otherwise exercised only by the
    // real-SQL concurrency integration tests, whose coverage depends on the OS scheduler actually interleaving
    // appends — the source of the flaky >=95% coverage gate on this file. This drives the branch deterministically:
    // the first SaveChangesAsync throws a fabricated audit tip-race, the second succeeds, proving detach+retry
    // recovers without forking or duplicating the chain.
    [Fact]
    public async Task Append_retries_on_a_tip_race_and_records_exactly_one_row()
    {
        var interceptor = new ThrowOnceOnSaveInterceptor(new DbUpdateException("audit tip-race",
            MakeTipRaceSqlException(2601, "Violation of UNIQUE KEY constraint 'IX_AuditEvents_PreviousHash'.")));
        await using var db = new AuditDbContext(new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(nameof(Append_retries_on_a_tip_race_and_records_exactly_one_row))
            .AddInterceptors(interceptor).Options);
        var sink = Sink(db);

        await sink.EmitAsync("Test.Raced", "kc-1", new { X = 1 });

        interceptor.Calls.Should().Be(2, "the first attempt lost the tip-race and the sink retried");
        var rows = await db.AuditEvents.ToListAsync();
        rows.Should().ContainSingle("the retry re-inserted rather than forking or duplicating the chain");
    }

    // Fails the first SaveChanges with a tip-race DbUpdateException, then delegates — drives the retry branch
    // without a real SQL Server (AuditDbContext is sealed, so intercept rather than subclass).
    private sealed class ThrowOnceOnSaveInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        private readonly Exception _first;
        public int Calls { get; private set; }

        public ThrowOnceOnSaveInterceptor(Exception first) => _first = first;

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken ct = default)
        {
            Calls++;
            if (Calls == 1)
                throw _first;
            return base.SavingChangesAsync(eventData, result, ct);
        }
    }

    // Microsoft.Data.SqlClient 5.1.x exposes no public SqlException/SqlError/SqlErrorCollection constructor, so a
    // tip-race SqlException (Number 2601/2627, message naming an audit-chain unique index) is fabricated via the
    // internal members. Test-only, never production.
    private static SqlException MakeTipRaceSqlException(int number, string message)
    {
        var errorCtor = typeof(SqlError).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance, binder: null,
            new[] { typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int), typeof(Exception) },
            modifiers: null) ?? throw new InvalidOperationException("SqlError ctor shape changed.");
        var error = (SqlError)errorCtor.Invoke(new object?[] { number, (byte)0, (byte)0, "server", message, "proc", 0, null });

        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(collection, new object[] { error });

        var createException = typeof(SqlException).GetMethod("CreateException",
            BindingFlags.NonPublic | BindingFlags.Static, binder: null,
            new[] { typeof(SqlErrorCollection), typeof(string) }, modifiers: null)
            ?? throw new InvalidOperationException("SqlException.CreateException shape changed.");
        return (SqlException)createException.Invoke(null, new object[] { collection, "16.0.0" })!;
    }
}
