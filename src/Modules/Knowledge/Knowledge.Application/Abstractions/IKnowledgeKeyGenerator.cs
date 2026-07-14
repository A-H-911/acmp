namespace Acmp.Modules.Knowledge.Application.Abstractions;

// Allocates the next human-readable knowledge key from a per-prefix, per-year counter: DOC-YYYY-### for wiki
// documents, TPL-YYYY-### for templates. The EF-backed implementation lives in Infrastructure (mirrors
// ResearchKeyGenerator); both prefixes share the one counter table (no schema change per prefix).
public interface IKnowledgeKeyGenerator
{
    Task<string> NextDocumentKeyAsync(int year, CancellationToken ct = default);
    Task<string> NextTemplateKeyAsync(int year, CancellationToken ct = default);
}
