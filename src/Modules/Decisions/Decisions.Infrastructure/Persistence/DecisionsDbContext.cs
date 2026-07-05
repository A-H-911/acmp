using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Infrastructure.Persistence;

// Maps ONLY the decisions schema (docs/domain/repository-structure.md §12: no cross-module tables). References to other modules
// (topic, meeting, vote, linked action = their PublicId) are by value, never by FK navigation (ADR-0001).
public sealed class DecisionsDbContext : ModuleDbContext, IDecisionsDbContext
{
    public const string Schema = "decisions";

    public DecisionsDbContext(DbContextOptions<DecisionsDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options, clock, currentUser)
    {
    }

    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<Vote> Votes => Set<Vote>();
    internal DbSet<DecisionKeyCounter> KeyCounters => Set<DecisionKeyCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DecisionsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
