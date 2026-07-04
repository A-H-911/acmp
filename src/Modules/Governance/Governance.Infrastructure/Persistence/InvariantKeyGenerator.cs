using Acmp.Modules.Governance.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Infrastructure.Persistence;

// Allocates the next AIV-YYYY-### key from the SAME per-prefix, per-year counter table as ADR keys (the
// (Prefix, Year) row is already prefix-generic; "AIV" just gets its own rows). README §F.
// ponytail: the unique index on invariants.Key is the collision backstop; a counter row committed here before
// a later handler SaveChanges fails can leave an ordinal gap — harmless and accepted at ≤20 users.
public sealed class InvariantKeyGenerator : IInvariantKeyGenerator
{
    private readonly GovernanceDbContext _db;

    public InvariantKeyGenerator(GovernanceDbContext db) => _db = db;

    public async Task<string> NextInvariantKeyAsync(int year, CancellationToken ct = default)
    {
        const string prefix = "AIV";
        var counter = await _db.KeyCounters.FirstOrDefaultAsync(c => c.Prefix == prefix && c.Year == year, ct);
        if (counter is null)
        {
            counter = new AdrKeyCounter { Prefix = prefix, Year = year, Next = 1 };
            _db.KeyCounters.Add(counter);
        }

        var ordinal = counter.Next;
        counter.Next++;
        await _db.SaveChangesAsync(ct);

        return $"{prefix}-{year}-{ordinal:D3}";
    }
}
