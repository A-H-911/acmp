using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Integrations.Webex;

// The Webex INotificationSink (ADR-0005, P13): mirrors committee-wide events to the Webex space. Because a
// single event fans out to one NotificationMessage PER recipient, this scoped sink collapses them by
// (Category + DeepLink) so exactly ONE card is posted per event (the space is shared — recipient identity is
// irrelevant). It only ENQUEUES a Hangfire job (fast) and swallows its own errors: Webex is optional and must
// never break the in-app write, which is the notification system of record.
public sealed class WebexNotificationSink : INotificationSink
{
    private readonly WebexOptions _options;
    private readonly IWebexJobScheduler _scheduler;
    private readonly ILogger<WebexNotificationSink> _logger;

    // Scoped state: one HTTP request / Hangfire job = one DI scope = one event fan-out.
    private readonly HashSet<string> _postedThisScope = new(StringComparer.Ordinal);

    public WebexNotificationSink(IOptions<WebexOptions> options, IWebexJobScheduler scheduler,
        ILogger<WebexNotificationSink> logger)
    {
        _options = options.Value;
        _scheduler = scheduler;
        _logger = logger;
    }

    public Task PublishAsync(NotificationMessage message, CancellationToken ct = default)
    {
        try
        {
            if (!_options.Enabled) return Task.CompletedTask;
            if (!WebexEligibleEvents.Includes(message.Category)) return Task.CompletedTask;

            var eventKey = message.Category + "|" + (message.DeepLink ?? string.Empty);
            if (!_postedThisScope.Add(eventKey)) return Task.CompletedTask; // already posted for this event

            var json = AdaptiveCardBuilder.BuildSpaceMessageJson(
                _options.SpaceId, message, _options.AcmpBaseUrl, _options.DefaultLanguage);
            _scheduler.Enqueue<WebexSendJob>(job => job.SendSpaceMessageAsync(json, CancellationToken.None));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue Webex card for category {Category}", message.Category);
        }

        return Task.CompletedTask;
    }
}
