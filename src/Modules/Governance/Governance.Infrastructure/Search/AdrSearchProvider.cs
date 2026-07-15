using Acmp.Modules.Governance.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Search;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Infrastructure.Search;

// ISearchProvider for ADRs (P15f, FR-143/144). Searches the ADR's bilingual Title/Context/DecisionText over
// the Governance schema only (ADR-0001). FREETEXT (Arabic 1025 on *_ar, English 1033 on *_en) OR the LIKE
// booster on SQL Server; LIKE only on InMemory.
public sealed class AdrSearchProvider : ISearchProvider
{
    private readonly GovernanceDbContext _db;

    public AdrSearchProvider(GovernanceDbContext db) => _db = db;

    public string ArtifactType => "ADRs";

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int take, CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0)
            return Array.Empty<SearchHit>();

        var src = _db.Adrs.AsNoTracking();
        var matched = _db.Database.IsSqlServer()
            ? src.Where(a =>
                EF.Functions.FreeText(a.Title.Ar, q, 1025) || EF.Functions.FreeText(a.Context.Ar, q, 1025) || EF.Functions.FreeText(a.DecisionText.Ar, q, 1025) ||
                EF.Functions.FreeText(a.Title.En, q, 1033) || EF.Functions.FreeText(a.Context.En, q, 1033) || EF.Functions.FreeText(a.DecisionText.En, q, 1033) ||
                a.Title.Ar.Contains(q) || a.Context.Ar.Contains(q) || a.DecisionText.Ar.Contains(q) ||
                a.Title.En.Contains(q) || a.Context.En.Contains(q) || a.DecisionText.En.Contains(q))
            : src.Where(a =>
                a.Title.Ar.Contains(q) || a.Context.Ar.Contains(q) || a.DecisionText.Ar.Contains(q) ||
                a.Title.En.Contains(q) || a.Context.En.Contains(q) || a.DecisionText.En.Contains(q));

        var rows = await matched
            .OrderByDescending(a => a.Key)
            .Take(take)
            .Select(a => new { a.PublicId, a.Key, a.Title, a.Context, a.Status })
            .ToListAsync(ct);

        return rows
            .Select(r => new SearchHit(
                ArtifactType, r.PublicId, r.Key, r.Title,
                SearchExcerpt.Around($"{r.Context.Ar} {r.Context.En}", q),
                r.Status.ToString(), $"/adrs/{r.Key}"))
            .ToList();
    }
}
