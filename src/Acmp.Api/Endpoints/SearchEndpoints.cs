using Acmp.Shared.Contracts.Search;

namespace Acmp.Api.Endpoints;

// AC-060/061 (FR-143/144/145/118) — the global search bar's backend. Fans out over every module's
// ISearchProvider (Topics, Decisions, ADRs, MoMs, wiki Documents), each querying only its own FTS-indexed
// tables (ADR-0001), and returns the hits grouped by artifact type. Any authenticated role may search
// (US-078). Read-only by construction: providers never mutate.
//
// DEVIATION from a MediatR query handler (the AuditEndpoints precedent): a pure cross-module read gains
// nothing from the validation/authorization/transaction pipeline, so it injects IEnumerable<ISearchProvider>
// straight into the endpoint lambda.
public static class SearchEndpoints
{
    // Per-type result cap. Grouped search shows the top few per artifact type, not an unbounded dump.
    private const int DefaultTakePerType = 5;
    private const int MaxTakePerType = 25;

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/search", async (
                IEnumerable<ISearchProvider> providers, CancellationToken ct,
                string? q = null, int take = DefaultTakePerType) =>
            {
                var query = (q ?? string.Empty).Trim();
                if (query.Length == 0)
                    return Results.Ok(Array.Empty<SearchGroupDto>());

                var perType = take <= 0 ? DefaultTakePerType : Math.Min(take, MaxTakePerType);

                // Sequential fan-out: all module DbContexts share ONE per-scope DbConnection (ADR-0026), which
                // cannot run concurrent commands. ponytail: sequential is correct + trivially fast at <=20
                // users / 5 small FTS queries within the 3s AC-060 budget; revisit only if providers grow or
                // the shared-connection wiring changes.
                var groups = new List<SearchGroupDto>();
                foreach (var provider in providers)
                {
                    var hits = await provider.SearchAsync(query, perType, ct);
                    if (hits.Count > 0)
                        groups.Add(new SearchGroupDto(provider.ArtifactType, hits));
                }

                return Results.Ok(groups);
            })
            .WithTags("Search")
            .RequireAuthorization()
            .RequireRateLimiting(Acmp.Api.Infrastructure.RateLimitPolicies.Search);

        return app;
    }

    public sealed record SearchGroupDto(string Type, IReadOnlyList<SearchHit> Items);
}
