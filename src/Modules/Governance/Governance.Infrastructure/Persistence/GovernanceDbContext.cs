using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Infrastructure.Persistence;

// Maps ONLY the governance schema (docs/34 §12: no cross-module tables). References to other modules (an
// ADR's SourceDecisionId) are by value, never by FK navigation (ADR-0001). Considered options are an owned
// collection of Adr (adr_options table). The Invariant aggregate joins this context in the P11c slice.
public sealed class GovernanceDbContext : ModuleDbContext, IGovernanceDbContext
{
    public const string Schema = "governance";

    public GovernanceDbContext(DbContextOptions<GovernanceDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options, clock, currentUser)
    {
    }

    public DbSet<Adr> Adrs => Set<Adr>();
    internal DbSet<AdrKeyCounter> KeyCounters => Set<AdrKeyCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GovernanceDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
