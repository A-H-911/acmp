namespace Acmp.Modules.Decisions.Application.Abstractions;

// Allocates human-readable, year-scoped decision keys (DECN-YYYY-###, README §F). Implemented in
// Infrastructure against per-year counter rows so numbering is gap-free and concurrency-safe.
public interface IDecisionKeyGenerator
{
    Task<string> NextDecisionKeyAsync(int year, CancellationToken ct = default);
}
