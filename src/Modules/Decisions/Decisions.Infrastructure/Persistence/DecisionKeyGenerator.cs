using Acmp.Modules.Decisions.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Infrastructure.Persistence;

// Allocates the next DECN-/VOTE-YYYY-### key from a per-prefix, per-year counter row (README §F).
// ponytail: the unique index on the target table's Key is the collision backstop; a counter row committed
// here before a later handler SaveChanges fails can leave an ordinal gap — harmless and accepted at ≤20 users.
public sealed class DecisionKeyGenerator : IDecisionKeyGenerator
{
    private readonly DecisionsDbContext _db;

    public DecisionKeyGenerator(DecisionsDbContext db) => _db = db;

    public Task<string> NextDecisionKeyAsync(int year, CancellationToken ct = default) => NextAsync("DECN", year, ct);

    public Task<string> NextVoteKeyAsync(int year, CancellationToken ct = default) => NextAsync("VOTE", year, ct);

    private async Task<string> NextAsync(string prefix, int year, CancellationToken ct)
    {
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
