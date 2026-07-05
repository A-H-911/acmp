using Acmp.Modules.Notifications.Application.Abstractions;
using Acmp.Modules.Notifications.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Notifications.Infrastructure.Persistence;

// Maps ONLY the notifications schema (docs/domain/repository-structure.md §12; ADR-0001). RecipientUserId is a Keycloak subject by
// value — no FK navigation to Membership.
public sealed class NotificationsDbContext : ModuleDbContext, INotificationsDbContext
{
    public const string Schema = "notifications";

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options, clock, currentUser)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
