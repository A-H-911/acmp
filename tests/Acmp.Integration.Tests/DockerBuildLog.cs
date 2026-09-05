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

    // Bounded so a chatty build cannot turn one failure message into a megabyte. 200 lines is far more
    // than the FTS image's ~40 steps and still shows the whole run-up to a stall.
    private const int MaxLines = 200;

    private static readonly object Gate = new();
    private static readonly Queue<string> Lines = new();

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
        // Timestamped because the fault this exists for is a STALL: the useful signal is the GAP between
        // two lines, which a bare message cannot show.
        var line = $"[docker-build {DateTime.UtcNow:HH:mm:ss}] {formatter(state, exception)}";

        if (exception is not null)
        {
            line += $"{Environment.NewLine}[docker-build {DateTime.UtcNow:HH:mm:ss}] {exception.GetType().Name}: {exception.Message}";
        }

        // ponytail: one static buffer, because this suite builds exactly one image. If a second image ever
        // gets a logger, give each builder its own instance rather than untangling a shared queue.
        lock (Gate)
        {
            Lines.Enqueue(line);

            while (Lines.Count > MaxLines)
            {
                Lines.Dequeue();
            }
        }

        // Kept for local runs (`--logger "console;verbosity=detailed"`), where it is the nicer view.
        // ⚠ IT IS NOT THE DELIVERY MECHANISM — see Tail().
        Console.WriteLine(line);
    }

    /// <summary>
    /// The captured build output, for embedding in a failure message.
    /// </summary>
    /// <remarks>
    /// ⚠⚠ THIS EXISTS BECAUSE `Console.WriteLine` DOES NOT REACH THE CI JOB LOG. CI runs
    /// `dotnet test acmp.sln -c Release --no-build --collect:...` with no `--logger
    /// "console;verbosity=detailed"`, so console output written from a test is discarded. The first
    /// version of this class printed and nothing else, and CI run 33938255263 proved it: 18m25s, two
    /// 480-second build timeouts, and ZERO `[docker-build ...]` lines in the job log — the same silence
    /// it was written to remove.
    ///
    /// `LL-055`: a control proves FIRING, never COUPLING. The logger demonstrably RECEIVED Testcontainers'
    /// output; that says nothing about whether the output reaches a reader. Worse, the calibration hid it:
    /// the probe was run with `--logger "console;verbosity=detailed"` in order to SEE the output, and that
    /// flag supplied the very channel whose absence breaks it in CI.
    ///
    /// So delivery is via the exception message, which always reaches the log — the same choice
    /// <see cref="ContainerStartup.StartOrFailFastAsync"/> already makes by embedding the container log.
    /// </remarks>
    internal static string Tail()
    {
        lock (Gate)
        {
            return Lines.Count == 0
                ? "(no build output was captured - the image was built without .WithLogger(DockerBuildLog.Instance))"
                : string.Join(Environment.NewLine, Lines);
        }
    }
}
