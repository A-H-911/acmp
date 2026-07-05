using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Infrastructure.Persistence;

// Maps ONLY the topics schema (docs/domain/repository-structure.md §12: no cross-module tables). References to other modules
// (Owner = CommitteeMember.PublicId) are by value, never by FK navigation (ADR-0001).
public sealed class TopicsDbContext : ModuleDbContext, ITopicsDbContext
{
    public const string Schema = "topics";

    public TopicsDbContext(DbContextOptions<TopicsDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options, clock, currentUser)
    {
    }

    public DbSet<Topic> Topics => Set<Topic>();
    internal DbSet<TopicKeyCounter> KeyCounters => Set<TopicKeyCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TopicsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
