namespace Acmp.Modules.Dependencies.Application.Abstractions;

// Allocates human-readable, year-scoped dependency keys (DPN-YYYY-###, README §F). Implemented in
// Infrastructure against per-year counter rows so numbering is gap-free and concurrency-safe.
public interface IDependencyKeyGenerator
{
    Task<string> NextDependencyKeyAsync(int year, CancellationToken ct = default);
}
