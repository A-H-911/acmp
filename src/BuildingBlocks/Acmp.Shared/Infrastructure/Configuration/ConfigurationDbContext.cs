using Microsoft.EntityFrameworkCore;

namespace Acmp.Shared.Infrastructure.Configuration;

/*
 * WBS-24.5 (DEC-080 / SC-035) — the externalized configuration store's own DbContext (schema "config"),
 * mirroring AuditDbContext next door. It lives in BuildingBlocks rather than in a module because the
 * table is cross-cutting by definition, and ADR-0001 forbids a module reading another module's tables:
 * putting it in any one module would either violate that boundary or duplicate the store per module.
 * Audit is the established precedent for exactly this shape.
 *
 * It does NOT derive from ModuleDbContext. A configuration row is not an AuditableEntity and must not be
 * audit-STAMPED as one; its changes are audited explicitly by the command that writes them, because
 * SEC-077 classifies a retention/immutability config change as a privileged action and the audit row has
 * to name the actor and the before/after, which a stamp cannot.
 */
public sealed class ConfigurationDbContext : DbContext
{
    public const string Schema = "config";

    public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options) : base(options) { }

    public DbSet<ConfigurationSetting> Settings => Set<ConfigurationSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var e = modelBuilder.Entity<ConfigurationSetting>();
        // SEC-103 names the TABLE `Configuration`; the C# type avoids that name deliberately — see
        // ConfigurationSetting's header for why.
        e.ToTable("Configuration");
        e.HasKey(x => x.Id);
        e.Property(x => x.Key).IsRequired().HasMaxLength(200);
        e.Property(x => x.ValueJson).IsRequired();
        e.Property(x => x.Scope).IsRequired().HasMaxLength(100);
        // SEC-103 specifies Key as UNIQUE. It is what makes "the value of this setting" a well-formed
        // question: without it a second row for the same key is invisible and a reader gets whichever
        // the query happens to return first.
        e.HasIndex(x => x.Key).IsUnique();
        // Scope is the display and authorization grouping, so it is queried on every read of a section.
        e.HasIndex(x => x.Scope);
    }
}
