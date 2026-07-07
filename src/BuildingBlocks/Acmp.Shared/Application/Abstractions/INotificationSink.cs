using Acmp.Shared.Contracts.Notifications;

namespace Acmp.Shared.Application.Abstractions;

// A single delivery channel behind the notification dispatcher (ADR-0005). v1 registers exactly one sink
// — the in-app notification center; the Webex adapter (P13) registers a second. The dispatcher
// (INotificationChannel) fans one NotificationMessage out to every registered sink. Sinks own their
// failure isolation: an optional channel (Webex) must never break the in-app write, which is the SoR.
public interface INotificationSink
{
    Task PublishAsync(NotificationMessage message, CancellationToken ct = default);
}
