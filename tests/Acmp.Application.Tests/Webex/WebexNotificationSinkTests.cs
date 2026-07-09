using System.Linq.Expressions;
using Acmp.Modules.Integrations.Webex;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acmp.Application.Tests.Webex;

// The Webex sink gate: only committee-wide events when enabled, exactly one space post per event (recipient
// fan-out collapsed), and enqueue failures swallowed so the in-app write is never broken.
public class WebexNotificationSinkTests
{
    private static NotificationMessage Msg(string category, string? link = "/meetings/MTG-2026-001") =>
        new("kc-a", LocalizedString.Create("t", "ت"), LocalizedString.Create("b", "ب"), category, link);

    private static WebexNotificationSink Sink(IWebexJobScheduler scheduler, bool enabled = true) =>
        new(Options.Create(new WebexOptions { Enabled = enabled, SpaceId = "room", AcmpBaseUrl = "https://acmp.local" }),
            scheduler, NullLogger<WebexNotificationSink>.Instance);

    private static int Enqueues(IWebexJobScheduler scheduler) =>
        scheduler.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IWebexJobScheduler.Enqueue));

    [Fact]
    public async Task Enqueues_one_card_for_an_eligible_event()
    {
        var scheduler = Substitute.For<IWebexJobScheduler>();
        await Sink(scheduler).PublishAsync(Msg("AgendaPublished"));
        Enqueues(scheduler).Should().Be(1);
    }

    [Fact]
    public async Task Does_nothing_when_disabled()
    {
        var scheduler = Substitute.For<IWebexJobScheduler>();
        await Sink(scheduler, enabled: false).PublishAsync(Msg("AgendaPublished"));
        Enqueues(scheduler).Should().Be(0);
    }

    [Fact]
    public async Task Ignores_non_committee_wide_events()
    {
        var scheduler = Substitute.For<IWebexJobScheduler>();
        // VoteOpened / RiskEscalated are subset-targeted — must not broadcast to the shared space.
        await Sink(scheduler).PublishAsync(Msg("VoteOpened"));
        await Sink(scheduler).PublishAsync(Msg("RiskEscalated"));
        Enqueues(scheduler).Should().Be(0);
    }

    [Fact]
    public async Task Collapses_a_recipient_fan_out_to_one_post_per_event()
    {
        var scheduler = Substitute.For<IWebexJobScheduler>();
        var sink = Sink(scheduler);

        // Same event, three recipients (same Category + DeepLink) → one post.
        foreach (var _ in new[] { "kc-a", "kc-b", "kc-c" })
            await sink.PublishAsync(Msg("AgendaPublished"));
        // A different event (different DeepLink) → a second post.
        await sink.PublishAsync(Msg("MeetingScheduled", "/meetings/MTG-2026-002"));

        Enqueues(scheduler).Should().Be(2);
    }

    [Fact]
    public async Task Swallows_enqueue_failures_so_the_in_app_write_is_never_broken()
    {
        var scheduler = Substitute.For<IWebexJobScheduler>();
        scheduler.When(s => s.Enqueue<WebexSendJob>(Arg.Any<Expression<Func<WebexSendJob, Task>>>()))
            .Do(_ => throw new InvalidOperationException("hangfire down"));

        var act = () => Sink(scheduler).PublishAsync(Msg("AgendaPublished"));

        await act.Should().NotThrowAsync();
    }
}
