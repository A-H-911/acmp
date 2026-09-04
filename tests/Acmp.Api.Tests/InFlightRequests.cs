using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Acmp.Api.Tests;

// DEF-134 / DEC-125 d1 — the seam at which DEF-109's fault is actually visible.
//
// WHY THIS EXISTS, AND IT IS THE WHOLE POINT OF THE ROW. WBS-27.1's watchdog had one trigger: a
// dedicated sampler asking to sleep 5s and waking >=20s later, i.e. THE PROCESS WAS NOT SCHEDULED.
// DEF-109 occurrence 6 fired with that instrument in place and its positive control live, and the
// artefact recorded 204 samples with windowMaxDrift of 0.049s ONCE and under 3 MILLISECONDS in every
// other window — against a 15-second threshold — while eighteen requests each burned a full
// 100-second HttpClient ceiling across seventeen classes. The process was scheduled promptly and
// continuously throughout the failure. So the fault leaves the trigger's quantity AT ITS HEALTHY
// VALUE, and no threshold on it can ever fire (PE-829, PE-830).
//
// ⭐ SO THE TRIGGER IS KEYED ON THE FAULT'S OWN DEFINITION INSTEAD. DEF-109 *is* "a request did not
// come back". This register is that, directly: every request the suite issues is recorded while it is
// in flight, and the watchdog asks whether any has been outstanding longer than a bound. There is no
// proxy and therefore no coupling left to assume.
//
// ⭐⭐ AND IT MAKES THE COUPLING INJECTABLE, WHICH IS LL-055's ACTUAL LESSON. The previous control
// injected a 40-second DRIFT — the trigger's own predicate — which is a tautology with respect to the
// question that matters, and StallWatchdogTests' header said so honestly and deferred it to "only an
// occurrence can show that". An occurrence took two days and three PRs to arrive and said no. A hung
// request, by contrast, can be injected in-process in milliseconds (see the delaying handler in
// StallWatchdogTests), so the fault-to-trigger path is exercised before this ever ships.
//
// ⚠ DELIBERATELY NOT A DIAGNOSIS, on DW-097's model. Naming which requests were outstanding does not
// name why. What it buys is the discriminating data the register has never had: whether eighteen
// scattered failures share an endpoint, a module or a verb — and DEC-110 d2's surviving branch, after
// occurrence 6 refuted scheduling, is a deadlock, which that comparison speaks to and a resource
// figure does not.
internal static class InFlightRequests
{
    // Keyed on a monotonic ticket rather than the HttpRequestMessage: the same message instance is
    // never reused, but a dictionary keyed on a reference keeps the message (and its content stream)
    // alive for the whole run, which is exactly the retention shape DW-096 is about.
    private static readonly ConcurrentDictionary<long, Entry> Live = new();
    private static long _nextTicket;

    internal readonly record struct Entry(string Method, string Path, long StartedAtTicks);

    /// <summary>Record a request as started. Returns the ticket to pass to <see cref="Complete"/>.</summary>
    internal static long Begin(string method, string path)
    {
        var ticket = Interlocked.Increment(ref _nextTicket);
        Live[ticket] = new Entry(method, path, Stopwatch.GetTimestamp());
        return ticket;
    }

    /// <summary>Record a request as finished, however it finished.</summary>
    internal static void Complete(long ticket) => Live.TryRemove(ticket, out _);

    /// <summary>
    /// Every request outstanding longer than <paramref name="threshold"/>, oldest first. Empty is the
    /// healthy answer and is what makes a non-empty result meaningful.
    /// </summary>
    internal static IReadOnlyList<(string Method, string Path, TimeSpan Age)> Outstanding(TimeSpan threshold)
    {
        var now = Stopwatch.GetTimestamp();
        var hits = new List<(string, string, TimeSpan)>();

        // Snapshot semantics are fine and the race is benign in both directions: a request that
        // completes mid-enumeration is reported as hung one sample early at worst, and one that starts
        // mid-enumeration is picked up on the next sample five seconds later. Neither can invent a
        // request that was never outstanding, which is the only error that would matter here.
        foreach (var (_, e) in Live)
        {
            var age = Stopwatch.GetElapsedTime(e.StartedAtTicks, now);
            if (age >= threshold) hits.Add((e.Method, e.Path, age));
        }

        hits.Sort((a, b) => b.Item3.CompareTo(a.Item3));
        return hits;
    }

    /// <summary>How many requests are in flight, regardless of age — context for a snapshot.</summary>
    internal static int LiveCount => Live.Count;

    /// <summary>Drop all state. For tests only, so one test's injected request cannot reach another.</summary>
    internal static void Reset()
    {
        Live.Clear();
        Interlocked.Exchange(ref _nextTicket, 0);
    }

    /// <summary>
    /// Puts the tracking middleware at the FRONT of the real pipeline without replacing it. Registered
    /// once in <see cref="AcmpWebApplicationFactory"/>, so no test opts in — an instrument that has to
    /// be switched on for the run that happens to hang is an instrument that is never on.
    /// </summary>
    internal sealed class StartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            // Before next(app), so this wraps EVERY later component — authentication, authorization,
            // routing, the MediatR pipeline and the exception handler alike. A stall anywhere inside any
            // of them is inside this timer.
            app.Use(async (context, nextMiddleware) =>
            {
                var ticket = Begin(context.Request.Method, context.Request.Path + context.Request.QueryString);
                try
                {
                    await nextMiddleware(context);
                }
                finally
                {
                    // ⚠ finally, not a line after the await: DEF-109's requests end by CANCELLATION at
                    // the 100-second ceiling, so on the failure this exists to observe the ONLY path that
                    // runs is the exceptional one. Completing on success alone would leak every hung
                    // request into the register permanently, and the first occurrence would become a
                    // false positive for the rest of the run.
                    Complete(ticket);
                }
            });

            next(app);
        };
    }
}
