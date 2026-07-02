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

    private static AuditDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task First_emit_chains_off_genesis()
    {
        await using var db = NewDb(nameof(First_emit_chains_off_genesis));
        var sink = new SqlAuditSink(db, new FixedClock(), NullLogger<SqlAuditSink>.Instance);

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
        var sink = new SqlAuditSink(db, new FixedClock(), NullLogger<SqlAuditSink>.Instance);

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
}
