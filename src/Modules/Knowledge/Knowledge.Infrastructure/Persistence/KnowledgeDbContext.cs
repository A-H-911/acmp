using Acmp.Modules.Knowledge.Application.Abstractions;
using Acmp.Modules.Knowledge.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Knowledge.Infrastructure.Persistence;

// Maps ONLY the knowledge schema (docs/domain/repository-structure.md §12: no cross-module tables). DocumentVersions are an
// owned collection of Document (knowledge_document_versions) — EF loads/saves them with the aggregate. Templates
// (P15d-2) are a flat sibling table in the same schema; both key off the shared knowledge_key_counters.
public sealed class KnowledgeDbContext : ModuleDbContext, IKnowledgeDbContext
{
    public const string Schema = "knowledge";

    public KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options, clock, currentUser)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Template> Templates => Set<Template>();
    internal DbSet<KnowledgeKeyCounter> KeyCounters => Set<KnowledgeKeyCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KnowledgeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
