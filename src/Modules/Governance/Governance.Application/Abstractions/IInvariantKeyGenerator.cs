namespace Acmp.Modules.Governance.Application.Abstractions;

// Allocates human-readable, year-scoped in-app Architecture Invariant keys (AIV-YYYY-###, README §F).
// Implemented in Infrastructure against the same per-year counter rows as ADR keys (a different prefix), so
// numbering is gap-free and concurrency-safe.
public interface IInvariantKeyGenerator
{
    Task<string> NextInvariantKeyAsync(int year, CancellationToken ct = default);
}
