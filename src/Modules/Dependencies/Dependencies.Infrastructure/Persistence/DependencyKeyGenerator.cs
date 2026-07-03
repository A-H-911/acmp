using Acmp.Modules.Dependencies.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Dependencies.Infrastructure.Persistence;

// Allocates the next DPN-YYYY-### key from a per-prefix, per-year counter row (README §F).
// ponytail: the unique index on dependencies.Key is the collision backstop; a counter row committed here
// before a later handler SaveChanges fails can leave an ordinal gap — harmless and accepted at ≤20 users.
public sealed class DependencyKeyGenerator : IDependencyKeyGenerator
{
    private readonly DependenciesDbContext _db;

    public DependencyKeyGenerator(DependenciesDbContext db) => _db = db;

    public async Task<string> NextDependencyKeyAsync(int year, CancellationToken ct = default)
    {
        const string prefix = "DPN";
        var counter = await _db.KeyCounters.FirstOrDefaultAsync(c => c.Prefix == prefix && c.Year == year, ct);
        if (counter is null)
        {
            counter = new DependencyKeyCounter { Prefix = prefix, Year = year, Next = 1 };
            _db.KeyCounters.Add(counter);
        }

        var ordinal = counter.Next;
        counter.Next++;
        await _db.SaveChangesAsync(ct);

        return $"{prefix}-{year}-{ordinal:D3}";
    }
}
