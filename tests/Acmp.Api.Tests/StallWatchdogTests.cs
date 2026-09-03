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

    // The sustained-starvation gate keeps one bit of static state, so without this reset each test would
    // inherit whatever the previous one left — the same cross-contamination that put a live-state
    // classification into an assertion and turned main red. ⭐ It also makes the two "must not fire"
    // controls DETERMINISTIC rather than lucky: after a reset a single sample can never satisfy the
    // sustained rule, so those tests hold whether or not the runner's pool happens to be starved.
    public StallWatchdogTests() => StallWatchdog.ResetSustainedState();

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
        flat.Should().Contain("AN EMPTY OR ABSENT FILE MEANS NO STALL WAS OBSERVED");
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

    // ⛔ THE PREDICATE THESE PIN REPLACED ONE THAT COULD NOT FIRE. The first version asked for
    // availableWorkers == 0; GetAvailableThreads counts against the pool MAXIMUM, which is 32767, so it
    // needed 32,767 concurrent work items and was dead code — a second trigger that looked like a working
    // one and whose silence would have read as "the pool was fine" rather than "nothing ever asked".
    // That is DEF-078's shape inside the very instrument built to escape it, which is why the replacement
    // is a PURE function: pool state cannot be forced from a test, and a predicate that cannot be
    // exercised is exactly what went wrong the first time.
    [Theory]
    // Work queued while the pool is at or past its floor: further threads now arrive only on the
    // runtime's slow injection schedule, so queued work waits. That is starvation.
    [InlineData(1L, 24, 24, true)]
    [InlineData(500L, 40, 24, true)]
    // Nothing queued — a large pool is not evidence of anything.
    [InlineData(0L, 40, 24, false)]
    // Queued work while the pool is still BELOW its floor is normal: the runtime injects those threads
    // immediately and without throttling, so the queue drains on its own.
    [InlineData(5L, 10, 24, false)]
    public void The_pool_starvation_signature_is_queued_work_against_a_pool_at_its_floor(
        long pending, int threadCount, int minWorkers, bool expected) =>
        StallWatchdog.IsPoolStarved(pending, threadCount, minWorkers).Should().Be(expected);

    [Fact]
    public void One_starved_sample_is_not_enough_however_starved_the_pool_actually_is()
    {
        // The regression this pins, and the instrument found it about ITSELF on its first CI run: the
        // instantaneous signature fired during an ORDINARY suite on a 4-core runner, because `pending > 0`
        // at the instant of sampling is routine. A trigger that fires on every run is exactly as useless
        // as one that never fires -- it fills the artefact with noise and teaches its reader to ignore it.
        //
        // Deterministic without being able to control the pool: after the constructor's reset, the
        // sustained rule cannot be satisfied by a single sample no matter what the real pool state is.
        // So this holds on an idle 24-core box and on a saturated runner alike.
        StallWatchdog.CaptureIfDegraded(StallWatchdog.SampleInterval, _dir).Should().BeFalse();
        File.Exists(SnapshotFile).Should().BeFalse();
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_dir)) System.IO.Directory.Delete(_dir, recursive: true);
    }
}
