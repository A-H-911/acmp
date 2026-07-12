namespace Acmp.Modules.Research.Application.Abstractions;

// Allocates the next human-readable mission key (RMS-YYYY-###) from a per-year counter. The EF-backed
// implementation lives in Infrastructure (mirrors AdrKeyGenerator).
public interface IResearchKeyGenerator
{
    Task<string> NextResearchKeyAsync(int year, CancellationToken ct = default);
}
