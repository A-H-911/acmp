using Acmp.Modules.Knowledge.Domain;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Knowledge.Application.Abstractions;

// The Application layer talks to persistence through this narrow port; the EF implementation
// (schema "knowledge") lives in Infrastructure and maps only its own tables (ADR-0001, docs/domain/repository-structure.md §12).
// DocumentVersions are an owned collection of Document — EF loads/saves them with the aggregate.
public interface IKnowledgeDbContext
{
    DbSet<Document> Documents { get; }
    DbSet<Template> Templates { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
