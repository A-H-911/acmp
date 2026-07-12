using Acmp.Modules.Research.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Research.Infrastructure.Persistence;

// Allocates the next RMS-YYYY-### key from a per-prefix, per-year counter row (mirrors AdrKeyGenerator).
// ponytail: the unique index on missions.Key is the collision backstop; a counter row committed here before a
// later handler SaveChanges fails can leave an ordinal gap — harmless and accepted at ≤20 users.
public sealed class ResearchKeyGenerator : IResearchKeyGenerator
{
    private readonly ResearchDbContext _db;

    public ResearchKeyGenerator(ResearchDbContext db) => _db = db;

    public async Task<string> NextResearchKeyAsync(int year, CancellationToken ct = default)
    {
        const string prefix = "RMS";
        var counter = await _db.KeyCounters.FirstOrDefaultAsync(c => c.Prefix == prefix && c.Year == year, ct);
        if (counter is null)
        {
            counter = new ResearchKeyCounter { Prefix = prefix, Year = year, Next = 1 };
            _db.KeyCounters.Add(counter);
        }

        var ordinal = counter.Next;
        counter.Next++;
        await _db.SaveChangesAsync(ct);

        return $"{prefix}-{year}-{ordinal:D3}";
    }
}
