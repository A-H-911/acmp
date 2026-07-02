namespace Acmp.Shared.Infrastructure.Audit;

// BL-066 (ADR-0009) — verifies the tamper-evidence property of the audit chain: every row's stored Hash
// must equal a re-computation from its own fields (no content edit), and each row's PreviousHash must equal
// the prior row's Hash (no deletion/insertion/reorder). The first row must chain off Genesis. Any break is
// reported as the Sequence at which the chain first fails.
public static class AuditChainVerifier
{
    public readonly record struct Result(bool IsValid, long? BrokenAtSequence, string? Reason)
    {
        public static readonly Result Ok = new(true, null, null);
    }

    // events MUST be ordered by Sequence ascending.
    public static Result Verify(IReadOnlyList<AuditEvent> events)
    {
        var expectedPrev = AuditEvent.Genesis;
        foreach (var e in events)
        {
            if (e.Hash != e.Recompute())
                return new Result(false, e.Sequence, "content tampered (stored hash != recomputed)");
            if (e.PreviousHash != expectedPrev)
                return new Result(false, e.Sequence, "chain broken (previous hash mismatch)");
            expectedPrev = e.Hash;
        }
        return Result.Ok;
    }
}
