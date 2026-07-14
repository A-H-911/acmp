namespace Acmp.Modules.Knowledge.Application.Abstractions;

// Allocates the next human-readable document key (DOC-YYYY-###) from a per-prefix, per-year counter. The
// EF-backed implementation lives in Infrastructure (mirrors ResearchKeyGenerator). Template keys (TPL-) are a
// later P15d-2 slice — they will add a NextTemplateKeyAsync onto the same per-prefix-per-year counter table.
public interface IKnowledgeKeyGenerator
{
    Task<string> NextDocumentKeyAsync(int year, CancellationToken ct = default);
}
