using Acmp.Modules.Knowledge.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Search;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Knowledge.Infrastructure.Search;

// ISearchProvider for wiki Documents (P15f, FR-118/143/144). Searches the Document's bilingual Title/Body over
// the Knowledge schema only (ADR-0001). FREETEXT (Arabic 1025 on *_ar, English 1033 on *_en) OR the LIKE
// booster on SQL Server; LIKE only on InMemory.
public sealed class DocumentSearchProvider : ISearchProvider
{
    private readonly KnowledgeDbContext _db;

    public DocumentSearchProvider(KnowledgeDbContext db) => _db = db;

    public string ArtifactType => "Documents";

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int take, CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0)
            return Array.Empty<SearchHit>();

        var src = _db.Documents.AsNoTracking();
        var matched = _db.Database.IsSqlServer()
            ? src.Where(d =>
                EF.Functions.FreeText(d.Title.Ar, q, 1025) || EF.Functions.FreeText(d.Body.Ar, q, 1025) ||
                EF.Functions.FreeText(d.Title.En, q, 1033) || EF.Functions.FreeText(d.Body.En, q, 1033) ||
                d.Title.Ar.Contains(q) || d.Body.Ar.Contains(q) ||
                d.Title.En.Contains(q) || d.Body.En.Contains(q))
            : src.Where(d =>
                d.Title.Ar.Contains(q) || d.Body.Ar.Contains(q) ||
                d.Title.En.Contains(q) || d.Body.En.Contains(q));

        var rows = await matched
            .OrderByDescending(d => d.Key)
            .Take(take)
            .Select(d => new { d.PublicId, d.Key, d.Title, d.Body, d.Status })
            .ToListAsync(ct);

        return rows
            .Select(r => new SearchHit(
                ArtifactType, r.PublicId, r.Key, r.Title,
                SearchExcerpt.Around($"{r.Body.Ar} {r.Body.En}", q),
                r.Status.ToString(), $"/knowledge/{r.Key}"))
            .ToList();
    }
}
