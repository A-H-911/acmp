using Acmp.Modules.Knowledge.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Knowledge.Infrastructure.Persistence;

// Allocates the next DOC-YYYY-### key from a per-prefix, per-year counter row (mirrors ResearchKeyGenerator).
// The per-prefix design leaves room for the P15d-2 TPL- key without a schema change. ponytail: the unique index
// on documents.Key is the collision backstop; a counter row committed here before a later handler SaveChanges
// fails can leave an ordinal gap — harmless and accepted at ≤20 users.
public sealed class KnowledgeKeyGenerator : IKnowledgeKeyGenerator
{
    private readonly KnowledgeDbContext _db;

    public KnowledgeKeyGenerator(KnowledgeDbContext db) => _db = db;

    public Task<string> NextDocumentKeyAsync(int year, CancellationToken ct = default) => NextKeyAsync("DOC", year, ct);

    private async Task<string> NextKeyAsync(string prefix, int year, CancellationToken ct)
    {
        var counter = await _db.KeyCounters.FirstOrDefaultAsync(c => c.Prefix == prefix && c.Year == year, ct);
        if (counter is null)
        {
            counter = new KnowledgeKeyCounter { Prefix = prefix, Year = year, Next = 1 };
            _db.KeyCounters.Add(counter);
        }

        var ordinal = counter.Next;
        counter.Next++;
        await _db.SaveChangesAsync(ct);

        return $"{prefix}-{year}-{ordinal:D3}";
    }
}
