using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Shared.Contracts.Search;

// Global-search cross-module read seam (ADR-0001, P15f). FR-143/118: one search fans out across Topics,
// Decisions, ADRs, MoMs and wiki Documents WITHOUT any module reading another's tables. Each source module
// implements ISearchProvider in its own Infrastructure over its own FTS-indexed columns (the P10f
// ITraceabilityReader / P11e IDecisionReader precedent); the Acmp.Api SearchEndpoints host injects the whole
// IEnumerable<ISearchProvider> and groups the hits by ArtifactType. No cross-schema query, no single shared
// index — CONTAINS/FREETEXT is per-table anyway, and AC-060 wants results grouped by type, which IS the
// fan-out shape. If search ever escalates past SQL FTS (OQ-034), THIS interface is the swap point — so there
// is deliberately no ISearchIndex/engine abstraction underneath it (that would be a one-impl interface).
public interface ISearchProvider
{
    // Stable artifact-type key used to group results and label the group in the UI (e.g. "Topics").
    string ArtifactType { get; }

    // Runs the module's own full-text query for `query` and returns up to `take` lean hits. Never mutates.
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int take, CancellationToken ct = default);
}

// One result row (FR-144): the artifact's type, ids, bilingual title, a matched excerpt, its status name, and
// a deep link the UI navigates to. Bilingual text travels as the shared LocalizedString; module enums never
// leak — Status is its string name.
public sealed record SearchHit(
    string Type,
    Guid Id,
    string Key,
    LocalizedString Title,
    string Excerpt,
    string Status,
    string DeepLink);
