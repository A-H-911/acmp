using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Search;
using Acmp.Shared.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Infrastructure.Search;

// ISearchProvider for Minutes of Meeting (P15f, FR-143/144). MoM has no title field — its content is the
// bilingual Summary — so the hit "title" is a short excerpt of the Summary and the excerpt is a match window
// of the same. Searches the Meetings schema only (ADR-0001). FREETEXT (Arabic 1025 + English 1033) OR the LIKE
// booster on SQL Server; LIKE only on InMemory.
public sealed class MinutesSearchProvider : ISearchProvider
{
    private const int TitleWindow = 80;

    private readonly MeetingsDbContext _db;

    public MinutesSearchProvider(MeetingsDbContext db) => _db = db;

    public string ArtifactType => "MoMs";

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int take, CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0)
            return Array.Empty<SearchHit>();

        var src = _db.Minutes.AsNoTracking();
        var matched = _db.Database.IsSqlServer()
            ? src.Where(m =>
                EF.Functions.FreeText(m.Summary.Ar, q, 1025) || EF.Functions.FreeText(m.Summary.En, q, 1033) ||
                m.Summary.Ar.Contains(q) || m.Summary.En.Contains(q))
            : src.Where(m => m.Summary.Ar.Contains(q) || m.Summary.En.Contains(q));

        var rows = await matched
            .OrderByDescending(m => m.Key)
            .Take(take)
            .Select(m => new { m.PublicId, m.Key, m.Summary, m.Status })
            .ToListAsync(ct);

        return rows
            .Select(r => new SearchHit(
                ArtifactType, r.PublicId, r.Key,
                new LocalizedString(
                    SearchExcerpt.Around(r.Summary.En, q, TitleWindow),
                    SearchExcerpt.Around(r.Summary.Ar, q, TitleWindow)),
                SearchExcerpt.Around($"{r.Summary.Ar} {r.Summary.En}", q),
                r.Status.ToString(), $"/minutes/{r.Key}"))
            .ToList();
    }
}
