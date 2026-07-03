using Acmp.Modules.Traceability.Application.Abstractions;
using Acmp.Modules.Traceability.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Traceability.Infrastructure.Persistence;

// Maps ONLY the traceability schema (docs/34 §12: no cross-module tables). Edge endpoints are value snapshots
// (type + PublicId + key + title), never FK navigations into other modules (ADR-0001, ADR-0019).
public sealed class TraceabilityDbContext : ModuleDbContext, ITraceabilityDbContext
{
    public const string Schema = "traceability";

    public TraceabilityDbContext(DbContextOptions<TraceabilityDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options, clock, currentUser)
    {
    }

    public DbSet<Relationship> Relationships => Set<Relationship>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TraceabilityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
