using Acmp.Modules.Governance.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Infrastructure.Persistence;

// Allocates the next ADR-YYYY-### key from a per-prefix, per-year counter row (README §F). The in-app ADR
// key is distinct from the planning package's ADR-#### files.
// ponytail: the unique index on adrs.Key is the collision backstop; a counter row committed here before a
// later handler SaveChanges fails can leave an ordinal gap — harmless and accepted at ≤20 users.
public sealed class AdrKeyGenerator : IAdrKeyGenerator
{
    private readonly GovernanceDbContext _db;

    public AdrKeyGenerator(GovernanceDbContext db) => _db = db;

    public async Task<string> NextAdrKeyAsync(int year, CancellationToken ct = default)
    {
        const string prefix = "ADR";
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
