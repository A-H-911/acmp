using Acmp.Modules.Risks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Risks.Infrastructure.Persistence;

// Allocates the next RSK-YYYY-### key from a per-prefix, per-year counter row (README §F).
// ponytail: the unique index on risks.Key is the collision backstop; a counter row committed here
// before a later handler SaveChanges fails can leave an ordinal gap — harmless and accepted at ≤20 users.
public sealed class RiskKeyGenerator : IRiskKeyGenerator
{
    private readonly RisksDbContext _db;

    public RiskKeyGenerator(RisksDbContext db) => _db = db;

    public async Task<string> NextRiskKeyAsync(int year, CancellationToken ct = default)
    {
        const string prefix = "RSK";
        var counter = await _db.KeyCounters.FirstOrDefaultAsync(c => c.Prefix == prefix && c.Year == year, ct);
        if (counter is null)
        {
            counter = new RiskKeyCounter { Prefix = prefix, Year = year, Next = 1 };
            _db.KeyCounters.Add(counter);
        }

        var ordinal = counter.Next;
        counter.Next++;
        await _db.SaveChangesAsync(ct);

        return $"{prefix}-{year}-{ordinal:D3}";
    }
}
