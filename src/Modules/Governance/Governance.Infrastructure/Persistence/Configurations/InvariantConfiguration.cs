using Acmp.Modules.Governance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Governance.Infrastructure.Persistence.Configurations;

// Invariant aggregate → the invariants table. Bilingual fields are owned LocalizedString value objects (en/ar
// column pair) — Statement/Rationale required, ExceptionsPolicy/reasons optional (nullable columns). Category
// and Scope are int-backed enums. Owner is a Keycloak subject string + name snapshot. Supersession peer ids
// are plain values, no FK (ADR-0001). The Status + Key indexes back the register filters and the unique key.
public sealed class InvariantConfiguration : IEntityTypeConfiguration<Invariant>
{
    public void Configure(EntityTypeBuilder<Invariant> b)
    {
        b.ToTable("invariants");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/domain/data-architecture.md §1.5, ADR-0018)

        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.HasIndex(x => x.Status);
        b.Property(x => x.Category).HasConversion<int>().IsRequired();
        b.Property(x => x.Scope).HasConversion<int>().IsRequired();

        b.Property(x => x.OwnerUserId).IsRequired().HasMaxLength(128);
        b.Property(x => x.OwnerName).IsRequired().HasMaxLength(256);

        b.Property(x => x.ActivatedAt);
        b.Property(x => x.ActivatedByUserId).HasMaxLength(128);
        b.Property(x => x.ActivatedByName).HasMaxLength(256);

        b.Property(x => x.SupersededByInvariantId);
        b.Property(x => x.SupersedesInvariantId);

        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.Ignore(x => x.DomainEvents);

        // Required bilingual fields (en/ar columns NOT NULL).
        b.OwnsOne(x => x.Statement, t =>
        {
            t.Property(p => p.En).HasColumnName("statement_en").IsRequired().HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("statement_ar").IsRequired().HasMaxLength(4000);
        });
        b.Navigation(x => x.Statement).IsRequired();

        b.OwnsOne(x => x.Rationale, t =>
        {
            t.Property(p => p.En).HasColumnName("rationale_en").IsRequired().HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("rationale_ar").IsRequired().HasMaxLength(4000);
        });
        b.Navigation(x => x.Rationale).IsRequired();

        // Optional bilingual fields — owned, nullable navigations (columns nullable).
        b.OwnsOne(x => x.ExceptionsPolicy, t =>
        {
            t.Property(p => p.En).HasColumnName("exceptions_policy_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("exceptions_policy_ar").HasMaxLength(4000);
        });
        b.OwnsOne(x => x.SupersessionReason, t =>
        {
            t.Property(p => p.En).HasColumnName("supersession_reason_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("supersession_reason_ar").HasMaxLength(4000);
        });
        b.OwnsOne(x => x.RetirementReason, t =>
        {
            t.Property(p => p.En).HasColumnName("retirement_reason_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("retirement_reason_ar").HasMaxLength(4000);
        });
    }
}
