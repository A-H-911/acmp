using DotNet.Testcontainers.Builders;
using FluentAssertions;

namespace Acmp.Integration.Tests;

// DW-084's proof, and it is deliberately a FORCED failure. The hang it guards against is intermittent,
// so a green run proves nothing about the timeout path — the row says exactly this, and it is the same
// rule every other guard here is held to: prove it by forcing its refusal.
//
// The forced container runs, prints, and never satisfies its wait strategy: the shape of a hung SQL
// Server, minus the 25 minutes. Both halves of the contract are asserted, because either alone would
// pass vacuously — the failure must be FAST AND NAMED, and it must carry the container's OWN output.
// Requires a running Docker daemon, like the other container suites here.
public sealed class ContainerStartupTimeoutTests
{
    private const string Marker = "acmp-forced-startup-marker";
    private const string NeverLogged = "acmp-message-that-is-never-logged";

    private static readonly TimeSpan ForcedBudget = TimeSpan.FromSeconds(10);

    // Image passed to the constructor, matching the other fixtures here: the parameterless ctor is
    // obsolete, and an explicit tag means a Testcontainers upgrade cannot silently move it.
    private static ContainerBuilder Alpine() => new ContainerBuilder("alpine:3.20")
        .WithEntrypoint("/bin/sh", "-c")
        .WithCommand($"echo {Marker}; sleep 300");

    [Fact]
    public async Task A_container_that_never_becomes_ready_fails_fast_and_carries_its_own_log()
    {
        // Positive control, and it is load-bearing twice over. It proves the helper does not break an
        // ordinary start (a guard that refuses everything is not a guard), and it warms the image so the
        // 10-second bound below measures READINESS rather than a cold pull — a pull inside the measured
        // window would fire the budget before the container existed, and the log tail would then fall
        // back to a placeholder, silently gutting the second assertion into a pass over nothing.
        await using (var warm = Alpine()
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(Marker))
            .Build())
        {
            await ContainerStartup.StartOrFailFastAsync(warm, "warm-up");
        }

        await using var hung = Alpine()
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(NeverLogged))
            .Build();

        var start = () => ContainerStartup.StartOrFailFastAsync(hung, "forced", ForcedBudget);

        var thrown = await start.Should().ThrowAsync<TimeoutException>(
            "a container that never becomes ready must fail with a named timeout, not be retried until the job ceiling");

        thrown.Which.Message.Should().Contain("did not become ready in 10 seconds",
            "the message must name the container and the bound it exceeded — `cancelled` named nothing");

        thrown.Which.Message.Should().Contain(Marker,
            "the container's own startup log must be attached; a fast failure with no log is the `cancelled` verdict again, just sooner");
    }
}
