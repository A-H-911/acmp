using Acmp.Modules.Risks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Risks.Infrastructure.Persistence.Configurations;

// Risk aggregate → the risks table + an owned risk_mitigations child table (FK back to the risk). Bilingual
// fields are owned LocalizedString value objects (en/ar column pair) — Title required, the rest optional
// (nullable columns). People fields are Keycloak subject strings + a name snapshot. Cross-module references
// (subject id/key, a mitigation's linked action id) are plain values, no FK (ADR-0001). The (SubjectType,
// SubjectId) index backs "risks against this artifact" and the P10 traceability lookups. Severity/Exposure
// are NOT stored — they are derived at read time (docs/12 line 247).
public sealed class RiskConfiguration : IEntityTypeConfiguration<Risk>
{
    public void Configure(EntityTypeBuilder<Risk> b)
    {
        b.ToTable("risks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/16 §1.5, ADR-0018)

        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.Likelihood).HasConversion<int>().IsRequired();
        b.Property(x => x.Impact).HasConversion<int>().IsRequired();

        b.Property(x => x.OwnerUserId).IsRequired().HasMaxLength(128);
        b.HasIndex(x => x.OwnerUserId);           // "my risks" access path
        b.Property(x => x.OwnerName).IsRequired().HasMaxLength(256);

        b.Property(x => x.SubjectType).HasConversion<int>().IsRequired();
        b.Property(x => x.SubjectId).IsRequired();
        b.HasIndex(x => new { x.SubjectType, x.SubjectId }); // "risks against this artifact" + traceability
        b.Property(x => x.SubjectKey).HasMaxLength(32);

        b.Property(x => x.ClosedAt);
        b.Property(x => x.AcceptingAuthority).HasMaxLength(256);
        b.Property(x => x.EscalationTarget).HasMaxLength(256);

        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.Ignore(x => x.DomainEvents);

        // Required bilingual title (en/ar columns NOT NULL).
        b.OwnsOne(x => x.Title, t =>
        {
            t.Property(p => p.En).HasColumnName("title_en").IsRequired().HasMaxLength(512);
            t.Property(p => p.Ar).HasColumnName("title_ar").IsRequired().HasMaxLength(512);
        });
        b.Navigation(x => x.Title).IsRequired();

        // Optional bilingual fields — owned, nullable navigations (columns nullable).
        b.OwnsOne(x => x.Description, t =>
        {
            t.Property(p => p.En).HasColumnName("description_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("description_ar").HasMaxLength(4000);
        });
        b.OwnsOne(x => x.ClosureNote, t =>
        {
            t.Property(p => p.En).HasColumnName("closure_note_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("closure_note_ar").HasMaxLength(4000);
        });
        b.OwnsOne(x => x.AcceptanceRationale, t =>
        {
            t.Property(p => p.En).HasColumnName("acceptance_rationale_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("acceptance_rationale_ar").HasMaxLength(4000);
        });
        b.OwnsOne(x => x.EscalationReason, t =>
        {
            t.Property(p => p.En).HasColumnName("escalation_reason_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("escalation_reason_ar").HasMaxLength(4000);
        });

        // Owned mitigations (docs/11 §Mitigation) — same shape as decision_conditions.
        b.Navigation(x => x.Mitigations).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Mitigations, m =>
        {
            m.ToTable("risk_mitigations");
            m.WithOwner().HasForeignKey("RiskEntityId");
            m.HasKey(x => x.Id);
            m.Property(x => x.Id).ValueGeneratedOnAdd();
            m.HasIndex(x => x.PublicId).IsUnique();
            m.Property(x => x.Type).HasConversion<int>();
            m.Property(x => x.Status).HasConversion<int>();
            m.Property(x => x.OwnerUserId).HasMaxLength(128);
            m.Property(x => x.LinkedActionId);
            m.Property(x => x.DueDate);
            m.Ignore(x => x.DomainEvents);

            // Required bilingual mitigation description, nested inside the owned collection.
            m.OwnsOne(x => x.Description, t =>
            {
                t.Property(p => p.En).HasColumnName("description_en").IsRequired().HasMaxLength(4000);
                t.Property(p => p.Ar).HasColumnName("description_ar").IsRequired().HasMaxLength(4000);
            });
            m.Navigation(x => x.Description).IsRequired();
        });
    }
}

internal sealed class RiskKeyCounterConfiguration : IEntityTypeConfiguration<RiskKeyCounter>
{
    public void Configure(EntityTypeBuilder<RiskKeyCounter> b)
    {
        b.ToTable("risk_key_counters");
        b.HasKey(x => new { x.Prefix, x.Year });
        b.Property(x => x.Prefix).HasMaxLength(8);
        b.Property(x => x.Next).IsRequired();
    }
}
