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

    // DEF-134 / DEC-125 d1 — THE TRIGGER THAT IS KEYED ON THE FAULT ITSELF RATHER THAN ON A PROXY.
    //
    // ⛔⛔ WHY THE DRIFT TRIGGER ALONE WAS NOT ENOUGH, MEASURED RATHER THAN ARGUED. DEF-109 occurrence 6
    // fired with this instrument in place and its positive control live (CI 33765425613, backend job
    // 100681936270). Eighteen requests each burned a full 100-second HttpClient ceiling across seventeen
    // classes over 17 minutes, and the artefact recorded 204 samples whose windowMaxDrift was 0.0489907s
    // ONCE and under 3 MILLISECONDS in every one of the other sixteen windows — against a 15-second
    // threshold. THE FAULT LEAVES DRIFT AT ITS HEALTHY VALUE, so no threshold on drift can fire on it,
    // and tightening one only manufactures the false positives DEC-122 d1 deleted a trigger for.
    //
    // ⭐ DEF-109 *IS* "a request did not come back", so this threshold is that, directly. 30 seconds is
    // well above any healthy request in this suite (a whole green run of 432 tests takes ~3 minutes) and
    // well below the 100-second ceiling, so a stall is recorded WHILE it is happening rather than after
    // the request has already given up — the same reasoning the drift threshold was chosen on.
    //
    // ⚠ 30s IS AN ESTIMATE FROM ONE OCCURRENCE'S SHAPE, NOT A MEASURED CEILING, and is written down as
    // such for the same reason DEC-121 d2 said it of the backend job's 40 minutes: the honest form of an
    // unmeasured bound is to say it is unmeasured. What calibrates it is the next occurrence's artefact.
    internal static readonly TimeSpan InFlightThreshold = TimeSpan.FromSeconds(30);

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
                CaptureIfRequestsHung(InFlightRequests.Outstanding(InFlightThreshold));

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
            $"sampleInterval={SampleInterval}, driftThreshold={DriftThreshold}, " +
            $"inFlightThreshold={InFlightThreshold}\n",
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

    /// <summary>
    /// Record a snapshot if any request has been in flight past <see cref="InFlightThreshold"/>. Takes
    /// the outstanding list as a PARAMETER rather than reading the register itself, so a test can inject
    /// the fault's own symptom deterministically — see the remark below. Returns true when a snapshot
    /// was written.
    /// </summary>
    /// <remarks>
    /// ⭐⭐ THE PARAMETER IS THE POINT, AND IT IS LL-055's REMEDY MADE MECHANICAL. The drift control
    /// injects a drift value: the trigger's OWN PREDICATE, which is a tautology with respect to whether
    /// the fault moves that quantity — and `StallWatchdogTests` said so honestly and deferred the
    /// question to "only an occurrence can show that". An occurrence took two days and three PRs to
    /// arrive and answered NO. Here the injectable thing is the FAULT'S SYMPTOM (a request outstanding
    /// past a bound), and the register that produces it is exercised end to end by a real hung request
    /// through real middleware, so the fault-to-trigger path is proven BEFORE this ships rather than by
    /// waiting for occurrence 7.
    /// </remarks>
    internal static bool CaptureIfRequestsHung(
        IReadOnlyList<(string Method, string Path, TimeSpan Age)> outstanding,
        string? destinationRoot = null)
    {
        if (outstanding.Count == 0) return false;

        Write(HungRequestSnapshot(outstanding), destinationRoot);
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

    // DEF-134 / DEC-125 d1. The snapshot for the trigger that CAN fire on DEF-109 — it names WHICH
    // requests were outstanding and for how long, which is the discriminating data six occurrences have
    // never had. With scheduling refuted by occurrence 6, DEC-110 d2's surviving branch is a deadlock,
    // and what tells a deadlock from a slow dependency is whether the hung requests share an endpoint,
    // a module or a verb. A resource figure cannot answer that; this list can.
    //
    // ⛔ IT STILL NAMES NO CAUSE, and DW-097's model requires saying so where it is written rather than
    // only in the header. It narrows.
    private static string HungRequestSnapshot(IReadOnlyList<(string Method, string Path, TimeSpan Age)> outstanding)
    {
        var sb = new StringBuilder();
        sb.Append("--- stall snapshot ").Append(DateTimeOffset.UtcNow.ToString("O")).AppendLine(" ---");
        sb.AppendLine("trigger: request in flight");
        sb.Append("in flight past ").Append(InFlightThreshold).Append(": ").Append(outstanding.Count)
          .Append(" of ").Append(InFlightRequests.LiveCount).AppendLine(" live");

        // Oldest first, and capped: eighteen simultaneous hangs is the observed shape, but a runaway
        // would otherwise write megabytes into an artefact whose value is that a human reads all of it.
        foreach (var (method, path, age) in outstanding.Take(50))
            sb.Append("  hung ").Append(age.TotalSeconds.ToString("F1")).Append("s  ")
              .Append(method).Append(' ').AppendLine(path);
        if (outstanding.Count > 50)
            sb.Append("  ... ").Append(outstanding.Count - 50).AppendLine(" more not listed");

        AppendRuntimeState(sb);
        return sb.ToString();
    }

    // The managed thread/task state DEF-109's clause (2) asks for and WBS-27.1 shipped without, plus the
    // runner figures taken AT the stall rather than after.
    //
    // ⛔⛔ WHAT IT CANNOT DO, STATED HERE AND IN THE HEADER: there are no managed STACKS. .NET Core
    // removed the ability to walk another thread's stack in-process, and the only mechanism that would
    // (a full dump) is the ~1.2 GB artefact DEC-122 d3 measured being DROPPED from DEF-130's capture for
    // exactly that reason. So this reports how many threads exist and what the OS says they are doing —
    // not what managed frame each is in.
    private static void AppendRuntimeState(StringBuilder sb)
    {
        ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIo);
        ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIo);
        ThreadPool.GetMinThreads(out var minWorkers, out var minIo);

        sb.Append("threadpool: available workers=").Append(availableWorkers).Append('/').Append(maxWorkers)
          .Append(" (min ").Append(minWorkers).Append("), available io=").Append(availableIo).Append('/').Append(maxIo)
          .Append(" (min ").Append(minIo).AppendLine(")");
        sb.Append("threadpool: threads=").Append(ThreadPool.ThreadCount)
          .Append(" pending=").Append(ThreadPool.PendingWorkItemCount)
          .Append(" completed=").Append(ThreadPool.CompletedWorkItemCount).AppendLine();

        var gc = GC.GetGCMemoryInfo();
        sb.Append("gc: heap=").Append(GC.GetTotalMemory(false))
          .Append(" collections=").Append(GC.CollectionCount(0)).Append('/').Append(GC.CollectionCount(1))
          .Append('/').Append(GC.CollectionCount(2)).AppendLine();
        sb.Append("gc: pauseTimePercentage=").Append(gc.PauseTimePercentage)
          .Append(" heapSize=").Append(gc.HeapSizeBytes)
          .Append(" memoryLoad=").Append(gc.MemoryLoadBytes)
          .Append(" totalAvailable=").Append(gc.TotalAvailableMemoryBytes).AppendLine();

        using var process = Process.GetCurrentProcess();
        sb.Append("process: threads=").Append(process.Threads.Count)
          .Append(" workingSet=").Append(process.WorkingSet64)
          .Append(" privateBytes=").Append(process.PrivateMemorySize64)
          .Append(" cpuSeconds=").Append(process.TotalProcessorTime.TotalSeconds.ToString("F1"))
          .AppendLine();

        // ⚠ GUARDED, NOT ASSUMED. ProcessThread.ThreadState and WaitReason are Windows-only and throw
        // PlatformNotSupportedException on Linux — where CI runs. A diagnostic that throws on the
        // platform it was built for would be worse than no diagnostic, so the failure is recorded as a
        // line in the artefact rather than swallowed: a reader must be able to tell "no threads were
        // waiting" from "this runtime would not say".
        try
        {
            var states = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (System.Diagnostics.ProcessThread t in process.Threads)
            {
                var key = t.ThreadState == System.Diagnostics.ThreadState.Wait
                    ? $"Wait/{t.WaitReason}"
                    : t.ThreadState.ToString();
                states[key] = states.TryGetValue(key, out var n) ? n + 1 : 1;
            }

            sb.Append("threads by state:");
            foreach (var (state, n) in states) sb.Append(' ').Append(state).Append('=').Append(n);
            sb.AppendLine();
        }
        catch (Exception ex)
        {
            sb.Append("threads by state: UNAVAILABLE (").Append(ex.GetType().Name).AppendLine(")");
        }

        sb.Append("host: processors=").Append(Environment.ProcessorCount)
          .Append(" uptime=").Append(Environment.TickCount64 / 1000).AppendLine("s");
    }

    private static string Snapshot(
        TimeSpan drift,
        TimeSpan actualInterval,
        int availableWorkers,
        int availableIo,
        long pending)
    {
        var sb = new StringBuilder();
        sb.Append("--- stall snapshot ").Append(DateTimeOffset.UtcNow.ToString("O")).AppendLine(" ---");
        sb.AppendLine("trigger: scheduling drift");
        sb.Append("requested interval: ").Append(SampleInterval).Append("  actual: ").Append(actualInterval)
          .Append("  drift: ").Append(drift).Append("  (threshold ").Append(DriftThreshold).AppendLine(")");
        sb.Append("in flight: ").Append(InFlightRequests.LiveCount).AppendLine(" request(s)");

        // The pool figures READ AT THE TRIGGER are kept, because they were sampled a moment earlier than
        // AppendRuntimeState's and on a drift snapshot that gap is the interesting part.
        sb.Append("threadpool at trigger: available workers=").Append(availableWorkers)
          .Append(" available io=").Append(availableIo).Append(" pending=").Append(pending).AppendLine();

        // GC.GetTotalMemory(false) — NOT forced. LL-047's rule is that retention must be measured after a
        // forced full collect, but that rule is for measuring a LEAK. Here the subject is a live stall and
        // a forced blocking collection would perturb the very scheduling being recorded.
        //
        // PauseTimePercentage inside AppendRuntimeState is the one figure that speaks to the causal half
        // PE-785 weakened: the step from "this suite retains 2.0-2.5 GB" to "the runner GC-thrashes and
        // the TestServer stalls" was never measured. A high pause percentage would support it; a low one
        // would refute it.
        AppendRuntimeState(sb);

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
        under investigation would starve) when either of two things is true: a REQUEST HAS BEEN IN FLIGHT
        past its bound, or this process stopped being scheduled promptly. Its existence means a stall was
        OBSERVED.

        ⭐ THE TWO TRIGGERS ARE NOT REDUNDANT AND OCCURRENCE 6 IS WHY BOTH EXIST (DEF-134, DEC-125 d1).
        Drift shipped alone and could not fire on the fault: on occurrence 6, eighteen requests each burned
        a full 100-second ceiling across seventeen classes over 17 minutes while windowMaxDrift was 0.049s
        ONCE and under 3 MILLISECONDS in every other window, against a 15-second threshold. The process was
        scheduled promptly THROUGHOUT the failure, so no threshold on drift could ever have fired.
        "trigger: request in flight" is keyed on DEF-109's own definition instead — a request did not come
        back — so there is no proxy left to assume. Drift is kept because it is what refuted thread-pool
        starvation, and it did so through the heartbeat rather than through a snapshot.

        HOW TO READ A "request in flight" SNAPSHOT. The list is the finding. With scheduling refuted, the
        surviving branch is a deadlock inside the pipeline, and what tells that from a slow dependency is
        whether the hung requests SHARE something — one endpoint, one module, one verb — or scatter across
        unrelated ones. Compare the paths first; the resource figures below them are context, not evidence.

        ⛔ AND READ AN EMPTY REGISTER AS A FINDING TOO. If requests are timing out while this file shows
        nothing in flight, the stall is UPSTREAM of the server pipeline — in the transport or the client —
        which no server-side timer could otherwise distinguish from a healthy run.

        ⛔⛔ THERE ARE NO MANAGED STACKS HERE, AND THAT IS A LIMIT RATHER THAN AN OVERSIGHT. .NET Core
        removed walking another thread's stack in-process, and the only mechanism that would — a full
        dump — is the ~1.2 GB artefact DEC-122 d3 measured being DROPPED from DEF-130's capture for exactly
        that reason. "threads by state" is what the OS will say instead: how many threads exist and what
        they are waiting on, never which managed frame each is in. On Linux, where CI runs, even that is
        Windows-only and the line will read UNAVAILABLE — recorded rather than omitted, so a reader can
        tell "no threads were waiting" from "this runtime would not say".

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
            ⚠ AND SINCE DEF-134 THIS ABSENCE SAYS MORE THAN IT USED TO. It now means no request was in
            flight past its bound EITHER — so on a run whose tests failed at the 100-second ceiling, an
            empty file says the hang was not visible to the server pipeline at all, which points upstream
            of it. Before DEF-134 the same silence was compatible with the fault simply being invisible
            to the only trigger there was, and occurrence 6 proved that is what it had been.
          · "heartbeat" LINES -> the thread stayed alive, and their maxDrift / maxPending / maxThreads
            are the data a threshold should be calibrated FROM. If the heartbeats stop before the job
            does, the thread died there.

        NONE OF IT IS A DIAGNOSIS. DEF-109's clause (2) asks for an artefact that IDENTIFIES A CAUSE;
        DEC-115 d2 ruled that a successful capture does not satisfy that, and DEC-116 d1 that a refuted
        hypothesis does not either.

        """;
}
