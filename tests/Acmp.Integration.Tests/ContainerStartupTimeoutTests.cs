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

    private const string BuildName = "forced-build (temporary Dockerfile)";

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

    // DW-085's proof. Its own row predicted this could NOT be forced "without committing a
    // deliberately-hanging Dockerfile to this repository, which is a worse thing to own than the gap".
    // That was too pessimistic, and the distinction it missed is the useful one: the objection is to
    // OWNING a hanging Dockerfile - a file sitting in deploy/ that looks like a shipped artifact and
    // that someone could build by accident. A Dockerfile written to a temp directory at run time and
    // deleted afterwards is unambiguous test scaffolding, ships nothing, and still forces the real
    // build path through the real builder. So this guard is FORCED, to the same standard as the
    // container one - it does not borrow that standard by association.
    [Fact]
    public async Task An_image_build_that_never_finishes_fails_fast_and_says_there_is_no_log()
    {
        var dir = Directory.CreateTempSubdirectory("acmp-forced-build-");
        try
        {
            // Alpine is already local by the time this runs, so the measured window is the BUILD, not a pull.
            File.WriteAllText(Path.Combine(dir.FullName, "Dockerfile"), "FROM alpine:3.20\nRUN sleep 600\n");

            var image = new ImageFromDockerfileBuilder()
                .WithDockerfileDirectory(dir.FullName)
                .WithDockerfile("Dockerfile")
                .WithName("acmp/forced-build-timeout:test")
                .WithCleanUp(true)
                .Build();

            var build = () => ContainerStartup.BuildOrFailFastAsync(image, BuildName, ForcedBudget);

            var thrown = await build.Should().ThrowAsync<TimeoutException>(
                "a build that never finishes must fail with a named timeout, not run to the job ceiling");

            thrown.Which.Message.Should().Contain("did not finish building in 10 seconds",
                "the message must name the bound it exceeded");

            thrown.Which.Message.Should().Contain(BuildName,
                "the message must name WHICH build hung - there is no container log to identify it by");

            // LL-022: the framework already throws TimeoutException, so asserting the type alone would pass
            // vacuously. The clause below is the part this guard actually adds, and it is asserted for that
            // reason - a reader must not be left hunting for a log that cannot exist yet.
            thrown.Which.Message.Should().Contain("no container log to attach");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
