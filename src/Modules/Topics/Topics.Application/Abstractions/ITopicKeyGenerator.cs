namespace Acmp.Modules.Topics.Application.Abstractions;

// Allocates the next human-readable, year-scoped topic key (TOP-YYYY-###, README §F). Implemented in
// Infrastructure against a SQL sequence/table so numbering is gap-free and concurrency-safe.
public interface ITopicKeyGenerator
{
    Task<string> NextAsync(int year, CancellationToken ct = default);
}
