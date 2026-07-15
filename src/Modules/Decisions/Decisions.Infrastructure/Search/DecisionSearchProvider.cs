using Acmp.Modules.Decisions.Domain;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Search;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Infrastructure.Search;

// ISearchProvider for Decisions (P15f, FR-143/144). Queries only the Decisions schema (ADR-0001) over the
// FTS-indexed bilingual columns. Engine split (OQ-034 spike): on SQL Server the predicate is FREETEXT (Arabic
// word-breaker LCID 1025 + English 1033, ranking + morphology) OR-ed with a LIKE booster that recovers the
// word-breaker's derivational misses (عمارة↔معماري); on any other provider (the InMemory API test suite,
// which cannot translate FREETEXT) it degrades to the LIKE path alone. EF.Functions/.Contains keep every
// clause parameterized — no string concatenation into SQL.
public sealed class DecisionSearchProvider : ISearchProvider
{
    private readonly DecisionsDbContext _db;

    public DecisionSearchProvider(DecisionsDbContext db) => _db = db;

    public string ArtifactType => "Decisions";

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int take, CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0)
            return Array.Empty<SearchHit>();

        var src = _db.Decisions.AsNoTracking();
        var matched = _db.Database.IsSqlServer()
            ? src.Where(d =>
                EF.Functions.FreeText(d.Title.Ar, q, 1025) || EF.Functions.FreeText(d.Statement.Ar, q, 1025) || EF.Functions.FreeText(d.Rationale.Ar, q, 1025) ||
                EF.Functions.FreeText(d.Title.En, q, 1033) || EF.Functions.FreeText(d.Statement.En, q, 1033) || EF.Functions.FreeText(d.Rationale.En, q, 1033) ||
                d.Title.Ar.Contains(q) || d.Statement.Ar.Contains(q) || d.Rationale.Ar.Contains(q) ||
                d.Title.En.Contains(q) || d.Statement.En.Contains(q) || d.Rationale.En.Contains(q))
            : src.Where(d =>
                d.Title.Ar.Contains(q) || d.Statement.Ar.Contains(q) || d.Rationale.Ar.Contains(q) ||
                d.Title.En.Contains(q) || d.Statement.En.Contains(q) || d.Rationale.En.Contains(q));

        var rows = await matched
            .OrderByDescending(d => d.IssuedAt)
            .Take(take)
            .Select(d => new { d.PublicId, d.Key, d.Title, d.Statement, d.Status })
            .ToListAsync(ct);

        return rows
            .Select(r => new SearchHit(
                ArtifactType, r.PublicId, r.Key, r.Title,
                SearchExcerpt.Around($"{r.Statement.Ar} {r.Statement.En}", q),
                r.Status.ToString(), $"/decisions/{r.Key}"))
            .ToList();
    }
}
