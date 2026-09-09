using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Integration.Tests;

/*
 * DEC-160 g1 — NFR-002, NFR-003, NFR-004 and NFR-006 measured the way their own Verification clauses
 * specify: an integration test, over endpoints, at 10 000 records, reading OTel spans.
 *
 * ⚠⚠ THE VARIABILITY LIMITATION, STATED BEFORE ANY NUMBER EXISTS (the DEC-157 d4 discipline).
 * These assert on a SHARED GitHub runner, so a red here can arrive with no code change at all. That
 * is a real cost and it is accepted deliberately, because the operator chose a route that runs in CI
 * and every clause says "assert". It is also unlikely to bite: DW-103 measured the database layer at
 * 8-13 ms against budgets of 500-1000 ms, and adding the HTTP hop costs tens of milliseconds, so a
 * runner would have to be roughly fifty times slower than the reference box to manufacture a red.
 * If one does arrive, read it as an environment signal and confirm against a second run BEFORE
 * treating it as a regression — LL-035: a remedy that moves a probability cannot be falsified by a
 * single recurrence.
 *
 * ⭐ WHAT THIS SUITE DOES NOT COVER, SEQUENCED RATHER THAN QUIETLY DROPPED. NFR-001's clause names a
 * LOAD TEST — "Locust or k6, 15 concurrent users, typical navigation paths" — which is a different
 * instrument from an integration test, so DW-043 stays Open and is the next unit of work.
 */
[Collection(NfrPerfCollection.Name)]
public sealed class NfrEndpointPerformanceTests : IAsyncLifetime
{
    // NFR-002 and NFR-001 both name 15 concurrent users; NFR-003 says "light concurrent load".
    private const int Concurrency = 15;
    private const int Rounds = 20;   // 15 x 20 = 300 samples, enough for a P95 that means something

    private const string SecretaryRoles = "Secretary";

    private readonly NfrPerfFixture _fixture;
    private readonly List<Activity> _sqlSpans = [];
    private readonly ActivityListener _listener;

    private NfrPerfWebFactory _factory = null!;
    private HttpClient _client = null!;

    public NfrEndpointPerformanceTests(NfrPerfFixture fixture)
    {
        _fixture = fixture;

        /*
         * ⭐ NFR-004 SAYS "measure via OTel trace span for the SQL query step", SO THE SQL SPAN IS READ
         * DIRECTLY RATHER THAN INFERRED FROM THE REQUEST. Filtering by source is what keeps the two
         * apart: without ShouldListenTo, an AspNetCore server span and a SqlClient span both land in
         * the same list and the "SQL step" figure silently becomes whole-request latency.
         */
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.Contains("SqlClient", StringComparison.OrdinalIgnoreCase),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (_sqlSpans) _sqlSpans.Add(activity);
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public Task InitializeAsync()
    {
        _factory = new NfrPerfWebFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add(PerfAuthHandler.RolesHeader, SecretaryRoles);
        _client.DefaultRequestHeaders.Add(PerfAuthHandler.SubHeader, "perf-secretary");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _listener.Dispose();
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    /*
     * ⛔⛔ THE CONTROL, AND IT RUNS BEFORE ANY TIMING IS TRUSTED. DW-103's own text warns that SL-030's
     * per-request visibility resolution is the thing most likely to have moved, and every seeded row
     * carries SubmittedBySub='perf-seed-sub'. If visibility filtered them out, every endpoint below
     * would return an EMPTY page — fast, clean, and completely meaningless. That is LL-074's
     * empty-subject failure, and an assertion is the only thing that separates it from a pass.
     */
    [Fact]
    public async Task Seed_is_visible_through_the_api_before_any_latency_is_measured()
    {
        var response = await _client.GetAsync("/api/topics/?page=1&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var total = payload.GetProperty("total").GetInt32();

        total.Should().BeGreaterThanOrEqualTo(NfrPerfFixture.SeededTopics,
            "every later measurement is meaningless if the seeded rows are not reachable through the API — " +
            $"seeded {NfrPerfFixture.SeededTopics} in {_fixture.SeedSeconds:F1}s but the backlog reports {total}");
    }

    [Fact]
    public async Task NFR_002_backlog_list_p95_is_within_1000ms_at_15_concurrent_users()
    {
        var samples = await MeasureAsync(() => _client.GetAsync("/api/topics/?page=1&pageSize=25"));

        Report("NFR-002 list", samples);
        Percentile(samples, 95).Should().BeLessThan(1000);
    }

    [Fact]
    public async Task NFR_003_topic_detail_p95_is_within_500ms()
    {
        // A key from the middle of the seeded range, so it is neither the first nor the last row.
        const string key = NfrPerfFixture.SeedKeyPrefix + "0005000";

        var probe = await _client.GetAsync($"/api/topics/{key}");
        probe.StatusCode.Should().Be(HttpStatusCode.OK, "the detail measurement must resolve a real seeded topic");

        var samples = await MeasureAsync(() => _client.GetAsync($"/api/topics/{key}"));

        Report("NFR-003 detail", samples);
        Percentile(samples, 95).Should().BeLessThan(500);
    }

    [Fact]
    public async Task NFR_004_full_text_search_sql_span_p95_is_within_800ms()
    {
        lock (_sqlSpans) _sqlSpans.Clear();

        var probe = await _client.GetAsync("/api/search?q=observability");
        probe.StatusCode.Should().Be(HttpStatusCode.OK);
        var groups = await probe.Content.ReadFromJsonAsync<JsonElement>();
        groups.GetArrayLength().Should().BeGreaterThan(0,
            "FTS returning nothing would make every duration below a measurement of an empty index");

        /*
         * ⛔ THE SAMPLE COUNT HERE IS SET BY A SECURITY CONTROL, NOT BY STATISTICS, AND THE FIRST RUN
         * PROVED IT. /api/search carries a PER-USER FIXED WINDOW of SearchPermitPerMinute = 60
         * (HardeningExtensions), so the standard 15 x 20 = 300 burst returned HTTP 429 partway through.
         * That is the product behaving correctly, not a fault. 15 x 3 = 45, plus the probe above, stays
         * inside the window — and the status assertion inside MeasureAsync is what caught it, because a
         * 429 is FAST and would otherwise have been timed as a very healthy P95 over rejections.
         */
        var samples = await MeasureAsync(() => _client.GetAsync("/api/search?q=observability"), rounds: 3);
        Report("NFR-004 search (whole request)", samples);

        // The clause names the SQL query STEP, so the verdict is taken on the SqlClient spans.
        double[] sqlDurations;
        lock (_sqlSpans)
            sqlDurations = _sqlSpans.Select(a => a.Duration.TotalMilliseconds).ToArray();

        sqlDurations.Should().NotBeEmpty("NFR-004 is verified 'via OTel trace span for the SQL query step', " +
            "so an empty span list means the instrument, not the system, is what was measured");

        Report("NFR-004 search (SQL span)", sqlDurations);
        Percentile(sqlDurations, 95).Should().BeLessThan(800);
    }

    /*
     * ⚠ NFR-006's VERIFICATION CLAUSE SAYS "measure HTTP round-trip for the PATCH request", AND THERE
     * IS NO PATCH. The autosave note is captured by POST /api/meetings/{id}/discussion — the only
     * write path for a discussion note in the API. The substance of the clause (the autosave round
     * trip) is unambiguous, so the POST is what is measured; the verb mismatch is recorded here rather
     * than silently mapped, because under DEC-159 f2 the clause is part of the requirement and
     * correcting it is the operator's call, not this test's.
     */
    [Fact]
    public async Task NFR_006_discussion_note_round_trip_is_within_2000ms()
    {
        var meetingId = await SeedMeetingAsync();
        var body = new { topicId = Guid.NewGuid(), body = "Synthetic autosave payload for the NFR-006 round trip." };

        var probe = await _client.PostAsJsonAsync($"/api/meetings/{meetingId}/discussion", body);
        probe.StatusCode.Should().Be(HttpStatusCode.NoContent, "the round trip must actually persist to be worth timing");

        // Serial, not concurrent: the clause bounds ONE round trip from a typing pause, not throughput.
        var samples = new List<double>();
        for (var i = 0; i < 30; i++)
        {
            var started = Stopwatch.GetTimestamp();
            var response = await _client.PostAsJsonAsync($"/api/meetings/{meetingId}/discussion", body);
            samples.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        Report("NFR-006 discussion round trip", samples);
        Percentile(samples, 95).Should().BeLessThan(2000);
    }

    /*
     * ⚠ THE READ-BACK IS NOT DEFENSIVE PADDING — THE FIRST RUN 404'd HERE AND THE CAUSE WAS AMBIGUOUS.
     * A 404 from the endpoint has two completely different explanations: the seed never committed, or
     * it committed and the request scope cannot see it. Those need opposite fixes, and the endpoint's
     * status code cannot tell them apart. Reading the row back through a SEPARATE scope splits them:
     * if this assertion fails the write is at fault; if it passes and the POST still 404s, the fault is
     * in how the request scope resolves the context (ADR-0026 shares one connection per scope), which
     * would itself be a finding worth recording.
     */
    private async Task<Guid> SeedMeetingAsync()
    {
        Guid publicId;

        // NOT the host's scope — see NfrPerfFixture.NewMeetingsContext. A write there joins an ambient
        // transaction only TransactionBehavior commits, so it rolls back while reporting success.
        await using (var db = _fixture.NewMeetingsContext())
        {
            var now = DateTimeOffset.UtcNow;
            var meeting = Meeting.Schedule(
                key: "MTG-PERF-0001",
                title: "NFR-006 autosave measurement",
                // The product anchors every meeting to this well-known id (CON-001, one committee).
                // A random guid would have been a variable the product never has.
                committeeId: Meeting.SingleCommitteeId,
                chairUserId: Guid.NewGuid(),
                chairName: "Perf Chair",
                scheduledStart: now.AddHours(1),
                scheduledEnd: now.AddHours(2),
                type: MeetingType.Regular,
                mode: MeetingMode.Remote,
                location: null,
                joinUrl: null,
                now: now);

            db.Meetings.Add(meeting);
            await db.SaveChangesAsync();
            publicId = meeting.PublicId;
        }

        using (var verify = _factory.Services.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            var found = await db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == publicId);
            found.Should().NotBeNull(
                "the seeded meeting must be readable from a fresh scope before the round trip is timed — " +
                "if this fails the seed never committed, which is a different fault from the endpoint not finding it");
        }

        return publicId;
    }

    /// <summary>Drives <see cref="Concurrency"/> requests at a time for <see cref="Rounds"/> rounds.</summary>
    private static async Task<IReadOnlyList<double>> MeasureAsync(Func<Task<HttpResponseMessage>> request, int? rounds = null)
    {
        var roundCount = rounds ?? Rounds;
        var samples = new List<double>(Concurrency * roundCount);

        for (var round = 0; round < roundCount; round++)
        {
            var inFlight = Enumerable.Range(0, Concurrency).Select(async _ =>
            {
                var started = Stopwatch.GetTimestamp();
                using var response = await request();
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                response.StatusCode.Should().Be(HttpStatusCode.OK,
                    "a non-200 short-circuits the handler and would be timed as if it were a success");
                return elapsed;
            });

            samples.AddRange(await Task.WhenAll(inFlight));
        }

        return samples;
    }

    /// <summary>Nearest-rank percentile — no interpolation, so the value is always an observed sample.</summary>
    private static double Percentile(IReadOnlyCollection<double> samples, int percentile)
    {
        var ordered = samples.OrderBy(x => x).ToArray();
        var rank = (int)Math.Ceiling(percentile / 100.0 * ordered.Length);
        return ordered[Math.Clamp(rank - 1, 0, ordered.Length - 1)];
    }

    // Printed so a green run still carries its numbers: a pass with no figure cannot be compared to
    // the next one, and a regression that stays inside the budget would be invisible.
    private static void Report(string label, IReadOnlyCollection<double> samples) =>
        Console.WriteLine(
            $"[perf] {label}: n={samples.Count} " +
            $"P50={Percentile(samples, 50):F1}ms P95={Percentile(samples, 95):F1}ms " +
            $"max={samples.Max():F1}ms cores={Environment.ProcessorCount}");
}
