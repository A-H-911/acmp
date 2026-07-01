using Acmp.Modules.Decisions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Decisions.Infrastructure.Persistence.Configurations;

// Decision aggregate: its own table + an owned decision_conditions child table (FK back to the decision).
// Bilingual fields are owned LocalizedString value objects (en/ar column pair) — Rationale required, the
// rest optional (nullable columns). Cross-module references (topic/meeting/vote/linked-action ids) are
// plain values, no FK (ADR-0001).
public sealed class DecisionConfiguration : IEntityTypeConfiguration<Decision>
{
    public void Configure(EntityTypeBuilder<Decision> b)
    {
        b.ToTable("decisions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/16 §1.5, ADR-0018)
        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.TopicId).IsRequired();
        b.HasIndex(x => x.TopicId);                 // "decisions for this topic" access path
        b.Property(x => x.MeetingId);
        b.Property(x => x.Outcome).HasConversion<int>().IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.VoteId);
        b.Property(x => x.ChairApprovedByUserId).HasMaxLength(128);
        b.Property(x => x.ChairApprovedByName).HasMaxLength(256);
        b.Property(x => x.ChairOverride);
        b.Property(x => x.IssuedAt);
        b.Property(x => x.SupersededByDecisionId);
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

        // Required bilingual rationale (en/ar columns NOT NULL).
        b.OwnsOne(x => x.Rationale, t =>
        {
            t.Property(p => p.En).HasColumnName("rationale_en").IsRequired().HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("rationale_ar").IsRequired().HasMaxLength(4000);
        });
        b.Navigation(x => x.Rationale).IsRequired();

        // Optional bilingual fields — owned, nullable navigations (columns nullable).
        b.OwnsOne(x => x.Alternatives, t =>
        {
            t.Property(p => p.En).HasColumnName("alternatives_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("alternatives_ar").HasMaxLength(4000);
        });
        b.OwnsOne(x => x.OverrideJustification, t =>
        {
            t.Property(p => p.En).HasColumnName("override_justification_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("override_justification_ar").HasMaxLength(4000);
        });
        b.OwnsOne(x => x.SupersessionReason, t =>
        {
            t.Property(p => p.En).HasColumnName("supersession_reason_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("supersession_reason_ar").HasMaxLength(4000);
        });

        b.Navigation(x => x.Conditions).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Conditions, c =>
        {
            c.ToTable("decision_conditions");
            c.WithOwner().HasForeignKey("DecisionEntityId");
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).ValueGeneratedOnAdd();
            c.HasIndex(x => x.PublicId).IsUnique();
            c.Property(x => x.Status).HasConversion<int>();
            c.Property(x => x.DueDate);
            c.Property(x => x.LinkedActionId);
            c.Ignore(x => x.DomainEvents);

            // Required bilingual condition text, nested inside the owned collection.
            c.OwnsOne(x => x.Text, t =>
            {
                t.Property(p => p.En).HasColumnName("text_en").IsRequired().HasMaxLength(2000);
                t.Property(p => p.Ar).HasColumnName("text_ar").IsRequired().HasMaxLength(2000);
            });
            c.Navigation(x => x.Text).IsRequired();
        });
    }
}

internal sealed class DecisionKeyCounterConfiguration : IEntityTypeConfiguration<DecisionKeyCounter>
{
    public void Configure(EntityTypeBuilder<DecisionKeyCounter> b)
    {
        b.ToTable("decision_key_counters");
        b.HasKey(x => new { x.Prefix, x.Year });
        b.Property(x => x.Prefix).HasMaxLength(8);
        b.Property(x => x.Next).IsRequired();
    }
}
