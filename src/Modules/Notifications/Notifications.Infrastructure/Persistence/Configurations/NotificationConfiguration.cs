using Acmp.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Notifications.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);

        b.Property(x => x.RecipientUserId).IsRequired().HasMaxLength(128);
        b.OwnsOne(x => x.Title, t =>
        {
            t.Property(p => p.En).HasColumnName("title_en").IsRequired().HasMaxLength(256);
            t.Property(p => p.Ar).HasColumnName("title_ar").IsRequired().HasMaxLength(256);
        });
        b.OwnsOne(x => x.Body, t =>
        {
            t.Property(p => p.En).HasColumnName("body_en").IsRequired().HasMaxLength(1024);
            t.Property(p => p.Ar).HasColumnName("body_ar").IsRequired().HasMaxLength(1024);
        });
        b.Property(x => x.Category).IsRequired().HasMaxLength(64);
        b.Property(x => x.DeepLink).HasMaxLength(512);
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);

        // The notification center query is "my unread/recent first" — index the access path.
        b.HasIndex(x => new { x.RecipientUserId, x.IsRead });
        b.Ignore(x => x.DomainEvents);
    }
}
