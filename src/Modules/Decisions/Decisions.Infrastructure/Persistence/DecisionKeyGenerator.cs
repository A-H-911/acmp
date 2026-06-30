using Acmp.Modules.Decisions.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Infrastructure.Persistence;

// Allocates the next DECN-YYYY-### key from a per-prefix, per-year counter row (README §F).
public sealed class DecisionKeyGenerator : IDecisionKeyGenerator
{
    private readonly DecisionsDbContext _db;

    public DecisionKeyGenerator(DecisionsDbContext db) => _db = db;

    public async Task<string> NextDecisionKeyAsync(int year, CancellationToken ct = default)
    {
        const string prefix = "DECN";
        var counter = await _db.KeyCounters.FirstOrDefaultAsync(c => c.Prefix == prefix && c.Year == year, ct);
        if (counter is null)
        {
            counter = new DecisionKeyCounter { Prefix = prefix, Year = year, Next = 1 };
            _db.KeyCounters.Add(counter);
        }

        var ordinal = counter.Next;
        counter.Next++;
        await _db.SaveChangesAsync(ct);

        return $"{prefix}-{year}-{ordinal:D3}";
    }
}
