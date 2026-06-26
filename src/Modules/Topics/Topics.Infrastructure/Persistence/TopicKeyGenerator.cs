using Acmp.Modules.Topics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Infrastructure.Persistence;

// Allocates the next TOP-YYYY-### key from a per-year counter row (README §F).
// ponytail: a get-increment-save on the counter row. Gap-free and fine for a single low-traffic
// committee; a unique index on Topic.Key fails loudly on the rare concurrent collision rather than
// silently duplicating. Upgrade to a DB sequence only if throughput ever demands it.
public sealed class TopicKeyGenerator : ITopicKeyGenerator
{
    private readonly TopicsDbContext _db;

    public TopicKeyGenerator(TopicsDbContext db) => _db = db;

    public async Task<string> NextAsync(int year, CancellationToken ct = default)
    {
        var counter = await _db.KeyCounters.FirstOrDefaultAsync(c => c.Year == year, ct);
        if (counter is null)
        {
            counter = new TopicKeyCounter { Year = year, Next = 1 };
            _db.KeyCounters.Add(counter);
        }

        var ordinal = counter.Next;
        counter.Next++;
        await _db.SaveChangesAsync(ct);

        return $"TOP-{year}-{ordinal:D3}";
    }
}
