using Acmp.Modules.Actions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Actions.Infrastructure.Persistence.Configurations;

// ActionItem aggregate → the actions table. Bilingual fields are owned LocalizedString value objects
// (en/ar column pair) — Title required, the rest optional (nullable columns). People fields are Keycloak
// subject strings + name snapshots. Cross-module references (source id/keys) are plain values, no FK
// (ADR-0001). The (SourceType, SourceId) index backs the P8d "does an action link this decision?" query.
public sealed class ActionConfiguration : IEntityTypeConfiguration<ActionItem>
{
    public void Configure(EntityTypeBuilder<ActionItem> b)
    {
        b.ToTable("actions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/16 §1.5, ADR-0018)

        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.Priority).HasConversion<int>().IsRequired();
        b.Property(x => x.ProgressPct).IsRequired();

        b.Property(x => x.OwnerUserId).IsRequired().HasMaxLength(128);
        b.HasIndex(x => x.OwnerUserId);           // "my actions" access path
        b.Property(x => x.OwnerName).IsRequired().HasMaxLength(256);
        b.Property(x => x.DueDate);

        b.Property(x => x.SourceType).HasConversion<int>().IsRequired();
        b.Property(x => x.SourceId).IsRequired();
        b.HasIndex(x => new { x.SourceType, x.SourceId }); // downstream-link lookup (AC-029, P8d)
        b.Property(x => x.SourceKey).HasMaxLength(32);
        b.Property(x => x.MeetingKey).HasMaxLength(32);

        b.Property(x => x.CompletedByUserId).HasMaxLength(128);
        b.Property(x => x.CompletedAt);
        b.Property(x => x.VerifiedByUserId).HasMaxLength(128);
        b.Property(x => x.VerifiedByName).HasMaxLength(256);
        b.Property(x => x.VerifiedAt);

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
        b.OwnsOne(x => x.BlockedReason, t =>
        {
            t.Property(p => p.En).HasColumnName("blocked_reason_en").HasMaxLength(2000);
            t.Property(p => p.Ar).HasColumnName("blocked_reason_ar").HasMaxLength(2000);
        });
        b.OwnsOne(x => x.CompletionNote, t =>
        {
            t.Property(p => p.En).HasColumnName("completion_note_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("completion_note_ar").HasMaxLength(4000);
        });
        b.OwnsOne(x => x.CancelReason, t =>
        {
            t.Property(p => p.En).HasColumnName("cancel_reason_en").HasMaxLength(2000);
            t.Property(p => p.Ar).HasColumnName("cancel_reason_ar").HasMaxLength(2000);
        });
    }
}

internal sealed class ActionKeyCounterConfiguration : IEntityTypeConfiguration<ActionKeyCounter>
{
    public void Configure(EntityTypeBuilder<ActionKeyCounter> b)
    {
        b.ToTable("action_key_counters");
        b.HasKey(x => new { x.Prefix, x.Year });
        b.Property(x => x.Prefix).HasMaxLength(8);
        b.Property(x => x.Next).IsRequired();
    }
}
