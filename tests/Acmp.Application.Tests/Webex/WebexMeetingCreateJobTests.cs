using System.Linq.Expressions;
using Acmp.Modules.Integrations.Webex;
using Acmp.Modules.Integrations.Webex.Oauth;
using Acmp.Shared.Contracts.Meetings;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acmp.Application.Tests.Webex;

// Meeting auto-create: with a valid OAuth token it creates the Webex meeting and writes id + join URL back.
// With no token (or no meeting created) it degrades gracefully — the ACMP meeting is never touched (AC-072).
public class WebexMeetingCreateJobTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static WebexMeetingCreateJob Job(IWebexTokenService tokens, IWebexApiClient client, IMeetingWebexWriter writer) =>
        new(tokens, client, writer, NullLogger<WebexMeetingCreateJob>.Instance);

    [Fact]
    public async Task Creates_the_webex_meeting_and_writes_the_correlation_back()
    {
        var tokens = Substitute.For<IWebexTokenService>();
        tokens.GetValidAccessTokenAsync(Arg.Any<CancellationToken>()).Returns("acc-1");
        var client = Substitute.For<IWebexApiClient>();
        client.CreateMeetingAsync("acc-1", "Committee", Start, Start.AddHours(1), Arg.Any<CancellationToken>())
            .Returns(new CreatedWebexMeeting("webex-9", "https://webex/join"));
        var writer = Substitute.For<IMeetingWebexWriter>();
        var meetingId = Guid.NewGuid();

        await Job(tokens, client, writer).CreateAsync(meetingId, "Committee", Start, Start.AddHours(1), default);

        await writer.Received(1).SetWebexMeetingAsync(meetingId, "webex-9", "https://webex/join", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_when_no_oauth_token_is_available()
    {
        var tokens = Substitute.For<IWebexTokenService>();
        tokens.GetValidAccessTokenAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        var client = Substitute.For<IWebexApiClient>();
        var writer = Substitute.For<IMeetingWebexWriter>();

        await Job(tokens, client, writer).CreateAsync(Guid.NewGuid(), "Committee", Start, Start.AddHours(1), default);

        await client.DidNotReceive().CreateMeetingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await writer.DidNotReceive().SetWebexMeetingAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_the_writeback_when_the_create_returns_no_meeting()
    {
        var tokens = Substitute.For<IWebexTokenService>();
        tokens.GetValidAccessTokenAsync(Arg.Any<CancellationToken>()).Returns("acc-1");
        var client = Substitute.For<IWebexApiClient>();
        client.CreateMeetingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns((CreatedWebexMeeting?)null);
        var writer = Substitute.For<IMeetingWebexWriter>();

        await Job(tokens, client, writer).CreateAsync(Guid.NewGuid(), "Committee", Start, Start.AddHours(1), default);

        await writer.DidNotReceive().SetWebexMeetingAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}

// The provisioner only enqueues for online meetings when enabled.
public class WebexMeetingProvisionerTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static int Enqueues(IWebexJobScheduler s) =>
        s.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IWebexJobScheduler.Enqueue));

    private static WebexMeetingProvisioner Provisioner(IWebexJobScheduler scheduler, bool enabled) =>
        new(Options.Create(new WebexOptions { Enabled = enabled }), scheduler);

    [Fact]
    public async Task Enqueues_a_create_job_for_an_online_meeting_when_enabled()
    {
        var scheduler = Substitute.For<IWebexJobScheduler>();
        await Provisioner(scheduler, enabled: true).ProvisionAsync(Guid.NewGuid(), "t", Start, Start.AddHours(1), isOnline: true);
        Enqueues(scheduler).Should().Be(1);
    }

    [Fact]
    public async Task Does_nothing_for_in_person_meetings_or_when_disabled()
    {
        var scheduler = Substitute.For<IWebexJobScheduler>();
        await Provisioner(scheduler, enabled: true).ProvisionAsync(Guid.NewGuid(), "t", Start, Start.AddHours(1), isOnline: false);
        await Provisioner(scheduler, enabled: false).ProvisionAsync(Guid.NewGuid(), "t", Start, Start.AddHours(1), isOnline: true);
        Enqueues(scheduler).Should().Be(0);
    }
}
