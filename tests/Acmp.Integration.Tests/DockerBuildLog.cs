using Microsoft.Extensions.Logging;

namespace Acmp.Integration.Tests;

// DEF-140. `BuildOrFailFastAsync`'s timeout message ends "read the Docker build output above it" —
// and on 2026-09-05 (PR #365, run 33935474689) there was none. The FTS image build blew its 480s
// budget TWICE and the job log holds EXACTLY 480 seconds of silence between the previous test's
// SKIP line and the timeout, because Testcontainers routes build progress to an ILogger that
// nothing was listening to.
//
// ⚠⚠ THE GUARD WAS NEVER BROKEN. It fired correctly, on time, with a specific message — pointing at
// an artefact that was never captured. That converts an unbounded hang into a BOUNDED MYSTERY, which
// is better and is not diagnosis. DEF-139 is the same shape hours earlier on a different instrument
// (DEF-129 names a trace.zip that structurally cannot hold the guest page). An instrument must report
// on itself, and "read the output above" is a claim about the world that has to be true.
//
// Deliberately not TestcontainersSettings.Logger: that would route EVERY container's chatter through
// here for all 73 integration tests. This is attached only where the build actually happens.
internal sealed class DockerBuildLog : ILogger
{
    internal static readonly DockerBuildLog Instance = new();

    private DockerBuildLog()
    {
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // Timestamped because the fault this exists for is a STALL: the useful signal is the gap
        // between lines, which a bare message cannot show.
        Console.WriteLine($"[docker-build {DateTime.UtcNow:HH:mm:ss}] {formatter(state, exception)}");

        if (exception is not null)
        {
            Console.WriteLine($"[docker-build {DateTime.UtcNow:HH:mm:ss}] {exception.GetType().Name}: {exception.Message}");
        }
    }
}
