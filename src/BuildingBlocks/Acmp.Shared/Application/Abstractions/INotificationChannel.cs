using Acmp.Shared.Contracts.Notifications;

namespace Acmp.Shared.Application.Abstractions;

// A delivery channel for notifications (ADR-0005). v1 registers exactly one implementation: the
// in-app notification center. Email/Webex are added later behind this same interface.
public interface INotificationChannel
{
    Task PublishAsync(NotificationMessage message, CancellationToken ct = default);
}
