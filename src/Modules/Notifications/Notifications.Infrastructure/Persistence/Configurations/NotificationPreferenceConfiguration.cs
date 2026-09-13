using Acmp.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Notifications.Infrastructure.Persistence.Configurations;

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> b)
    {
        b.ToTable("notification_preferences");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);

        b.Property(x => x.UserId).IsRequired().HasMaxLength(128);
        b.Property(x => x.Category).IsRequired().HasMaxLength(64);
        b.Property(x => x.Channel).IsRequired().HasMaxLength(16);
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);

        // One choice per member, event type and channel: a second row would make "is it on?" ambiguous.
        // Also the in-app filter's lookup path. Only SQL Server enforces it (NotificationPreferenceSqlTests).
        b.HasIndex(x => new { x.UserId, x.Category, x.Channel }).IsUnique();
        b.Ignore(x => x.DomainEvents);
    }
}
