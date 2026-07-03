using Acmp.Modules.Dependencies.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Dependencies.Infrastructure.Persistence.Configurations;

// Dependency edge → the dependencies table. Both endpoints are stored as value snapshots (type + PublicId +
// key + title), no FK to any module (ADR-0001, ADR-0019). The (FromType, FromId) and (ToType, ToId) indexes
// back the two panel queries (outbound / inbound); the Status index backs the register. Status carries the
// soft-delete (Removed) — rows are never physically deleted. RowVersion is the optimistic-concurrency token
// (ADR-0018).
public sealed class DependencyConfiguration : IEntityTypeConfiguration<Dependency>
{
    public void Configure(EntityTypeBuilder<Dependency> b)
    {
        b.ToTable("dependencies");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (ADR-0018)

        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.FromType).HasConversion<int>().IsRequired();
        b.Property(x => x.FromId).IsRequired();
        b.Property(x => x.FromKey).IsRequired().HasMaxLength(32);
        b.Property(x => x.FromTitle).IsRequired().HasMaxLength(512);

        b.Property(x => x.ToType).HasConversion<int>().IsRequired();
        b.Property(x => x.ToId).IsRequired();
        b.Property(x => x.ToKey).IsRequired().HasMaxLength(32);
        b.Property(x => x.ToTitle).IsRequired().HasMaxLength(512);

        b.Property(x => x.Kind).HasConversion<int>().IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.Note).HasMaxLength(1000);

        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.Ignore(x => x.DomainEvents);

        // Register access path (filtered/sorted by status) + the two panel access paths (outbound / inbound).
        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.FromType, x.FromId });
        b.HasIndex(x => new { x.ToType, x.ToId });
    }
}

internal sealed class DependencyKeyCounterConfiguration : IEntityTypeConfiguration<DependencyKeyCounter>
{
    public void Configure(EntityTypeBuilder<DependencyKeyCounter> b)
    {
        b.ToTable("dependency_key_counters");
        b.HasKey(x => new { x.Prefix, x.Year });
        b.Property(x => x.Prefix).HasMaxLength(8);
        b.Property(x => x.Next).IsRequired();
    }
}
