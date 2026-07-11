using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Acmp.Application.Tests.Audit;

// BL-066 (ADR-0009) — the durable, immutable, hash-chained AuditEvent store behind IAuditSink.
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
}
