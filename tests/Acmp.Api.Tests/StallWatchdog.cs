using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Acmp.Api.Tests;

// WBS-27.1 / DEC-120 d3 — an instrument that can produce a DEF-109 artefact AT ALL.
//
// WHY THIS EXISTS, AND IT IS A STRUCTURAL GAP RATHER THAN AN OVERSIGHT. DEF-109's end condition
// (DEC-110 d3) carries clause (2): "it fires again and a captured artefact — a hang stack, a process
// dump, a runner resource metric — identifies a cause". No such artefact has ever existed for ANY of
// its four occurrences, and none could: the only capture this project owns (CrashArtefacts, DEC-112 d2)
// hooks ContainerNotRunningException, which is DEF-121/DEF-130's container-crash path. DEF-109's
// signature is TaskCanceledException at the 100-second HttpClient ceiling INSIDE this assembly's own
// process, where no container dies and there is nothing to `docker cp`. So clause (2) could not fire.
// A clause that CANNOT fire is indistinguishable from the outside from one that has NOT YET fired, and
// accumulated quiet reads as progress in both cases (PE-810).
//
// ⚠⚠ THE SAMPLER IS A DEDICATED THREAD AND THAT IS THE WHOLE DESIGN, NOT A DETAIL. The hypothesis this
// instrument exists to test is thread-pool starvation or a scheduling stall inside this process. A
// System.Threading.Timer, a Task.Delay loop, or anything else scheduled on the ThreadPool would be
// STARVED BY THE VERY CONDITION IT IS MEASURING — it would fall silent exactly when the fault fires, and
// the missing sample would read as the absence of the fault. That is LL-009's shape (an instrument that
// shares a mechanism with its subject) and DEF-078's (a green control that evaluated nothing).
//
// ⭐ TIMER DRIFT IS THE PRIMARY SIGNAL, and it is a DIRECT measure rather than an inference. A dedicated
// thread that asks to sleep 5s and wakes 40s later was not being scheduled, which is the same thing that
// happens to the in-process TestServer when a request burns its 100-second ceiling without a handler
// ever running.
//
// ⛔⛔ WHAT THIS DOES NOT DO, STATED UP FRONT BECAUSE DW-097 IS THIS PROJECT'S MODEL FOR SHIPPING AN
// INSTRUMENT (LL-035). It diagnoses nothing. It reduces the probability of nothing. It does not fix
// DEF-109, it does not close DEF-109, and a green run proves nothing about it. It makes clause (2)
// REACHABLE — no more. Whether the artefact it leaves NAMES A CAUSE is the question the next occurrence
// answers, and DEC-115 d2 and DEC-116 d1 have both already ruled that a successful capture and a refuted
// hypothesis are each NOT a satisfied clause.
//
// ⛔ AND IT IS NOT A WIDENING OF THE CONTAINER CAPTURE. DW-097's prohibition — "closing this row is not a
// licence to widen the capture later without evidence" — survives that row's closure and is deliberately
// respected: CrashArtefacts is untouched. This is a different mechanism for a different fault family,
// which is how DEC-120 d3 scoped it.
internal static class StallWatchdog
{
    /// <summary>Where snapshots land. CI points this at a workspace path it uploads on failure.</summary>
    internal static string Directory =>
        Environment.GetEnvironmentVariable("ACMP_STALL_ARTEFACT_DIR")
        ?? Path.Combine(Path.GetTempPath(), "acmp-stall-watchdog");

    /// <summary>How long the sampling thread asks to sleep between samples.</summary>
    internal static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);

    // A sample that arrives this much later than requested means the thread was not scheduled. Chosen
    // well above ordinary jitter on a loaded 4-core runner and well below the 100-second HttpClient
    // ceiling, so a stall is recorded WHILE it is happening rather than after the request has already
    // given up. It is a threshold on a continuous quantity, so the file records the observed drift and
    // lets a reader judge, rather than only recording that a bound was crossed.
    internal static readonly TimeSpan DriftThreshold = TimeSpan.FromSeconds(15);

    private static readonly object Gate = new();

    [ModuleInitializer]
    internal static void Start()
    {
        // Opt-out, not opt-in: the fault is intermittent and environmental, so an instrument that has to
        // be switched on for the run that happens to fail is an instrument that is never on.
        if (Environment.GetEnvironmentVariable("ACMP_STALL_WATCHDOG") == "off") return;

        var thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "acmp-stall-watchdog",
            // Above normal so that being descheduled is evidence about the PROCESS rather than about
            // this thread losing a fair race for a core.
            Priority = ThreadPriority.AboveNormal,
        };
        thread.Start();
    }

    private static void Loop()
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var before = stopwatch.Elapsed;
            Thread.Sleep(SampleInterval);
            var actual = stopwatch.Elapsed - before;

            try
            {
                CaptureIfDegraded(actual);
            }
            catch
            {
                // Never throw from the watchdog. It runs alongside a suite that is already in trouble,
                // and a diagnostic that takes the process down is worse than no diagnostic
                // (CrashArtefacts makes the same choice for the same reason).
            }
        }
    }

    /// <summary>
    /// Record a snapshot if this sample shows the process was not being scheduled, or the ThreadPool has
    /// work queued with no worker free. Returns true when a snapshot was written. Internal so a test can
    /// inject a drift and prove the instrument actually fires — a capture that has never been shown to
    /// produce output is DEF-078's green control with no subject (LL-013).
    /// </summary>
    internal static bool CaptureIfDegraded(TimeSpan actualInterval, string? destinationRoot = null)
    {
        var drift = actualInterval - SampleInterval;
        ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIo);
        var pending = ThreadPool.PendingWorkItemCount;

        var starved = availableWorkers == 0 && pending > 0;
        if (drift < DriftThreshold && !starved) return false;

        Write(Snapshot(drift, actualInterval, availableWorkers, availableIo, pending, starved), destinationRoot);
        return true;
    }

    private static string Snapshot(
        TimeSpan drift,
        TimeSpan actualInterval,
        int availableWorkers,
        int availableIo,
        long pending,
        bool starved)
    {
        ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIo);
        ThreadPool.GetMinThreads(out var minWorkers, out var minIo);

        var gc = GC.GetGCMemoryInfo();
        using var process = Process.GetCurrentProcess();

        var sb = new StringBuilder();
        sb.Append("--- stall snapshot ").Append(DateTimeOffset.UtcNow.ToString("O")).AppendLine(" ---");
        sb.Append("trigger: ").AppendLine(starved && drift >= DriftThreshold ? "drift AND pool starvation"
            : starved ? "pool starvation" : "scheduling drift");
        sb.Append("requested interval: ").Append(SampleInterval).Append("  actual: ").Append(actualInterval)
          .Append("  drift: ").Append(drift).Append("  (threshold ").Append(DriftThreshold).AppendLine(")");

        sb.Append("threadpool: available workers=").Append(availableWorkers).Append('/').Append(maxWorkers)
          .Append(" (min ").Append(minWorkers).Append("), available io=").Append(availableIo).Append('/').Append(maxIo)
          .Append(" (min ").Append(minIo).AppendLine(")");
        sb.Append("threadpool: threads=").Append(ThreadPool.ThreadCount)
          .Append(" pending=").Append(pending)
          .Append(" completed=").Append(ThreadPool.CompletedWorkItemCount).AppendLine();

        // GC.GetTotalMemory(false) — NOT forced. LL-047's rule is that retention must be measured after a
        // forced full collect, but that rule is for measuring a LEAK. Here the subject is a live stall and
        // a forced blocking collection would perturb the very scheduling being recorded.
        sb.Append("gc: heap=").Append(GC.GetTotalMemory(false))
          .Append(" collections=").Append(GC.CollectionCount(0)).Append('/').Append(GC.CollectionCount(1))
          .Append('/').Append(GC.CollectionCount(2)).AppendLine();
        // PauseTimePercentage is the one figure that speaks to the causal half PE-785 weakened: the step
        // from "this suite retains 2.0-2.5 GB" to "the runner GC-thrashes and the TestServer stalls" was
        // never measured. A high pause percentage here would support it; a low one would refute it.
        sb.Append("gc: pauseTimePercentage=").Append(gc.PauseTimePercentage)
          .Append(" heapSize=").Append(gc.HeapSizeBytes)
          .Append(" memoryLoad=").Append(gc.MemoryLoadBytes)
          .Append(" totalAvailable=").Append(gc.TotalAvailableMemoryBytes).AppendLine();

        sb.Append("process: threads=").Append(process.Threads.Count)
          .Append(" workingSet=").Append(process.WorkingSet64)
          .Append(" privateBytes=").Append(process.PrivateMemorySize64)
          .Append(" cpuSeconds=").Append(process.TotalProcessorTime.TotalSeconds.ToString("F1"))
          .AppendLine();
        sb.Append("host: processors=").Append(Environment.ProcessorCount)
          .Append(" uptime=").Append(Environment.TickCount64 / 1000).AppendLine("s");

        return sb.ToString();
    }

    private static void Write(string snapshot, string? destinationRoot)
    {
        var dir = destinationRoot ?? Directory;
        System.IO.Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "stall-snapshots.txt");

        lock (Gate)
        {
            // Keyed on the FILE, never on a static "have I written it yet" flag. A process-wide flag is
            // wrong twice over: a second destination gets no header at all, and a file recreated after
            // deletion silently loses the paragraph that tells its reader what it cannot prove. The unit
            // tests caught exactly that — the flag survived across tests while the directory did not.
            if (!File.Exists(path)) File.AppendAllText(path, Header);

            File.AppendAllText(path, snapshot);
        }
    }

    // The artefact explains its own limits to whoever downloads it weeks later, because the reader who
    // needs it most is the one who did not write it. DW-097's value was that it said in advance what it
    // could not do and was therefore falsifiable on its first firing.
    private const string Header =
        """
        ACMP stall watchdog — DEF-109 clause (2), built by WBS-27.1 (DEC-120 d3).

        WHAT THIS FILE IS. Snapshots taken by a DEDICATED thread (never the ThreadPool, which the fault
        under investigation would starve) at the moment this process stopped being scheduled promptly, or
        the ThreadPool had work queued with no worker free. Its existence means a stall was OBSERVED.

        WHAT IT IS NOT. It is not a diagnosis and it names no cause on its own. DEF-109's clause (2) asks
        for an artefact that IDENTIFIES A CAUSE; DEC-115 d2 ruled that a successful capture does not
        satisfy that, and DEC-116 d1 ruled that a refuted hypothesis does not either. An elimination is
        not an identification.

        HOW TO READ IT. Large drift with a low gc pauseTimePercentage points AWAY from GC thrash and
        toward scheduling pressure or CPU starvation. Large drift WITH a high pause percentage is the
        first direct evidence for the causal step PE-785 weakened. availableWorkers=0 with a rising
        pending count is thread-pool starvation, which is where occurrence 2's timeline analysis said to
        look next and which has never been measured.

        AN EMPTY OR ABSENT FILE MEANS NO STALL WAS OBSERVED. It does not mean the run was healthy, and it
        is not evidence about DEF-109 in either direction.

        """;
}
