namespace Acmp.Modules.Actions.Application.Abstractions;

// Allocates human-readable, year-scoped action keys (ACT-YYYY-###, README §F). Implemented in
// Infrastructure against per-year counter rows so numbering is gap-free and concurrency-safe.
public interface IActionKeyGenerator
{
    Task<string> NextActionKeyAsync(int year, CancellationToken ct = default);
}
