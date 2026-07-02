using Acmp.Modules.Risks.Application.Abstractions;
using Acmp.Modules.Risks.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Risks.Infrastructure.Persistence;

// Maps ONLY the risks schema (docs/34 §12: no cross-module tables). References to other modules (the
// subject artifact = its PublicId + snapshot key, a mitigation's linked action id) are by value, never by
// FK navigation (ADR-0001). Mitigations are an owned collection of Risk (risk_mitigations table).
public sealed class RisksDbContext : ModuleDbContext, IRisksDbContext
{
    public const string Schema = "risks";

    public RisksDbContext(DbContextOptions<RisksDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options, clock, currentUser)
    {
    }

    public DbSet<Risk> Risks => Set<Risk>();
    internal DbSet<RiskKeyCounter> KeyCounters => Set<RiskKeyCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RisksDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
