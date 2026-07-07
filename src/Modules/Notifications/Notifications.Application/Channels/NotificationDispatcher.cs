using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Notifications;

namespace Acmp.Modules.Notifications.Application.Channels;

// The single INotificationChannel every module publishes through (ADR-0005). It fans each message out to
// every registered INotificationSink — the in-app center always, plus the Webex adapter (P13) when it is
// registered and enabled. The in-app sink runs first and its failures propagate (it is the notification
// system of record); optional sinks (Webex) swallow their own errors internally, so a Webex enqueue
// failure never blocks the in-app write. Registered as the one INotificationChannel; callers are unchanged.
public sealed class NotificationDispatcher : INotificationChannel
{
    private readonly IEnumerable<INotificationSink> _sinks;

    public NotificationDispatcher(IEnumerable<INotificationSink> sinks) => _sinks = sinks;

    public async Task PublishAsync(NotificationMessage message, CancellationToken ct = default)
    {
        foreach (var sink in _sinks)
            await sink.PublishAsync(message, ct);
    }
}
