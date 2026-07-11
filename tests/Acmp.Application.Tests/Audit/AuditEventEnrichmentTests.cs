using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Xunit;

namespace Acmp.Application.Tests.Audit;

// ADR-0026 — the enriched (v2) AuditEvent row + per-row hash versioning. Proves the chain stays verifiable
// across a v1 -> v2 boundary and that the v2 canonical payload is deterministic and tamper-evident.
public class AuditEventEnrichmentTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 11, 9, 0, 0, TimeSpan.Zero);

    private static AuditEvent V2(string prev, string? before = "{\"S\":\"A\"}", string? after = "{\"S\":\"B\"}") =>
        AuditEvent.CreateEnriched(prev, At, "Topics.Accepted", "Topic", "TOP-2026-001",
            "kc-42", "Chairman", "Success", before, after, "trace-abc");

    [Fact]
    public void Enriched_row_captures_the_full_field_set_and_is_v2()
    {
        var e = V2(AuditEvent.Genesis);

        e.HashVersion.Should().Be(2);
        e.Action.Should().Be("Topics.Accepted");
        e.SubjectType.Should().Be("Topic");
        e.SubjectId.Should().Be("TOP-2026-001");
        e.ActorUserId.Should().Be("kc-42");
        e.ActorRole.Should().Be("Chairman");
        e.Outcome.Should().Be("Success");
        e.BeforeJson.Should().Be("{\"S\":\"A\"}");
        e.AfterJson.Should().Be("{\"S\":\"B\"}");
        e.CorrelationId.Should().Be("trace-abc");
        // Legacy columns mirror action/actor so pre-enrichment readers still work.
        e.EventType.Should().Be("Topics.Accepted");
        e.Subject.Should().Be("kc-42");
        // The stored hash recomputes (the row is internally consistent).
        e.Hash.Should().Be(e.Recompute()).And.NotBeNullOrEmpty();
    }

    [Fact]
    public void Chain_verifies_across_a_v1_to_v2_boundary()
    {
        // A legacy v1 row, then an enriched v2 row chained off it — the verifier must accept both.
        var v1 = AuditEvent.CreateNext(AuditEvent.Genesis, At, "Legacy.Event", "kc-1", "{\"x\":1}");
        var v2 = V2(v1.Hash);

        v2.PreviousHash.Should().Be(v1.Hash);
        var result = AuditChainVerifier.Verify(new[] { v1, v2 });
        result.IsValid.Should().BeTrue();
        result.BrokenAtSequence.Should().BeNull();
    }

    [Fact]
    public void V2_hash_is_deterministic_and_distinguishes_null_from_empty()
    {
        var a = V2(AuditEvent.Genesis);
        var b = V2(AuditEvent.Genesis);
        a.Hash.Should().Be(b.Hash, "identical inputs must produce an identical hash");

        var withNullBefore = V2(AuditEvent.Genesis, before: null);
        var withEmptyBefore = V2(AuditEvent.Genesis, before: "");
        withNullBefore.Hash.Should().NotBe(withEmptyBefore.Hash,
            "the null-flag prefix must make a null field hash differently from an empty string");
    }

    [Fact]
    public void Verifier_flags_tampering_of_an_enriched_field()
    {
        var e = V2(AuditEvent.Genesis);
        // Simulate a direct-DB edit of the after-state without recomputing the hash.
        typeof(AuditEvent).GetProperty(nameof(AuditEvent.AfterJson))!
            .GetSetMethod(nonPublic: true)!.Invoke(e, new object?[] { "{\"S\":\"TAMPERED\"}" });

        var result = AuditChainVerifier.Verify(new[] { e });
        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("tampered");
    }
}
