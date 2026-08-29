using System.Net;

// DW-080 phase B / DEC-090 d2 — the container healthcheck for the api image, as a .NET entrypoint.
//
// WHY THIS EXISTS AT ALL. The api moved to `aspnet:10.0-noble-chiseled-extra`, which ships no shell and
// no package manager, and the compose healthcheck was `CMD-SHELL` running `curl -f .../readyz`. That
// cannot survive the base change in any form: there is nothing to install curl with, and nothing to
// interpret a shell string. This probe is invoked as a plain `CMD` through the `dotnet` host that is
// already in the image, so it needs neither.
//
// ⚠⚠ IT KEEPS PROBING /readyz, AND THAT IS THE POINT RATHER THAN A DETAIL. The cheap alternative was to
// drop the container healthcheck and gate on TCP from compose — which is exactly DEF-079: a check mapped
// with a predicate that evaluated NO checks, so it could only fail if the process stopped answering the
// socket. /readyz reaches SQL Server, Hangfire and object storage, and that reach IS the value. A probe
// that cannot fail while the app is broken is worse than no probe, because it reports health.
//
// EXIT CODE IS THE WHOLE INTERFACE: 0 healthy, 1 not. Docker reads nothing else.

var url = Environment.GetEnvironmentVariable("HEALTHPROBE_URL") ?? "http://127.0.0.1:8080/readyz";

// The timeout is the probe's own, deliberately below the compose `timeout: 5s`, so a hung request is
// reported as unhealthy by THIS process rather than killed by Docker — a killed probe and a failed one
// are indistinguishable in `docker inspect`, and only one of them tells you the app is wedged.
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };

try
{
    using var response = await http.GetAsync(url);

    if (response.StatusCode == HttpStatusCode.OK)
        return 0;

    // Not silent: an unhealthy container's reason belongs in `docker inspect --format '{{json .State.Health}}'`,
    // which captures this stream. A probe that fails without saying why turns every outage into a guess.
    await Console.Error.WriteLineAsync($"unhealthy: {url} -> {(int)response.StatusCode} {response.StatusCode}");
    return 1;
}
catch (Exception ex)
{
    // A refused connection during startup is the NORMAL first few probes, not an error worth decorating.
    // It still exits 1, which is what keeps the container "starting" until the retries are exhausted.
    await Console.Error.WriteLineAsync($"unhealthy: {url} -> {ex.GetType().Name}: {ex.Message}");
    return 1;
}
