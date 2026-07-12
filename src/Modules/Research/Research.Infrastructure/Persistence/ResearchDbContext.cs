using Acmp.Modules.Research.Application.Abstractions;
using Acmp.Modules.Research.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Research.Infrastructure.Persistence;

// Maps ONLY the research schema (docs/domain/repository-structure.md §12: no cross-module tables). References to other modules (a
// mission's SourceTopicId, a recommendation's LinkedTopicId) are by value, never by FK navigation (ADR-0001).
// Findings + Recommendations are owned collections of ResearchMission (research_findings / research_recommendations).
public sealed class ResearchDbContext : ModuleDbContext, IResearchDbContext
{
    public const string Schema = "research";

    public ResearchDbContext(DbContextOptions<ResearchDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options, clock, currentUser)
    {
    }

    public DbSet<ResearchMission> Missions => Set<ResearchMission>();
    internal DbSet<ResearchKeyCounter> KeyCounters => Set<ResearchKeyCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ResearchDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
