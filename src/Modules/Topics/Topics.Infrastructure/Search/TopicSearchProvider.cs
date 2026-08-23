using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Search;
using Acmp.Shared.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Infrastructure.Search;

// ISearchProvider for Topics (P15f, FR-143/144). Topic Title/Description are MONOLINGUAL single columns (not
// bilingual value objects), so the full-text index is single-column and the hit title mirrors the one string
// into both LocalizedString slots. Engine split: FREETEXT (Arabic 1025 + English 1033 query breakers over the
// same mixed-language column — belt-and-suspenders) OR the LIKE booster on SQL Server; LIKE only on InMemory.
public sealed class TopicSearchProvider : ISearchProvider
{
    private readonly TopicsDbContext _db;
    private readonly ITopicVisibility _visibility;

    // ⚠ ISearchProvider.SearchAsync carries NO principal parameter — the interface is deliberately
    // narrow ("THIS interface is the swap point"). The provider is registered AddScoped, so the caller
    // is resolved through the request-scoped ITopicVisibility rather than by widening the contract for
    // every other module's provider.
    public TopicSearchProvider(TopicsDbContext db, ITopicVisibility visibility)
    {
        _db = db;
        _visibility = visibility;
    }

    public string ArtifactType => "Topics";

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int take, CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0)
            return Array.Empty<SearchHit>();

        var src = _db.Topics.AsNoTracking();
        var matched = _db.Database.IsSqlServer()
            ? src.Where(t =>
                EF.Functions.FreeText(t.Title, q, 1025) || EF.Functions.FreeText(t.Description, q, 1025) ||
                EF.Functions.FreeText(t.Title, q, 1033) || EF.Functions.FreeText(t.Description, q, 1033) ||
                t.Title.Contains(q) || t.Description.Contains(q))
            : src.Where(t => t.Title.Contains(q) || t.Description.Contains(q));

        // C-AUTHZ-04 / FR-163: excluded from SEARCH RESULTS, which the control names explicitly —
        // a title and a description excerpt are exactly the content a Restricted topic withholds.
        // ⚠ COMPOSED BEFORE .Take. After it, the control would silently shrink pages instead of
        // filtering them: a search asking for 5 hits would return 3 with no explanation.
        matched = matched.VisibleTo(await _visibility.ResolveAsync(ct));

        var rows = await matched
            .OrderByDescending(t => t.Key)
            .Take(take)
            .Select(t => new { t.PublicId, t.Key, t.Title, t.Description, t.Status })
            .ToListAsync(ct);

        return rows
            .Select(r => new SearchHit(
                ArtifactType, r.PublicId, r.Key, new LocalizedString(r.Title, r.Title),
                SearchExcerpt.Around(r.Description, q),
                r.Status.ToString(), $"/topics/{r.Key}"))
            .ToList();
    }
}
