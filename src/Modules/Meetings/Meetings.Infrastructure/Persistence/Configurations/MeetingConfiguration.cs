using Acmp.Modules.Meetings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Meetings.Infrastructure.Persistence.Configurations;

// Meeting aggregate: its own table + owned attendance/discussion child tables (FK back to the meeting).
// Cross-module references (chair/attendee user ids, discussion topic id) are plain values, no FK.
public sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> b)
    {
        b.ToTable("meetings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/16 §1.5, ADR-0018)
        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.CommitteeId);
        b.Property(x => x.ScheduledStart).IsRequired();
        b.Property(x => x.ScheduledEnd).IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.Type).HasConversion<int>().IsRequired();
        b.Property(x => x.Mode).HasConversion<int>().IsRequired();
        b.Property(x => x.Location).HasMaxLength(512);
        b.Property(x => x.JoinUrl).HasMaxLength(1024);
        b.Property(x => x.ChairUserId);
        b.Property(x => x.ChairName).IsRequired().HasMaxLength(256);
        b.Property(x => x.StartedAt);
        b.Property(x => x.HeldAt);
        b.Property(x => x.CancelledAt);
        b.Property(x => x.CancellationReason).HasMaxLength(2000);
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.ScheduledStart);
        b.Ignore(x => x.PresentCount);
        b.Ignore(x => x.DomainEvents);

        b.Navigation(x => x.Attendees).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Attendees, a =>
        {
            a.ToTable("meeting_attendance");
            a.WithOwner().HasForeignKey("MeetingEntityId");
            a.HasKey(x => x.Id);
            a.Property(x => x.Id).ValueGeneratedOnAdd();
            a.HasIndex(x => x.PublicId).IsUnique();
            a.HasIndex("MeetingEntityId", nameof(Attendance.UserId)).IsUnique();
            a.Property(x => x.UserId).IsRequired();
            a.Property(x => x.Name).IsRequired().HasMaxLength(256);
            a.Property(x => x.Role).HasConversion<int>();
            a.Property(x => x.Status).HasConversion<int>();
            a.Property(x => x.IsVotingEligible);
            a.Property(x => x.JoinedAt);
            a.Property(x => x.LeftAt);
            a.Ignore(x => x.DomainEvents);
        });

        b.Navigation(x => x.Discussions).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Discussions, d =>
        {
            d.ToTable("meeting_discussions");
            d.WithOwner().HasForeignKey("MeetingEntityId");
            d.HasKey(x => x.Id);
            d.Property(x => x.Id).ValueGeneratedOnAdd();
            d.HasIndex(x => x.PublicId).IsUnique();
            d.Property(x => x.TopicId).IsRequired();
            d.Property(x => x.Body).IsRequired();
            d.Property(x => x.AuthorSub).IsRequired().HasMaxLength(128);
            d.Property(x => x.AuthorName).IsRequired().HasMaxLength(256);
            d.Property(x => x.Origin).HasConversion<int>();
            d.Property(x => x.IsApproved);
            d.Property(x => x.CreatedAt);
            d.Property(x => x.UpdatedAt);
            d.Ignore(x => x.DomainEvents);
        });
    }
}

// Agenda aggregate: its own table + owned agenda-item child table. Belongs to one Meeting by id.
public sealed class AgendaConfiguration : IEntityTypeConfiguration<Agenda>
{
    public void Configure(EntityTypeBuilder<Agenda> b)
    {
        b.ToTable("agendas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/16 §1.5, ADR-0018)
        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();
        b.Property(x => x.MeetingId).IsRequired();
        b.HasIndex(x => x.MeetingId).IsUnique();   // one agenda per meeting
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.Version);
        b.Property(x => x.PublishedAt);
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.Ignore(x => x.TotalTimeboxMinutes);
        b.Ignore(x => x.DomainEvents);

        b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Items, i =>
        {
            i.ToTable("agenda_items");
            i.WithOwner().HasForeignKey("AgendaEntityId");
            i.HasKey(x => x.Id);
            i.Property(x => x.Id).ValueGeneratedOnAdd();
            i.HasIndex(x => x.PublicId).IsUnique();
            i.HasIndex("AgendaEntityId", nameof(AgendaItem.TopicId)).IsUnique();
            i.Property(x => x.TopicId).IsRequired();
            i.Property(x => x.TopicKey).IsRequired().HasMaxLength(32);
            i.Property(x => x.TopicTitle).IsRequired().HasMaxLength(256);
            i.Property(x => x.Urgent);
            i.Property(x => x.Order);
            i.Property(x => x.TimeboxMinutes);
            i.Property(x => x.PresenterUserId);
            i.Property(x => x.PresenterName).HasMaxLength(256);
            i.Property(x => x.Outcome).HasConversion<int>();
            i.Property(x => x.ActualMinutes);
            i.Property(x => x.CarryOverFromAgendaId);
            i.Ignore(x => x.DomainEvents);
        });
    }
}

internal sealed class MeetingKeyCounterConfiguration : IEntityTypeConfiguration<MeetingKeyCounter>
{
    public void Configure(EntityTypeBuilder<MeetingKeyCounter> b)
    {
        b.ToTable("meeting_key_counters");
        b.HasKey(x => new { x.Prefix, x.Year });
        b.Property(x => x.Prefix).HasMaxLength(8);
        b.Property(x => x.Next).IsRequired();
    }
}
