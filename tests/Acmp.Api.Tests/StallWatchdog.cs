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

        // ⛔⛔ THE POSITIVE CONTROL (`DEF-131`, `DEC-121` d3). Written UNCONDITIONALLY, before the thread
        // starts, so the file's EXISTENCE proves the watchdog ran and its CONTENT distinguishes "ran and
        // saw nothing" from "never ran". On `DEF-109` occurrence 5 both upload steps executed and found
        // NO FILES during a 24-minute run in which 37 requests each burned a 100-second ceiling — and
        // that silence was unreadable, because an empty directory is produced identically by both cases.
        // `DEF-078`'s green control with no subject, inside the instrument built to escape that family.
        // ⚠ It costs nothing on a green run: the upload is `if: failure()`, so a passing build uploads
        // nothing whatever this writes. On a FAILING run, knowing the watchdog was alive is the point.
        // ⭐ This project already required a positive control four times — `DW-068`, `DW-084`, `AV-213`,
        // `AV-216` ("CONFIRMED LIVE, not only by source reading") — and recorded it in no rule register.
        WriteStartupRecord();

        var thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "acmp-stall-watchdog",
            // A best-effort hint and NOT a guarantee, stated here so a reader of the artefact does not
            // lean on it: the runtime documents that "operating systems are not required to honor the
            // priority of a thread", and on Linux — where CI runs — it is typically ignored outright.
            // (It cannot throw: Thread.Priority raises only on an invalid value or a dead thread;
            // the Linux-throwing member is ProcessThread.BasePriority, a different type.) So a large
            // drift does NOT by itself distinguish "the process was not scheduled" from "this thread
            // lost an ordinary race for a core" — the snapshot records CPU seconds, processor count and
            // pool state so a reader can tell those apart from the evidence rather than from this hint.
            Priority = ThreadPriority.AboveNormal,
        };
        thread.Start();
    }

    /// <summary>
    /// One heartbeat per this many samples. At a 5-second interval that is roughly one line per minute,
    /// so a 24-minute occurrence leaves ~24 lines — enough to prove the thread stayed alive and to show
    /// WHEN it stopped if it died, and far too few to be noise.
    /// </summary>
    private const int HeartbeatEvery = 12;

    private static void Loop()
    {
        var stopwatch = Stopwatch.StartNew();
        var samples = 0L;
        var maxDrift = TimeSpan.Zero;
        long maxPending = 0;
        var maxThreads = 0;

        while (true)
        {
            var before = stopwatch.Elapsed;
            Thread.Sleep(SampleInterval);
            var actual = stopwatch.Elapsed - before;

            try
            {
                samples++;
                var drift = actual - SampleInterval;
                if (drift > maxDrift) maxDrift = drift;
                if (ThreadPool.PendingWorkItemCount > maxPending) maxPending = ThreadPool.PendingWorkItemCount;
                if (ThreadPool.ThreadCount > maxThreads) maxThreads = ThreadPool.ThreadCount;

                CaptureIfDegraded(actual);

                // ⚠ The maxima are WINDOWED, not cumulative — DEC-122 d1. Cumulative maxima collapse a
                // whole run into one number that only ever rises, so they cannot say WHEN the queue grew;
                // per-window they are a time series, and during a stall the minute-by-minute profile is
                // the finding. This is also the only place pool state is now recorded at all, the
                // starvation TRIGGER having been deleted for firing on healthy runs.

                // ⭐⭐ THE HEARTBEAT CARRIES THE CALIBRATION PAYLOAD, WHICH IS THE POINT OF ITS CONTENT
                // RATHER THAN A BONUS. `DEC-121` d3 ruled the ORDER: the control first, the threshold
                // calibrated only AFTERWARDS from real data. These maxima ARE that data — the next
                // occurrence says what drift and queue depth actually look like on a loaded runner, so
                // the threshold is chosen from measurement instead of from the guessing that has already
                // made this predicate wrong twice (`LL-054`).
                if (samples % HeartbeatEvery == 0)
                {
                    Write($"heartbeat: samples={samples} elapsed={stopwatch.Elapsed:hh\\:mm\\:ss} " +
                          $"windowMaxDrift={maxDrift} windowMaxPending={maxPending} windowMaxThreads={maxThreads}\n", null);
                    maxDrift = TimeSpan.Zero;
                    maxPending = 0;
                    maxThreads = 0;
                }
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
    /// Write the header and a startup line. Internal so a test can prove the control actually appears —
    /// a positive control nobody exercised is the fault it exists to prevent.
    /// </summary>
    internal static void WriteStartupRecord(string? destinationRoot = null)
    {
        ThreadPool.GetMinThreads(out var minWorkers, out _);
        ThreadPool.GetMaxThreads(out var maxWorkers, out _);
        Write(
            $"watchdog STARTED {DateTimeOffset.UtcNow:O} — pid {Environment.ProcessId}, " +
            $"processors={Environment.ProcessorCount}, pool min/max workers={minWorkers}/{maxWorkers}, " +
            $"sampleInterval={SampleInterval}, driftThreshold={DriftThreshold}\n",
            destinationRoot);
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
        if (drift < DriftThreshold) return false;

        ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIo);
        Write(Snapshot(drift, actualInterval, availableWorkers, availableIo, ThreadPool.PendingWorkItemCount), destinationRoot);
        return true;
    }

    // ⛔⛔ THERE WAS A SECOND, POOL-STARVATION TRIGGER HERE AND IT IS DELETED, NOT RETUNED — DEC-122 d1.
    // It was wrong THREE times on the same instrument (LL-054), and the third time is the one worth
    // keeping in view because no local run could have shown it:
    //   1. `availableWorkers == 0` — GetAvailableThreads counts against the pool MAXIMUM (32767), so it
    //      needed 32,767 concurrent work items. Dead code, caught in review.
    //   2. `pending > 0 && threadCount >= minWorkers` — fired on its FIRST CI run during an ordinary
    //      passing suite, and turned main red.
    //   3. The same condition SUSTAINED across two consecutive samples — still fired six times in six
    //      minutes of a healthy run. ⭐ THE MEASURED CAUSE: `min workers` tracks processor count, so it
    //      is 4 on the runner against 24 on a development box. `threadCount >= minWorkers` is therefore
    //      trivially true there and the predicate DEGENERATES into `pending > 0`. The environment
    //      changed what the code meant, which is why a calibration on a development box proves the
    //      mechanism and never the deployment.
    // ⭐ NOTHING IS LOST. The heartbeat below records maxPending and maxThreads per window, so a real
    // starvation would show the queue climbing minute by minute — as DATA rather than as a trigger, and
    // with no noise. Measured on a healthy run: maxPending reached 167, so no threshold below that is
    // defensible, and n=1 is not a distribution. When enough windows have accumulated to BE one, a
    // threshold can be chosen from it; until then there is nothing honest to choose.

    private static string Snapshot(
        TimeSpan drift,
        TimeSpan actualInterval,
        int availableWorkers,
        int availableIo,
        long pending)
    {
        ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIo);
        ThreadPool.GetMinThreads(out var minWorkers, out var minIo);

        var gc = GC.GetGCMemoryInfo();
        using var process = Process.GetCurrentProcess();

        var sb = new StringBuilder();
        sb.Append("--- stall snapshot ").Append(DateTimeOffset.UtcNow.ToString("O")).AppendLine(" ---");
        sb.AppendLine("trigger: scheduling drift");
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
        first direct evidence for the causal step PE-785 weakened.

        POOL STATE IS DATA HERE, NOT A TRIGGER, AND THAT IS DELIBERATE (DEC-122 d1). A starvation trigger
        was tried three times and fired on healthy runs twice, because `min workers` tracks processor
        count — 4 on the runner against 24 on a development box — so its condition degenerated there.
        The heartbeat's windowMaxPending / windowMaxThreads carry the same information WITHOUT asserting
        anything: real starvation shows as the queue climbing across consecutive windows. Measured on a
        healthy run, windowMaxPending reached 167, so treat single-window spikes as normal.

        NOTE WHAT DRIFT CANNOT SEE. This sampler is a dedicated OS thread, so the kernel wakes it on time
        whether or not the ThreadPool has a free worker. Drift catches CPU saturation, a blocking GC, or
        the VM being descheduled; pool starvation ALONE produces no drift and will appear only in the
        heartbeat numbers. An absence of snapshots is therefore not an absence of pool pressure.

        HOW TO READ AN ABSENCE — and this file exists BECAUSE that was got wrong once (DEF-131). On
        DEF-109 occurrence 5 the upload step ran and found no files at all, and that silence could not be
        read: an empty directory is produced identically by "the watchdog ran and saw nothing" and by
        "the watchdog never ran". So:

          · NO FILE AT ALL  -> the watchdog never started. Say nothing about DEF-109 from this; it is a
            fact about the instrument, and it is now a reportable defect in its own right.
          · A FILE WITH A "watchdog STARTED" LINE AND NO SNAPSHOT -> the watchdog ran and observed no
            stall by its current thresholds. That IS a finding, and it is the one this instrument exists
            to be able to make. It still does not mean the run was healthy.
          · "heartbeat" LINES -> the thread stayed alive, and their maxDrift / maxPending / maxThreads
            are the data a threshold should be calibrated FROM. If the heartbeats stop before the job
            does, the thread died there.

        NONE OF IT IS A DIAGNOSIS. DEF-109's clause (2) asks for an artefact that IDENTIFIES A CAUSE;
        DEC-115 d2 ruled that a successful capture does not satisfy that, and DEC-116 d1 that a refuted
        hypothesis does not either.

        """;
}
