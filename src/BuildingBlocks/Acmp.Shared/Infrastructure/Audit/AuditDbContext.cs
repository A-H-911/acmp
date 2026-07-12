using Microsoft.EntityFrameworkCore;

namespace Acmp.Shared.Infrastructure.Audit;

// BL-066 — the durable audit store's own DbContext (schema "audit"). It maps ONLY the AuditEvent table and
// does NOT derive from ModuleDbContext: the audit log is not itself an AuditableEntity and must not be
// audit-stamped. Append-only is enforced by the entity (no public setters, no delete path) plus the UNIQUE
// index on PreviousHash, which makes the hash-chain strictly linear (a row can be a parent at most once).
public sealed class AuditDbContext : DbContext
{
    public const string Schema = "audit";

    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var e = modelBuilder.Entity<AuditEvent>();
        e.ToTable("AuditEvents");
        e.HasKey(x => x.Sequence);
        e.Property(x => x.Sequence).ValueGeneratedOnAdd();
        e.Property(x => x.OccurredAt).IsRequired();
        e.Property(x => x.EventType).IsRequired().HasMaxLength(200);
        e.Property(x => x.Subject).HasMaxLength(200);
        e.Property(x => x.DataJson);
        e.Property(x => x.PreviousHash).IsRequired().HasMaxLength(64);
        e.Property(x => x.Hash).IsRequired().HasMaxLength(64);
        // Enriched (v2) columns — ADR-0026 / audit-and-records.md §1.1. All nullable so pre-enrichment
        // (v1) rows are unaffected; HashVersion defaults to 1 for those (migration) and is set to 2 in code.
        e.Property(x => x.HashVersion).IsRequired().HasDefaultValue(1);
        e.Property(x => x.Action).HasMaxLength(200);
        e.Property(x => x.SubjectType).HasMaxLength(100);
        e.Property(x => x.SubjectId).HasMaxLength(200);
        e.Property(x => x.ActorUserId).HasMaxLength(200);
        e.Property(x => x.ActorRole).HasMaxLength(100);
        e.Property(x => x.Outcome).HasMaxLength(20);
        e.Property(x => x.BeforeJson);
        e.Property(x => x.AfterJson);
        e.Property(x => x.CorrelationId).HasMaxLength(100);
        // A hash can chain off any given parent at most once → the chain cannot fork, and there is one genesis.
        e.HasIndex(x => x.PreviousHash).IsUnique();
        e.HasIndex(x => x.Hash).IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
