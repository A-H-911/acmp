using Acmp.Modules.Knowledge.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Knowledge.Infrastructure.Persistence.Configurations;

// Template aggregate → the templates table (flat — no owned collections, unlike Document). Name is an owned
// bilingual LocalizedString (en/ar column pair); Body is a single Markdown string column. TargetType + Status are
// stored as ints; the Status + TargetType indexes back the register filters and the unique display key.
public sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> b)
    {
        b.ToTable("templates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/domain/data-architecture.md §1.5, ADR-0018)

        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.HasIndex(x => x.Status);

        b.Property(x => x.TargetType).HasConversion<int>().IsRequired();
        b.HasIndex(x => x.TargetType);

        b.Property(x => x.Body).IsRequired();
        b.Property(x => x.Version).IsRequired();

        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.Ignore(x => x.DomainEvents);

        // Required bilingual name (en/ar columns NOT NULL).
        b.OwnsOne(x => x.Name, t =>
        {
            t.Property(p => p.En).HasColumnName("name_en").IsRequired().HasMaxLength(256);
            t.Property(p => p.Ar).HasColumnName("name_ar").IsRequired().HasMaxLength(256);
        });
        b.Navigation(x => x.Name).IsRequired();
    }
}
