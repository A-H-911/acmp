using System.Linq.Expressions;
using Acmp.Modules.Integrations.Webex;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acmp.Application.Tests.Webex;

// The send job posts the pre-built card to the committee space. On a 429 it reschedules the SAME call for the
// server-supplied Retry-After and returns normally, so Hangfire does not also retry this attempt.
public class WebexSendJobTests
{
    private static WebexSendJob Job(IWebexApiClient client, IWebexJobScheduler scheduler) =>
        new(client, scheduler, NullLogger<WebexSendJob>.Instance);

    private static int Schedules(IWebexJobScheduler scheduler) =>
        scheduler.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IWebexJobScheduler.Schedule));

    [Fact]
    public async Task Posts_the_card_to_the_space()
    {
        var client = Substitute.For<IWebexApiClient>();
        var scheduler = Substitute.For<IWebexJobScheduler>();

        await Job(client, scheduler).SendSpaceMessageAsync("{\"roomId\":\"room\"}", default);

        await client.Received(1).PostSpaceMessageAsync("{\"roomId\":\"room\"}", Arg.Any<CancellationToken>());
        Schedules(scheduler).Should().Be(0);
    }

    [Fact]
    public async Task Reschedules_and_does_not_throw_when_rate_limited()
    {
        var client = Substitute.For<IWebexApiClient>();
        client.When(c => c.PostSpaceMessageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new WebexRateLimitException(TimeSpan.FromSeconds(30)));
        var scheduler = Substitute.For<IWebexJobScheduler>();

        var act = () => Job(client, scheduler).SendSpaceMessageAsync("{}", default);

        await act.Should().NotThrowAsync();
        Schedules(scheduler).Should().Be(1);
    }
}
