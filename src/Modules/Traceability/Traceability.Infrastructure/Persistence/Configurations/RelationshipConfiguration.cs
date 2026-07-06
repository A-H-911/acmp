using Acmp.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Traceability.Infrastructure.Persistence.Configurations;

// Relationship edge → the relationships table (docs/domain/search-and-traceability.md §2.1). Both endpoints are stored as value snapshots
// (type + PublicId + key + title), no FK to any module (ADR-0001, ADR-0019). The (SourceType, SourceId) and
// (TargetType, TargetId) filtered indexes back the two panel queries (outgoing / incoming) over active edges;
// the RelType index backs the AC-029 downstream lookup + future reporting. IsActive is a soft-delete flag —
// rows are never physically deleted (docs/domain/search-and-traceability.md §5, ADR-0009).
public sealed class RelationshipConfiguration : IEntityTypeConfiguration<Relationship>
{
    public void Configure(EntityTypeBuilder<Relationship> b)
    {
        b.ToTable("relationships");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);

        b.Property(x => x.SourceType).HasConversion<int>().IsRequired();
        b.Property(x => x.SourceId).IsRequired();
        b.Property(x => x.SourceKey).IsRequired().HasMaxLength(32);
        b.Property(x => x.SourceTitle).IsRequired().HasMaxLength(512);

        b.Property(x => x.TargetType).HasConversion<int>().IsRequired();
        b.Property(x => x.TargetId).IsRequired();
        b.Property(x => x.TargetKey).IsRequired().HasMaxLength(32);
        b.Property(x => x.TargetTitle).IsRequired().HasMaxLength(512);

        b.Property(x => x.RelType).HasConversion<int>().IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.DeactivatedByUserId).HasMaxLength(128);

        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.Ignore(x => x.DomainEvents);

        // Panel access paths (docs/domain/search-and-traceability.md §2.1 IX_Relationship_Source / _Target), filtered to active edges.
        b.HasIndex(x => new { x.SourceType, x.SourceId }).HasFilter("[IsActive] = 1");
        b.HasIndex(x => new { x.TargetType, x.TargetId }).HasFilter("[IsActive] = 1");
        b.HasIndex(x => x.RelType).HasFilter("[IsActive] = 1");
    }
}
