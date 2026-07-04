using Acmp.Modules.Governance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Governance.Infrastructure.Persistence.Configurations;

// Adr aggregate → the adrs table + an owned adr_options child table (FK back to the ADR). Bilingual fields
// are owned LocalizedString value objects (en/ar column pair) — Title/Context/Decision required, the rest
// optional (nullable columns). People fields are Keycloak subject strings + a name snapshot. Cross-module
// references (SourceDecisionId, the supersession peer ids) are plain values, no FK (ADR-0001). The Status +
// Key indexes back the register filters and the unique display key.
public sealed class AdrConfiguration : IEntityTypeConfiguration<Adr>
{
    public void Configure(EntityTypeBuilder<Adr> b)
    {
        b.ToTable("adrs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/16 §1.5, ADR-0018)

        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.HasIndex(x => x.Status);

        b.Property(x => x.AuthorUserId).IsRequired().HasMaxLength(128);
        b.Property(x => x.AuthorName).IsRequired().HasMaxLength(256);
        b.Property(x => x.SourceDecisionId);

        b.Property(x => x.ApprovedAt);
        b.Property(x => x.ApprovedByUserId).HasMaxLength(128);
        b.Property(x => x.ApprovedByName).HasMaxLength(256);

        b.Property(x => x.SupersededByAdrId);
        b.Property(x => x.SupersedesAdrId);

        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.Ignore(x => x.DomainEvents);

        // Required bilingual fields (en/ar columns NOT NULL).
        b.OwnsOne(x => x.Title, t =>
        {
            t.Property(p => p.En).HasColumnName("title_en").IsRequired().HasMaxLength(512);
            t.Property(p => p.Ar).HasColumnName("title_ar").IsRequired().HasMaxLength(512);
        });
        b.Navigation(x => x.Title).IsRequired();

        b.OwnsOne(x => x.Context, t =>
        {
            t.Property(p => p.En).HasColumnName("context_en").IsRequired().HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("context_ar").IsRequired().HasMaxLength(4000);
        });
        b.Navigation(x => x.Context).IsRequired();

        b.OwnsOne(x => x.DecisionText, t =>
        {
            t.Property(p => p.En).HasColumnName("decision_en").IsRequired().HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("decision_ar").IsRequired().HasMaxLength(4000);
        });
        b.Navigation(x => x.DecisionText).IsRequired();

        // Optional bilingual fields — owned, nullable navigations (columns nullable).
        b.OwnsOne(x => x.DecisionDrivers, t =>
        {
            t.Property(p => p.En).HasColumnName("decision_drivers_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("decision_drivers_ar").HasMaxLength(4000);
        });
        b.OwnsOne(x => x.ConsequencesPositive, t =>
        {
            t.Property(p => p.En).HasColumnName("consequences_positive_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("consequences_positive_ar").HasMaxLength(4000);
        });
        b.OwnsOne(x => x.ConsequencesNegative, t =>
        {
            t.Property(p => p.En).HasColumnName("consequences_negative_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("consequences_negative_ar").HasMaxLength(4000);
        });
        b.OwnsOne(x => x.SupersessionReason, t =>
        {
            t.Property(p => p.En).HasColumnName("supersession_reason_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("supersession_reason_ar").HasMaxLength(4000);
        });
        b.OwnsOne(x => x.DeprecationReason, t =>
        {
            t.Property(p => p.En).HasColumnName("deprecation_reason_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("deprecation_reason_ar").HasMaxLength(4000);
        });

        // Owned considered options (docs/22 §A.7) — same shape as risk_mitigations.
        b.Navigation(x => x.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Options, o =>
        {
            o.ToTable("adr_options");
            o.WithOwner().HasForeignKey("AdrEntityId");
            o.HasKey(x => x.Id);
            o.Property(x => x.Id).ValueGeneratedOnAdd();
            o.HasIndex(x => x.PublicId).IsUnique();
            o.Property(x => x.IsChosen).IsRequired();
            o.Ignore(x => x.DomainEvents);

            o.OwnsOne(x => x.Name, t =>
            {
                t.Property(p => p.En).HasColumnName("name_en").IsRequired().HasMaxLength(512);
                t.Property(p => p.Ar).HasColumnName("name_ar").IsRequired().HasMaxLength(512);
            });
            o.Navigation(x => x.Name).IsRequired();

            o.OwnsOne(x => x.Body, t =>
            {
                t.Property(p => p.En).HasColumnName("body_en").HasMaxLength(4000);
                t.Property(p => p.Ar).HasColumnName("body_ar").HasMaxLength(4000);
            });
        });
    }
}

internal sealed class AdrKeyCounterConfiguration : IEntityTypeConfiguration<AdrKeyCounter>
{
    public void Configure(EntityTypeBuilder<AdrKeyCounter> b)
    {
        b.ToTable("adr_key_counters");
        b.HasKey(x => new { x.Prefix, x.Year });
        b.Property(x => x.Prefix).HasMaxLength(8);
        b.Property(x => x.Next).IsRequired();
    }
}
