using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;

namespace Acmp.Integration.Tests;

// DW-084 (DEC-077 d4/d5). Every container start in this suite goes through here, so a container that
// comes up but never becomes READY fails fast and legibly instead of being retried until the backend
// job hits its own `timeout-minutes: 25` — which GitHub reports as `cancelled`, a verdict that is
// neither pass nor fail, names nothing, and costs 25 minutes of runner time (DEF-108, data point 3).
//
// ⚠ This changes how that failure PRESENTS. It does not explain why SQLPAL failed to start, and it
// does NOT close DEF-108, which stays Open at high severity by DEC-077 d1.
internal static class ContainerStartup
{
    // The bound covers pull + create + boot + wait strategy, not just the boot: SqlBackstopFixture's
    // start includes a cold ~1.5 GB image pull on a fresh runner. Deliberately generous — a bound
    // tight enough to fire on a slow-but-healthy start would manufacture exactly the red DEC-077 d3
    // now turns into a mandatory operator stop. The whole green backend job runs in ~9 minutes, so
    // 10 still turns a 25-minute `cancelled` into a fast, named failure.
    internal static readonly TimeSpan Budget = TimeSpan.FromMinutes(10);

    // The log fetch is itself a Docker call, so it gets its own bound: a diagnostic that hangs would
    // reintroduce the exact failure this file exists to remove.
    private static readonly TimeSpan LogFetchBudget = TimeSpan.FromSeconds(30);

    private const int MaxLogChars = 8000;

    public static async Task StartOrFailFastAsync(IContainer container, string name, TimeSpan? budget = null)
    {
        var bound = budget ?? Budget;
        using var cts = new CancellationTokenSource(bound);

        try
        {
            await container.StartAsync(cts.Token);
        }
        // Testcontainers surfaces a cancelled wait strategy as TimeoutException and a cancelled Docker
        // API call as OperationCanceledException. The hang this exists for is the former, but which one
        // arrives depends on where the budget expires, so both are caught — and the IsCancellationRequested
        // filter keeps a genuine (not-our-timeout) failure unwrapped and reported as itself.
        //
        // ⚠ MEASURED, and it is why a bound alone is not enough: the framework's own exception is
        // TimeoutException("The operation has timed out.") — no container, no bound, no log. Setting
        // TestcontainersSettings.WaitStrategyTimeout would stop the hang and still tell nobody what hung.
        catch (Exception ex)
        {
            // DEF-121 — lift the container's own log directory out BEFORE anything disposes it. This runs on
            // EVERY start failure, not just our timeout, because the occurrence that motivated it was neither
            // a hang nor a timeout: the container exited with code 1 and Testcontainers threw
            // ContainerNotRunningException, which this method used to let straight through.
            //
            // ⚠ The capture NEVER throws, so the original failure is always the exception that surfaces.
            var artefacts = await CrashArtefacts.CaptureAsync(container, name);

            if (ex is TimeoutException or OperationCanceledException && cts.IsCancellationRequested)
                throw new TimeoutException(
                    $"{name} container did not become ready in {bound.TotalSeconds:0} seconds. " +
                    $"Container startup log:{Environment.NewLine}{await LogTailAsync(container)}" +
                    $"{Environment.NewLine}{artefacts}",
                    ex);

            // Anything else — a crashed container, a bad image, a Docker API error — is reported as ITSELF.
            // Wrapping it would hide the exception type the defect register discriminates on: DEF-121's
            // signature is a ContainerNotRunningException, and DEF-109's is a TaskCanceledException in a
            // different suite entirely.
            Console.WriteLine($"[{name}] start failed: {ex.GetType().Name}. {artefacts}");
            throw;
        }
    }

    // DW-085 (DEC-078 d2). The image BUILD is the other unbounded await on this path: SearchProvidersFtsTests
    // builds deploy/Dockerfile.sqlserver - the 3.62 GB FTS image (DW-059 measured it) - before any container
    // exists. Normally cached, so the hazard only appears on a runner with a cold cache or a stalled build,
    // which is the same conditional visibility that let the startup hang sit unnoticed.
    //
    // The budgets are chosen together, not separately: a build and a start can BOTH be slow in one run, and
    // 8 + 10 = 18 minutes still leaves the backend job room under its own timeout-minutes: 25 to fail, report,
    // and finish. Eight minutes is roughly 3x the ~160s a genuine cold build of that image takes.
    internal static readonly TimeSpan BuildBudget = TimeSpan.FromMinutes(8);

    public static async Task BuildOrFailFastAsync(IFutureDockerImage image, string name, TimeSpan? budget = null)
    {
        var bound = budget ?? BuildBudget;
        using var cts = new CancellationTokenSource(bound);

        try
        {
            await image.CreateAsync(cts.Token);
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException && cts.IsCancellationRequested)
        {
            // No container exists yet, so unlike StartOrFailFastAsync there is no log to attach. SAY that,
            // rather than leaving a reader hunting for one: an empty diagnostic that reads like a full one
            // is worse than a bare timeout.
            throw new TimeoutException(
                $"{name} image did not finish building in {bound.TotalSeconds:0} seconds. " +
                "No container exists at this point, so there is no container log to attach. " +
                "Look for the timestamped [docker-build ...] lines above: the gap between two of them is " +
                "where the build stalled. IF THERE ARE NONE, the image was built without " +
                "`.WithLogger(DockerBuildLog.Instance)` and that omission is the first thing to fix - " +
                "this message previously claimed build output existed when nothing had captured it (DEF-140).",
                ex);
        }
    }

    private static async Task<string> LogTailAsync(IContainer container)
    {
        try
        {
            using var cts = new CancellationTokenSource(LogFetchBudget);
            var (stdout, stderr) = await container.GetLogsAsync(timestampsEnabled: false, ct: cts.Token);
            var log = string.Concat(stdout, stderr).Trim();

            if (log.Length == 0)
                return "(the container produced no output)";

            return log.Length <= MaxLogChars ? log : "…" + log[^MaxLogChars..];
        }
        catch (Exception ex)
        {
            // The container may never have been created — the budget can expire during the image pull —
            // in which case there is no log to read. Say which, so an unreadable log never reads as a
            // quiet one: an empty tail and a failed fetch are different findings.
            return $"(the container log could not be read: {ex.GetType().Name}: {ex.Message})";
        }
    }
}
