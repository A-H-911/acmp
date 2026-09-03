using System.Text.RegularExpressions;
using FluentAssertions;

namespace Acmp.Api.Tests;

// WBS-27.1 — proving the instrument FIRES, which is the half LL-013 exists for: a capture that has never
// been shown to produce output is indistinguishable from one that is silently broken, and a clean run of
// it reads exactly like a clean run of a working one (DEF-078).
//
// ⚠ These tests inject the CONDITION (a drift value) rather than a real stall. That is deliberate and its
// limit is stated: they prove the trigger, the threshold and the file contents, and they prove nothing
// about whether a real DEF-109 occurrence produces a large drift. Only an occurrence can show that, which
// is exactly what clause (2) waits on.
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

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_dir)) System.IO.Directory.Delete(_dir, recursive: true);
    }
}
