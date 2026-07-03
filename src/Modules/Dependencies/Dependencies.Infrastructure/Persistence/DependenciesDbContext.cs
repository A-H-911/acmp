using Acmp.Modules.Dependencies.Application.Abstractions;
using Acmp.Modules.Dependencies.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Dependencies.Infrastructure.Persistence;

// Maps ONLY the dependencies schema (docs/34 §12: no cross-module tables). Edge endpoints are value
// snapshots (type + PublicId + key + title), never FK navigations into other modules (ADR-0001, ADR-0019).
public sealed class DependenciesDbContext : ModuleDbContext, IDependenciesDbContext
{
    public const string Schema = "dependencies";

    public DependenciesDbContext(DbContextOptions<DependenciesDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options, clock, currentUser)
    {
    }

    public DbSet<Dependency> Dependencies => Set<Dependency>();
    internal DbSet<DependencyKeyCounter> KeyCounters => Set<DependencyKeyCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DependenciesDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
