using Acmp.Modules.Actions.Application.Abstractions;
using Acmp.Modules.Actions.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Actions.Infrastructure.Persistence;

// Maps ONLY the actions schema (docs/domain/repository-structure.md §12: no cross-module tables). References to other modules (the
// source artifact = its PublicId + snapshot key) are by value, never by FK navigation (ADR-0001).
public sealed class ActionsDbContext : ModuleDbContext, IActionsDbContext
{
    public const string Schema = "actions";

    public ActionsDbContext(DbContextOptions<ActionsDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options, clock, currentUser)
    {
    }

    public DbSet<ActionItem> Actions => Set<ActionItem>();
    internal DbSet<ActionKeyCounter> KeyCounters => Set<ActionKeyCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ActionsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
