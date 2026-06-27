using Acmp.Modules.Notifications.Application.Abstractions;
using Acmp.Modules.Notifications.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Notifications;

namespace Acmp.Modules.Notifications.Application.Channels;

// The v1 notification channel (ADR-0005, AC-053): a synchronous write to the in-app notification center.
// Registered as the single INotificationChannel implementation; email/Webex are added later behind the
// same interface. Synchronous write meets AC-051's ≤5s for a ≤20-user committee — no queue/Hangfire here.
// Callers (e.g. Meetings) depend only on the Shared INotificationChannel interface, never on this module.
public sealed class InAppNotificationChannel : INotificationChannel
{
    private readonly INotificationsDbContext _db;

    public InAppNotificationChannel(INotificationsDbContext db) => _db = db;

    public async Task PublishAsync(NotificationMessage message, CancellationToken ct = default)
    {
        var notification = Notification.Create(
            message.RecipientUserId, message.Title, message.Body, message.Category, message.DeepLink);

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);
    }
}
