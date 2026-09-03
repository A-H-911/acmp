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

    // Whether the PREVIOUS sample saw the pool-starvation signature. Only the sampling thread writes it,
    // and the tests drive CaptureIfDegraded directly, so no synchronisation is needed. Reset exists so a
    // test can establish a known starting point rather than inheriting whatever the last one left.
    private static bool _previousSampleStarved;

    internal static void ResetSustainedState() => _previousSampleStarved = false;

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

                // ⭐⭐ THE HEARTBEAT CARRIES THE CALIBRATION PAYLOAD, WHICH IS THE POINT OF ITS CONTENT
                // RATHER THAN A BONUS. `DEC-121` d3 ruled the ORDER: the control first, the threshold
                // calibrated only AFTERWARDS from real data. These maxima ARE that data — the next
                // occurrence says what drift and queue depth actually look like on a loaded runner, so
                // the threshold is chosen from measurement instead of from the guessing that has already
                // made this predicate wrong twice (`LL-054`).
                if (samples % HeartbeatEvery == 0)
                    Write($"heartbeat: samples={samples} elapsed={stopwatch.Elapsed:hh\\:mm\\:ss} " +
                          $"maxDrift={maxDrift} maxPending={maxPending} maxThreads={maxThreads}\n", null);
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
        ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIo);
        ThreadPool.GetMinThreads(out var minWorkers, out _);
        var pending = ThreadPool.PendingWorkItemCount;

        // ⛔⛔ SUSTAINED, NOT INSTANTANEOUS, AND THE INSTRUMENT ITSELF IS WHY. On its first CI run the
        // instantaneous signature fired during an ORDINARY suite on a 4-core runner — `pending > 0` at the
        // instant of sampling is routine, because any queued work item counts. A trigger that fires on
        // every run is exactly as useless as one that never fires: it fills the artefact with noise and
        // teaches its reader to ignore it, which is the failure the `if: failure()` upload guard exists to
        // prevent. Requiring the condition on TWO CONSECUTIVE samples separates transient queuing, which
        // drains in milliseconds, from starvation, which persists — and it is a principle rather than a
        // magic number, which matters because nobody has measured what CI's normal pending depth is.
        var starvedNow = IsPoolStarved(pending, ThreadPool.ThreadCount, minWorkers);
        var starved = starvedNow && _previousSampleStarved;
        _previousSampleStarved = starvedNow;

        if (drift < DriftThreshold && !starved) return false;

        Write(Snapshot(drift, actualInterval, availableWorkers, availableIo, pending, starved), destinationRoot);
        return true;
    }

    /// <summary>
    /// The ThreadPool-starvation signature: work is QUEUED while the pool has already grown to or past
    /// its minimum, so further threads arrive only on the runtime's slow injection schedule and queued
    /// work waits. Pure, and internal, so a test can inject the condition directly.
    /// </summary>
    /// <remarks>
    /// ⛔⛔ THIS REPLACES A PREDICATE THAT COULD NOT FIRE, WHICH IS THE EXACT FAULT THIS WHOLE INSTRUMENT
    /// EXISTS TO ESCAPE. The first version asked for <c>availableWorkers == 0</c>. `GetAvailableThreads`
    /// counts against the pool MAXIMUM, which is 32767 here — so that condition needed 32,767 concurrent
    /// work items and was dead code. It would have sat in the file looking like a working second trigger,
    /// and its silence would have read as "the pool was fine" rather than as "nothing ever asked".
    /// ⚠ AND DRIFT DOES NOT COVER IT. This sampler is a dedicated OS thread in Thread.Sleep; the kernel
    /// schedules it on time whether or not the ThreadPool has a free worker. Drift catches CPU
    /// saturation, a blocking GC, or the VM being descheduled — it is silent on pool starvation alone,
    /// which is precisely where occurrence 2's timeline analysis said to look next. The two triggers are
    /// complementary, not redundant, and neither is sufficient.
    /// </remarks>
    internal static bool IsPoolStarved(long pending, int threadCount, int minWorkers) =>
        pending > 0 && threadCount >= minWorkers;

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
        first direct evidence for the causal step PE-785 weakened.

        A "pool starvation" trigger means work was QUEUED while the pool had already grown to or past its
        minimum, so further threads arrive only on the runtime's slow injection schedule. That is where
        occurrence 2's timeline analysis said to look next and it has never been measured. Note that the
        two triggers are complementary and neither is sufficient: this sampler is a dedicated OS thread,
        so the kernel wakes it on time whether or not the pool has a free worker — drift alone is SILENT
        on pool starvation, and pool starvation alone produces no drift.

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
