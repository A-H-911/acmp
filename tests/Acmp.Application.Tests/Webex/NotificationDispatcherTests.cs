using Acmp.Modules.Notifications.Application.Channels;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace Acmp.Application.Tests.Webex;

// The dispatcher (the single INotificationChannel) fans one message out to every registered sink.
public class NotificationDispatcherTests
{
    [Fact]
    public async Task Fans_out_to_every_registered_sink()
    {
        var a = Substitute.For<INotificationSink>();
        var b = Substitute.For<INotificationSink>();
        var message = new NotificationMessage("kc-a", LocalizedString.Create("t", "ت"),
            LocalizedString.Create("b", "ب"), "AgendaPublished", "/x");

        await new NotificationDispatcher(new[] { a, b }).PublishAsync(message);

        await a.Received(1).PublishAsync(message, Arg.Any<CancellationToken>());
        await b.Received(1).PublishAsync(message, Arg.Any<CancellationToken>());
    }
}
