using Acmp.Api.Infrastructure;
using Acmp.Modules.Integrations.Webex.Oauth;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acmp.Api.Tests;

// The DI-resolving MigrateAsync(app) wrapper is exercised by the app boot; these cover the retry loop
// (RunAsync) directly through its migrate/delay seams — no real DB, no real 5s waits.
public class MigrationRunnerTests
{
    // A never-used DbContext instance — the injected migrate delegate ignores it, so no provider is needed.
    private static DbContext Ctx() => new(new DbContextOptionsBuilder<DbContext>().Options);
    private static readonly Func<TimeSpan, Task> NoDelay = _ => Task.CompletedTask;

    [Fact]
    public async Task Applies_every_context_then_returns_on_success()
    {
        var applied = 0;
        await MigrationRunner.RunAsync(
            new List<DbContext> { Ctx(), Ctx() }, NullLogger.Instance,
            _ => { applied++; return Task.CompletedTask; }, NoDelay);

        applied.Should().Be(2);
    }

    [Fact]
    public async Task Retries_after_a_transient_failure_then_succeeds()
    {
        var attempts = 0;
        await MigrationRunner.RunAsync(
            new List<DbContext> { Ctx() }, NullLogger.Instance,
            _ =>
            {
                attempts++;
                if (attempts == 1) throw new InvalidOperationException("db warming up");
                return Task.CompletedTask;
            },
            NoDelay);

        attempts.Should().Be(2); // failed once, retried, succeeded
    }

    [Fact]
    public async Task Gives_up_and_rethrows_after_the_final_attempt()
    {
        await FluentActions.Awaiting(() => MigrationRunner.RunAsync(
                new List<DbContext> { Ctx() }, NullLogger.Instance,
                _ => throw new InvalidOperationException("still down"), NoDelay, maxAttempts: 2))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Adds_the_webex_store_when_the_adapter_is_registered()
    {
        var webexDb = new WebexDbContext(new DbContextOptionsBuilder<WebexDbContext>().Options);
        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(WebexDbContext)).Returns(webexDb);
        var contexts = new List<DbContext>();

        MigrationRunner.AddWebexIfPresent(contexts, sp);

        contexts.Should().ContainSingle().Which.Should().BeSameAs(webexDb);
    }

    [Fact]
    public void Skips_the_webex_store_when_the_adapter_is_off()
    {
        var sp = Substitute.For<IServiceProvider>(); // GetService returns null for everything
        var contexts = new List<DbContext>();

        MigrationRunner.AddWebexIfPresent(contexts, sp);

        contexts.Should().BeEmpty();
    }
}
