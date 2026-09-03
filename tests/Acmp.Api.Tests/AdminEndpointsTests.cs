using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// System Health endpoint (NR-08). Admin-config gated (Administrator only). The report always includes
// the synthetic "api" liveness entry; the SPA overlays these onto its fixed service catalog.
public class AdminEndpointsTests : IClassFixture<AcmpWebApplicationFactory>
{
    // WBS-27.2 / DEC-124 d1 - ONE host per class instead of one per test method. Every method
    // below now shares this host's fourteen InMemory databases, so a test that asserts over a
    // global count sees what its siblings wrote. SharedHostOrderGuard is the control for that.
    private readonly AcmpWebApplicationFactory _factory;

    public AdminEndpointsTests(AcmpWebApplicationFactory factory) => _factory = factory.Reset();

    private sealed record HealthResponse(string Status, List<HealthEntry> Entries);
    private sealed record HealthEntry(string Name, string Status, string? Description, double DurationMs);

    private static HttpClient Client(AcmpWebApplicationFactory factory, string? roles, string sub = "kc-admin")
    {
        var client = factory.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    [Fact] // Administrator reads the live health report; the "api" liveness check is always present.
    public async Task Administrator_gets_health_report_including_api_entry()
    {
        var factory = _factory;

        var response = await Client(factory, "Administrator").GetAsync("/api/admin/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body.Should().NotBeNull();
        body!.Entries.Should().Contain(e => e.Name == "api");
        body.Entries.Single(e => e.Name == "api").Status.Should().Be("Healthy");
    }

    [Fact] // docs/10: Admin.Config is Administrator-only — a Member is forbidden.
    public async Task Non_admin_is_forbidden()
    {
        var factory = _factory;

        var response = await Client(factory, "Member", sub: "kc-member").GetAsync("/api/admin/health");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // No bearer → 401 before any health check runs.
    public async Task Unauthenticated_is_unauthorized()
    {
        var factory = _factory;

        var response = await Client(factory, roles: null).GetAsync("/api/admin/health");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
