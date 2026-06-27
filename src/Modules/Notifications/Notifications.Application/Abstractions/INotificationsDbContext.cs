using Acmp.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Notifications.Application.Abstractions;

// Narrow persistence port; the EF implementation lives in Infrastructure.
public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
