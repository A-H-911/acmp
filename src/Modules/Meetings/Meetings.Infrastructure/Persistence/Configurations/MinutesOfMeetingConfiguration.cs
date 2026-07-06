using Acmp.Modules.Meetings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Meetings.Infrastructure.Persistence.Configurations;

// MinutesOfMeeting aggregate: its own table in the meetings schema. The MIN-YYYY-### key spans versions
// (a supersession adds a new row under the same key, Version+1), so the unique constraint is the composite
// (Key, Version) — NOT Key alone. Bilingual fields are owned LocalizedString value objects (en/ar column
// pair): Summary required, SupersessionReason optional. Cross-module references (MeetingId + snapshots)
// are plain values, no FK (ADR-0001).
public sealed class MinutesOfMeetingConfiguration : IEntityTypeConfiguration<MinutesOfMeeting>
{
    public void Configure(EntityTypeBuilder<MinutesOfMeeting> b)
    {
        b.ToTable("minutes_of_meeting");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/domain/data-architecture.md §1.5, ADR-0018)

        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.Property(x => x.Version).IsRequired();
        b.HasIndex(x => new { x.Key, x.Version }).IsUnique(); // one document, many versions

        b.Property(x => x.MeetingId).IsRequired();
        b.HasIndex(x => x.MeetingId);                 // "minutes for this meeting" access path
        b.Property(x => x.MeetingKey).IsRequired().HasMaxLength(32);
        b.Property(x => x.MeetingTitle).IsRequired().HasMaxLength(256);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();

        b.Property(x => x.ApprovedByUserId).HasMaxLength(128);
        b.Property(x => x.ApprovedByName).HasMaxLength(256);
        b.Property(x => x.ApprovedAt);
        b.Property(x => x.ApprovedBySoleAuthor);
        b.Property(x => x.PublishedAt);
        b.Property(x => x.SupersededByMinutesId);
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.Ignore(x => x.DomainEvents);

        // Required bilingual markdown body (en/ar columns NOT NULL, nvarchar(max) for long minutes).
        b.OwnsOne(x => x.Summary, t =>
        {
            t.Property(p => p.En).HasColumnName("summary_en").IsRequired();
            t.Property(p => p.Ar).HasColumnName("summary_ar").IsRequired();
        });
        b.Navigation(x => x.Summary).IsRequired();

        // Optional bilingual supersession reason — owned, nullable navigation (columns nullable).
        b.OwnsOne(x => x.SupersessionReason, t =>
        {
            t.Property(p => p.En).HasColumnName("supersession_reason_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("supersession_reason_ar").HasMaxLength(4000);
        });
    }
}
