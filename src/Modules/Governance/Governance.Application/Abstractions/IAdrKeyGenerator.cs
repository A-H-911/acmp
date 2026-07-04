namespace Acmp.Modules.Governance.Application.Abstractions;

// Allocates human-readable, year-scoped in-app ADR keys (ADR-YYYY-###, README §F — distinct from the
// planning package's ADR-#### files). Implemented in Infrastructure against per-year counter rows so
// numbering is gap-free and concurrency-safe.
public interface IAdrKeyGenerator
{
    Task<string> NextAdrKeyAsync(int year, CancellationToken ct = default);
}
