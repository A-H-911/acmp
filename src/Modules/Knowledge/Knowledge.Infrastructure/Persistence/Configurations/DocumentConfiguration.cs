using System.Text.Json;
using Acmp.Modules.Knowledge.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Acmp.Modules.Knowledge.Infrastructure.Persistence.Configurations;

// Document aggregate → the documents table + the owned knowledge_document_versions child table (FK back to the
// document). Bilingual fields are owned LocalizedString value objects (en/ar column pair). Tags are a short
// string list stored as a single JSON column (backing field, mirrors TopicConfiguration). The Status + Key
// indexes back the register filters and the unique display key.
public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    private static readonly ValueConverter<List<string>, string> ListConverter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

    private static readonly ValueComparer<List<string>> ListComparer = new(
        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
        v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
        v => v.ToList());

    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/domain/data-architecture.md §1.5, ADR-0018)

        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.HasIndex(x => x.Status);

        b.Property(x => x.Category).IsRequired().HasMaxLength(128);
        b.Property(x => x.OwnerUserId).IsRequired().HasMaxLength(128);
        b.Property(x => x.Version).IsRequired();

        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.Ignore(x => x.DomainEvents);

        // Tags: short string list via a JSON column (backing field).
        b.Property<List<string>>("_tags").HasColumnName("tags").HasConversion(ListConverter, ListComparer).IsRequired();
        b.Ignore(x => x.Tags);

        // Required bilingual fields (en/ar columns NOT NULL).
        b.OwnsOne(x => x.Title, t =>
        {
            t.Property(p => p.En).HasColumnName("title_en").IsRequired().HasMaxLength(512);
            t.Property(p => p.Ar).HasColumnName("title_ar").IsRequired().HasMaxLength(512);
        });
        b.Navigation(x => x.Title).IsRequired();

        b.OwnsOne(x => x.Body, t =>
        {
            t.Property(p => p.En).HasColumnName("body_en").IsRequired();
            t.Property(p => p.Ar).HasColumnName("body_ar").IsRequired();
        });
        b.Navigation(x => x.Body).IsRequired();

        // Owned immutable version snapshots (FR-117) — same owned-collection pattern as research_findings.
        b.Navigation(x => x.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Versions, v =>
        {
            v.ToTable("knowledge_document_versions");
            v.WithOwner().HasForeignKey("DocumentEntityId");
            v.HasKey(x => x.Id);
            v.Property(x => x.Id).ValueGeneratedOnAdd();
            v.HasIndex(x => x.PublicId).IsUnique();
            v.Property(x => x.Version).IsRequired();
            v.Property(x => x.SavedAt).IsRequired();
            v.Property(x => x.SavedByUserId).IsRequired().HasMaxLength(128);
            v.Ignore(x => x.DomainEvents);

            v.OwnsOne(x => x.Title, t =>
            {
                t.Property(p => p.En).HasColumnName("title_en").IsRequired().HasMaxLength(512);
                t.Property(p => p.Ar).HasColumnName("title_ar").IsRequired().HasMaxLength(512);
            });
            v.Navigation(x => x.Title).IsRequired();

            v.OwnsOne(x => x.Body, t =>
            {
                t.Property(p => p.En).HasColumnName("body_en").IsRequired();
                t.Property(p => p.Ar).HasColumnName("body_ar").IsRequired();
            });
            v.Navigation(x => x.Body).IsRequired();
        });
    }
}

internal sealed class KnowledgeKeyCounterConfiguration : IEntityTypeConfiguration<KnowledgeKeyCounter>
{
    public void Configure(EntityTypeBuilder<KnowledgeKeyCounter> b)
    {
        b.ToTable("knowledge_key_counters");
        b.HasKey(x => new { x.Prefix, x.Year });
        b.Property(x => x.Prefix).HasMaxLength(8);
        b.Property(x => x.Next).IsRequired();
    }
}
