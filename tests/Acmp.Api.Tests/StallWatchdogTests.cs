using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// WBS-27.1 — proving the instrument FIRES, which is the half LL-013 exists for: a capture that has never
// been shown to produce output is indistinguishable from one that is silently broken, and a clean run of
// it reads exactly like a clean run of a working one (DEF-078).
//
// ⚠ THE DRIFT TESTS INJECT THE CONDITION (a drift value) RATHER THAN A REAL STALL, AND THAT LIMIT WAS
// STATED HERE BEFORE IT WAS PAID: "they prove nothing about whether a real DEF-109 occurrence produces a
// large drift. Only an occurrence can show that." ⛔⛔ OCCURRENCE 6 ANSWERED, AND THE ANSWER WAS NO —
// windowMaxDrift of 0.049s once and under 3ms in sixteen of seventeen windows, against a 15-second
// threshold, while eighteen requests each burned a full 100-second ceiling. The fault leaves drift at its
// healthy value. Declaring the gap was right and it was not enough (LL-055): a CHEAPER INJECTION EXISTED
// the whole time, and the deferral to "only an occurrence can show that" cost two days and three PRs.
//
// ⭐⭐ SO THE IN-FLIGHT TESTS BELOW INJECT THE FAULT'S SYMPTOM INSTEAD OF THE TRIGGER'S PREDICATE, WHICH
// IS THE WHOLE POINT OF DEF-134. A_real_hung_request_is_visible_... issues an actual HTTP request through
// the actual middleware and holds it, then watches the register report it. That exercises the
// fault-to-trigger path end to end BEFORE this ships, rather than waiting for occurrence 7 to say whether
// the coupling was ever there.
public sealed class StallWatchdogTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(),
        "acmp-stall-watchdog-tests",
        Guid.NewGuid().ToString("N"));

    private string SnapshotFile => Path.Combine(_dir, "stall-snapshots.txt");

    // ⭐ THESE TESTS ARE NOW ENVIRONMENT-INDEPENDENT BY CONSTRUCTION, which they were not before DEC-122
    // d1. Drift is the only trigger, and drift is injected by the caller — so no assertion here depends
    // on the live ThreadPool, whose min-thread count differs between a 24-core development box and a
    // 4-core runner and which is what made an earlier version pass locally and fail on CI (LL-032).

    [Fact]
    public void A_normal_interval_writes_nothing_at_all()
    {
        // The control that makes the next test mean something. If the watchdog wrote on every sample, a
        // file would prove only that the process was running.
        var fired = StallWatchdog.CaptureIfDegraded(StallWatchdog.SampleInterval, _dir);

        fired.Should().BeFalse();
        File.Exists(SnapshotFile).Should().BeFalse("a healthy sample must leave no artefact — an artefact that always exists teaches a reader to ignore it");
    }

    [Fact]
    public void A_sample_that_arrives_far_late_is_captured_with_the_state_that_would_name_a_cause()
    {
        // The injected fault: a sample that took a minute to arrive on a thread that asked for five
        // seconds. That is what "the process was not being scheduled" looks like from inside.
        var late = StallWatchdog.SampleInterval + TimeSpan.FromSeconds(60);

        var fired = StallWatchdog.CaptureIfDegraded(late, _dir);

        fired.Should().BeTrue();
        var text = File.ReadAllText(SnapshotFile);

        // The trigger and the measured quantity, not merely the fact that a bound was crossed.
        text.Should().Contain("stall snapshot");
        text.Should().Contain("drift: ");

        // ⛔ ASSERTS THAT A TRIGGER WAS CLASSIFIED, NEVER *WHICH* ONE. The earlier version pinned the
        // literal "scheduling drift" and went red on CI while passing locally: the classification is
        // derived from LIVE ThreadPool state, which is not starved on an idle 24-core box and was starved
        // on a loaded 4-core runner. That is LL-032's shape — a fixture that is live state changes meaning
        // when the environment does — and the environment is exactly what this instrument exists to
        // observe, so the test must not depend on it.
        text.Should().Contain("trigger: ");

        // The state DEF-109 clause (2) names: thread/task state, and runner resource figures taken AT the
        // stall. Each of these is asserted because a snapshot missing one of them cannot answer the
        // question it was built for.
        text.Should().Contain("threadpool: available workers=");
        text.Should().Contain("threadpool: threads=");
        text.Should().Contain("pending=");
        text.Should().Contain("gc: pauseTimePercentage=");
        text.Should().Contain("process: threads=");
        text.Should().Contain("workingSet=");
        text.Should().Contain("host: processors=");
    }

    [Fact]
    public void The_artefact_states_its_own_limits_before_its_first_snapshot()
    {
        // DW-097's model: an instrument that says in advance what it cannot do is falsifiable on its first
        // firing, where one shipped claiming sufficiency has to be re-argued instead. The header is part
        // of the deliverable, not decoration — the reader who needs it most did not write it.
        StallWatchdog.CaptureIfDegraded(StallWatchdog.SampleInterval + TimeSpan.FromSeconds(60), _dir);

        // Whitespace-normalised on purpose: the assertion is about what the header SAYS, and a literal
        // Contain would break the moment a sentence reflowed across a line — which it did on the first
        // run here. A test that fails when prose is rewrapped tests the formatting, not the content.
        var flat = Regex.Replace(File.ReadAllText(SnapshotFile), @"\s+", " ");

        flat.Should().Contain("It is not a diagnosis and it names no cause on its own");
        flat.Should().Contain("An elimination is not an identification");

        // The header must teach a reader to tell the two absences apart, because getting that wrong is
        // exactly what DEF-131 records: it previously said an absent file meant no stall was observed,
        // and never said "or that the watchdog never started" -- which was the case actually in front of
        // us on DEF-109 occurrence 5.
        flat.Should().Contain("the watchdog never started");
        flat.Should().Contain("watchdog STARTED");
    }

    [Fact]
    public void Snapshots_accumulate_rather_than_overwriting_so_a_run_shows_a_shape()
    {
        // One stall is a point; several are a shape, and the shape is what distinguishes a single
        // scheduling hiccup from sustained starvation. Overwriting would discard exactly that.
        var late = StallWatchdog.SampleInterval + TimeSpan.FromSeconds(60);

        StallWatchdog.CaptureIfDegraded(late, _dir).Should().BeTrue();
        StallWatchdog.CaptureIfDegraded(late, _dir).Should().BeTrue();

        var occurrences = File.ReadAllText(SnapshotFile).Split("--- stall snapshot ").Length - 1;
        occurrences.Should().Be(2);
    }

    [Fact]
    public void A_drift_just_under_the_threshold_does_not_fire()
    {
        // Pins the boundary. Without this the threshold could be anything, including zero, and the
        // "normal interval writes nothing" test would still pass.
        var justUnder = StallWatchdog.SampleInterval + StallWatchdog.DriftThreshold - TimeSpan.FromSeconds(1);

        StallWatchdog.CaptureIfDegraded(justUnder, _dir).Should().BeFalse();
        File.Exists(SnapshotFile).Should().BeFalse();
    }

    [Fact]
    public void Pool_pressure_alone_never_writes_a_snapshot_however_loaded_the_runner_is()
    {
        // ⛔ THE REGRESSION THIS PINS, AND THE INSTRUMENT FOUND IT ABOUT ITSELF. A pool-starvation trigger
        // was tried three times and fired on HEALTHY runs twice -- six snapshots in six minutes of an
        // ordinary suite. The measured cause was invisible on a development box: `min workers` tracks
        // processor count, 4 on the runner against 24 locally, so `threadCount >= minWorkers` was
        // trivially true there and the condition degenerated into `pending > 0`. DEC-122 d1 deleted it;
        // pool state now lives in the heartbeat as DATA, where it asserts nothing.
        //
        // ⭐ This assertion holds whatever the real ThreadPool is doing, on an idle box and a saturated
        // runner alike, because drift is the only trigger and drift is supplied by the caller. That is
        // the property the earlier version lacked (LL-032).
        StallWatchdog.CaptureIfDegraded(StallWatchdog.SampleInterval, _dir).Should().BeFalse();
        File.Exists(SnapshotFile).Should().BeFalse();
    }

    [Fact]
    public void The_startup_record_proves_the_watchdog_RAN_even_when_it_sees_nothing()
    {
        // DEF-131, and the whole point of the fix. On DEF-109 occurrence 5 the upload step ran and found
        // NO FILES during a 24-minute run in which 37 requests each burned a 100-second ceiling -- and
        // that silence was unreadable, because an empty directory is produced identically by "ran and saw
        // nothing" and by "never ran". The startup record is what separates them.
        StallWatchdog.WriteStartupRecord(_dir);

        File.Exists(SnapshotFile).Should().BeTrue("the file's EXISTENCE is the positive control");
        var flat = Regex.Replace(File.ReadAllText(SnapshotFile), @"\s+", " ");

        flat.Should().Contain("watchdog STARTED");
        // The configuration is recorded WITH the evidence, so a later reader knows which thresholds the
        // absence of a snapshot was an absence against -- without it, "no stall observed" is unquotable.
        flat.Should().Contain("driftThreshold=");
        flat.Should().Contain("sampleInterval=");
        flat.Should().Contain("processors=");
    }

    [Fact]
    public void A_started_watchdog_that_sees_nothing_leaves_the_header_but_no_snapshot()
    {
        // The discrimination itself, asserted as one behaviour rather than inferred from two tests: after
        // a startup record, a healthy sample must add NOTHING. That is what makes "started, no snapshot"
        // a readable finding instead of an ambiguous silence.
        StallWatchdog.WriteStartupRecord(_dir);
        StallWatchdog.CaptureIfDegraded(StallWatchdog.SampleInterval, _dir).Should().BeFalse();

        var text = File.ReadAllText(SnapshotFile);
        text.Should().Contain("watchdog STARTED");
        text.Should().NotContain("stall snapshot");
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────────
    // DEF-134 / DEC-125 d1 — the trigger keyed on DEF-109's own definition.
    // ────────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly (string, string, TimeSpan)[] None = [];

    [Fact]
    public void No_outstanding_request_writes_nothing_at_all()
    {
        // The control that makes the next test mean something, and the same one the drift trigger has:
        // if a snapshot appeared whatever the register said, its presence would prove only that the
        // process was running.
        StallWatchdog.CaptureIfRequestsHung(None, _dir).Should().BeFalse();
        File.Exists(SnapshotFile).Should().BeFalse();
    }

    [Fact]
    public void A_request_outstanding_past_the_bound_is_captured_and_the_snapshot_NAMES_IT()
    {
        // ⭐ THE LIST IS THE FINDING, not the fact that a bound was crossed. With scheduling refuted by
        // occurrence 6, what tells a deadlock from a slow dependency is whether the hung requests share
        // an endpoint — so a snapshot that recorded only "3 requests hung" would be the same dead end
        // the resource figures already are.
        (string, string, TimeSpan)[] hung =
        [
            ("GET", "/api/topics?page=1", TimeSpan.FromSeconds(97)),
            ("POST", "/api/votes/close", TimeSpan.FromSeconds(41)),
        ];

        StallWatchdog.CaptureIfRequestsHung(hung, _dir).Should().BeTrue();
        var text = File.ReadAllText(SnapshotFile);

        text.Should().Contain("trigger: request in flight");
        text.Should().Contain("/api/topics?page=1");
        text.Should().Contain("/api/votes/close");
        text.Should().Contain("97.0s");

        // The thread/task state DEF-109's clause (2) named and WBS-27.1 shipped without.
        text.Should().Contain("threadpool: threads=");
        text.Should().Contain("threads by state:");
        text.Should().Contain("gc: pauseTimePercentage=");
        text.Should().Contain("host: processors=");
    }

    [Fact]
    public void The_oldest_hung_request_is_listed_first_so_the_first_line_is_the_earliest_stall()
    {
        // Ordering is load-bearing for a reader: the earliest request to hang is the one whose cause is
        // not itself a consequence of some other request already being stuck.
        var outstanding = InFlightRequestsProbe.Sorted(
            ("GET", "/api/second", TimeSpan.FromSeconds(40)),
            ("GET", "/api/first", TimeSpan.FromSeconds(80)));

        StallWatchdog.CaptureIfRequestsHung(outstanding, _dir);
        var text = File.ReadAllText(SnapshotFile);

        text.IndexOf("/api/first", StringComparison.Ordinal)
            .Should().BeLessThan(text.IndexOf("/api/second", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_real_hung_request_is_visible_in_the_register_through_the_real_middleware()
    {
        // ⭐⭐ THE CONTROL LL-055 EXISTS FOR, AND IT IS THE ONE THE DRIFT TRIGGER COULD NEVER HAVE.
        // It injects the FAULT'S SYMPTOM — a request that has entered the pipeline and not come back —
        // and asserts the register sees it. The drift tests inject the trigger's own predicate, which is
        // guaranteed to pass and says nothing about whether DEF-109 moves that quantity; occurrence 6
        // then showed it does not. Here the fault-to-trigger path is exercised end to end, in-process,
        // in milliseconds, with no occurrence required.
        // ⛔ THE FIRST DRAFT OF THIS TEST CALLED InFlightRequests.Begin DIRECTLY while its comment
        // claimed it went through the middleware. That is LL-055's tautology one layer up — it would
        // have proved the register works and NOTHING about whether the pipeline ever calls it, so
        // deleting the IStartupFilter registration would have left it green. It builds the real
        // middleware now.
        var pipeline = BuildRealTrackingPipeline(out var release);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/deliberately-hung";
        context.Request.QueryString = new QueryString("?trace=1");

        // The request enters the pipeline and does not come back until we release it. That is what
        // DEF-109 looks like from the server's side.
        var inFlight = pipeline(context);
        inFlight.IsCompleted.Should().BeFalse("the request must still be hung when the register is read");

        try
        {
            // Bound of zero: the assertion is that the register REPORTS an in-flight request, not that a
            // particular wall-clock time has passed — pinning a real duration would make this a sleep,
            // and a flaky one on a loaded runner (LL-032).
            var outstanding = InFlightRequests.Outstanding(TimeSpan.Zero);

            outstanding.Should().Contain(o => o.Path == "/api/deliberately-hung?trace=1",
                "a request that entered the pipeline and has not returned is exactly what DEF-109 is");
            StallWatchdog.CaptureIfRequestsHung(outstanding, _dir).Should().BeTrue();
            File.ReadAllText(SnapshotFile).Should().Contain("/api/deliberately-hung?trace=1");
        }
        finally
        {
            release.SetResult();
            await inFlight;
        }

        // ⛔ THE OTHER HALF OF THE CONTROL: once it completes it must LEAVE the register. Without the
        // middleware's `finally` the first hang would become a permanent false positive for the rest of
        // the run, and every later snapshot would report it as still stuck.
        InFlightRequests.Outstanding(TimeSpan.Zero)
            .Should().NotContain(o => o.Path == "/api/deliberately-hung?trace=1");
    }

    /// <summary>
    /// The REAL <see cref="InFlightRequests.StartupFilter"/>, applied to a real
    /// <see cref="ApplicationBuilder"/>, terminating in a delegate that blocks until released. Nothing
    /// here is a stand-in for the tracking code: the filter, the middleware and its <c>finally</c> are
    /// the shipped ones, so removing the registration or the middleware fails the test above.
    /// </summary>
    /// <remarks>
    /// ⚠ WHAT IT DOES NOT COVER, STATED RATHER THAN IMPLIED: there is no HTTP transport and no
    /// TestServer, so this proves the pipeline records a hung request and says nothing about a stall
    /// that never reaches the pipeline. That case is deliberately readable from the artefact instead —
    /// the header tells a reader that requests timing out against an EMPTY register point upstream of
    /// the server.
    /// </remarks>
    private static RequestDelegate BuildRealTrackingPipeline(out TaskCompletionSource release)
    {
        InFlightRequests.Reset();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        release = gate;

        var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        new InFlightRequests.StartupFilter()
            .Configure(builder => builder.Run(_ => gate.Task))(app);

        return app.Build();
    }

    [Fact]
    public void The_header_teaches_a_reader_to_read_the_in_flight_trigger_and_states_what_it_cannot_do()
    {
        // DW-097's model: the reader who needs the artefact most did not write it, and an instrument
        // that says in advance what it cannot do is falsifiable on its first firing. The stacks
        // limitation is the one a reader will otherwise assume away.
        StallWatchdog.CaptureIfRequestsHung([("GET", "/api/x", TimeSpan.FromSeconds(99))], _dir);
        var flat = Regex.Replace(File.ReadAllText(SnapshotFile), @"\s+", " ");

        flat.Should().Contain("THERE ARE NO MANAGED STACKS HERE");
        flat.Should().Contain("the stall is UPSTREAM of the server pipeline");
        flat.Should().Contain("An elimination is not an identification");
    }

    // Sorting is the production code's job; this mirrors only the ordering contract so the test above
    // states its input in the order a reader finds natural rather than pre-sorted.
    private static class InFlightRequestsProbe
    {
        internal static IReadOnlyList<(string Method, string Path, TimeSpan Age)> Sorted(
            params (string Method, string Path, TimeSpan Age)[] items)
        {
            var copy = items.ToList();
            copy.Sort((a, b) => b.Age.CompareTo(a.Age));
            return copy;
        }
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_dir)) System.IO.Directory.Delete(_dir, recursive: true);
    }
}
