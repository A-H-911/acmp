using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

/// <summary>
/// NFR-020 / AC-115 — "No endpoint is unauthenticated except health checks and the OIDC callback.
/// Target: 0 unauthenticated non-health endpoints."
/// </summary>
/// <remarks>
/// ⚠ WHY THIS EXISTS WHEN RealJwtAuthTests ALREADY PASSES. That suite proves the auth pipeline
/// challenges correctly by probing THREE routes: anonymous → 401, bogus bearer → 401, health stays
/// open. Three spot checks cannot establish "0 unauthenticated endpoints" over a surface of dozens,
/// and the requirement's target is a statement about the WHOLE SET. A new endpoint group added
/// without <c>RequireAuthorization()</c> would sail past every existing test — it would simply not be
/// one of the three probed. This enumerates the real routing table instead.
/// <para>
/// ⚠ AN ABSENCE IS ONLY EVIDENCE IF THE INSTRUMENT IS PROVEN PRESENT. "No AllowAnonymous appears in
/// the API" is the kind of grep that passes just as happily when the search is wrong. So the first
/// case asserts the enumeration actually SEES a substantial routing table before the second case
/// draws any conclusion from what it does not find.
/// </para>
/// </remarks>
[Trait("Category", "Security")]
public class EndpointAuthorizationCoverageTests : IClassFixture<AcmpWebApplicationFactory>
{
    private readonly AcmpWebApplicationFactory _factory;

    public EndpointAuthorizationCoverageTests(AcmpWebApplicationFactory factory) => _factory = factory;

    // Routes that carry NO authorization metadata by design. ⚠ "No bearer" is NOT "no authentication":
    // three of these five are authenticated by a mechanism the routing table cannot see, and each entry
    // names it. A future endpoint may only join this list with a comparable control of its own — copying
    // a line here without one is how a genuinely open endpoint gets excused.
    private static readonly string[] AnonymousByDesign =
    {
        "/healthz",  // liveness. NFR-020 names health checks explicitly; body is Predicate = _ => false.
        "/readyz",   // readiness. Same clause. DEF-078 hardened WHAT it says, not whether it is reachable.

        // The Webex trio cannot use the Keycloak bearer: two are top-level browser navigations and one is
        // a server-to-server callback from Webex. Each is authenticated another way instead.
        "/api/webex/webhook",        // HMAC — WebexSignatureFilter runs BEFORE the handler, plus a 5-minute replay guard.
        "/api/webex/oauth/start",    // operator-only setup key, FixedTimeEquals, fail-closed to 404 (not 401 — it hides the route).
        "/api/webex/oauth/callback", // completes only a flow whose single-use `state` cookie was minted by a key-gated /start.
    };

    private static IReadOnlyList<RouteEndpoint> RoutableEndpoints(IServiceProvider services) =>
        services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .ToList();

    [Fact]
    public void The_enumeration_sees_a_real_routing_table()
    {
        var endpoints = RoutableEndpoints(_factory.Services);

        // THE CONTROL ON THE CONTROL. If EndpointDataSource resolved to an empty or tiny set — a host
        // that failed to map, a future refactor to a different routing source — the coverage test
        // below would pass VACUOUSLY and report a clean security posture over nothing at all.
        endpoints.Should().HaveCountGreaterThan(50,
            "the coverage assertion below is meaningless unless the real API surface was enumerated");

        // And the enumeration must actually reach the API routes, not just framework plumbing.
        endpoints.Select(e => e.RoutePattern.RawText ?? string.Empty)
            .Where(p => p.StartsWith("/api/", StringComparison.Ordinal))
            .Should().HaveCountGreaterThan(40, "the /api surface is what NFR-020 is about");
    }

    [Fact]
    public void Every_endpoint_except_health_and_the_oauth_callback_requires_authorization()
    {
        var unprotected = RoutableEndpoints(_factory.Services)
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(e => e.RoutePattern.RawText ?? "(no pattern)")
            .Where(p => !AnonymousByDesign.Contains(p, StringComparer.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        // NFR-020's target is literally zero, so the assertion is literally zero — and it names the
        // offenders, because "some endpoint is open" is not an actionable failure message.
        unprotected.Should().BeEmpty(
            "NFR-020 targets 0 unauthenticated non-health endpoints; these carry no authorization "
            + "metadata and are not on the anonymous-by-design list: {0}",
            string.Join(", ", unprotected));
    }

    [Fact]
    public void Each_anonymous_by_design_route_actually_exists()
    {
        // Otherwise the allowlist above silently rots: a renamed or removed route would leave a
        // stale entry that could later excuse a genuinely unprotected endpoint of the same name.
        var patterns = RoutableEndpoints(_factory.Services)
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var allowed in AnonymousByDesign)
            patterns.Should().Contain(allowed,
                "every anonymous-by-design entry must name a route that exists, or the allowlist is rotting");
    }
}
