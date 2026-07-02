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
        // A hash can chain off any given parent at most once → the chain cannot fork, and there is one genesis.
        e.HasIndex(x => x.PreviousHash).IsUnique();
        e.HasIndex(x => x.Hash).IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
