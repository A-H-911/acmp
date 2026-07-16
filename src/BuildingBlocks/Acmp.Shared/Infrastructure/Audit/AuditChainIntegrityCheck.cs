using Acmp.Shared.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Shared.Infrastructure.Audit;

// D-16 / C-INS-02 (ADR-0009/0030) — the audit store's own tamper check: re-verify the whole hash chain.
public sealed class AuditChainIntegrityCheck : IIntegrityCheck
{
    private readonly AuditDbContext _db;

    public AuditChainIntegrityCheck(AuditDbContext db) => _db = db;

    public string Name => "audit-chain";

    public async Task<IntegrityCheckResult> RunAsync(CancellationToken ct = default)
    {
        // ponytail: loads the whole chain in Sequence order — fine for years at <=20 users, low traffic. If the
        // audit table ever grows large, verify incrementally from the last-known-good checkpoint (upgrade path).
        var events = await _db.AuditEvents.OrderBy(e => e.Sequence).ToListAsync(ct);
        var result = AuditChainVerifier.Verify(events);
        return result.IsValid
            ? IntegrityCheckResult.Ok(Name, events.Count)
            : IntegrityCheckResult.Broken(Name, events.Count, $"sequence {result.BrokenAtSequence}: {result.Reason}");
    }
}
