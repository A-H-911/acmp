namespace Acmp.Modules.Risks.Application.Abstractions;

// Allocates human-readable, year-scoped risk keys (RSK-YYYY-###, README §F). Implemented in
// Infrastructure against per-year counter rows so numbering is gap-free and concurrency-safe.
public interface IRiskKeyGenerator
{
    Task<string> NextRiskKeyAsync(int year, CancellationToken ct = default);
}
