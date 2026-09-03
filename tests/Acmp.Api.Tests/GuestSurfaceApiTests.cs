using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// ADR-0040 decision 3 / DEF-052 — a Guest reaches the guest surface and NOTHING ELSE.
//
// Every case here FORCES the refusal against the real pipeline. That matters more than usual for this
// gate: the endpoints it protects have no authorization metadata of their own, so a test that merely
// asserted the middleware was registered would pass while the record stayed readable.
public class GuestSurfaceApiTests : IClassFixture<AcmpWebApplicationFactory>
{
    // WBS-27.2 / DEC-124 d1 - ONE host per class instead of one per test method. Every method
    // below now shares this host's fourteen InMemory databases, so a test that asserts over a
    // global count sees what its siblings wrote. SharedHostOrderGuard is the control for that.
    private readonly AcmpWebApplicationFactory _factory;

    public GuestSurfaceApiTests(AcmpWebApplicationFactory factory) => _factory = factory.Reset();

    private static HttpClient Client(AcmpWebApplicationFactory factory, string roles, string sub = "kc-guest")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        return client;
    }

    [Theory] // the whole governance record, which a guest could read before this shipped
    [InlineData("/api/topics")]
    [InlineData("/api/decisions")]
    [InlineData("/api/adrs")]
    [InlineData("/api/invariants")]
    [InlineData("/api/risks")]
    [InlineData("/api/dependencies")]
    [InlineData("/api/actions")]
    [InlineData("/api/minutes")]
    [InlineData("/api/research")]
    [InlineData("/api/knowledge/documents")]
    [InlineData("/api/traceability")]
    [InlineData("/api/votes")]
    [InlineData("/api/members")]
    [InlineData("/api/audit")]
    [InlineData("/api/admin/health")]
    public async Task A_guest_is_refused_on_every_content_api(string path)
    {
        var factory = _factory;

        var response = await Client(factory, "Guest").GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // 403 and not 401: renewing the token cannot fix this, and the SPA renews on 401.
        response.Headers.GetValues("X-Acmp-Auth-Reason").Should().ContainSingle().Which.Should().Be("guest_scope");
    }

    [Fact] // without this a guest cannot complete a single sign-in (ADR-0004 provisions on first login)
    public async Task A_guest_may_provision_their_own_profile()
    {
        var factory = _factory;

        var response = await Client(factory, "Guest").PostAsync("/api/members/me", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact] // 'agenda: view' in the design's role matrix
    public async Task A_guest_may_read_meetings()
    {
        var factory = _factory;

        var response = await Client(factory, "Guest").GetAsync("/api/meetings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact] // VIEW, not full: the read allowance must not carry a write with it
    public async Task A_guest_may_not_write_to_meetings()
    {
        var factory = _factory;

        var response = await Client(factory, "Guest").PostAsJsonAsync("/api/meetings", new { title = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.GetValues("X-Acmp-Auth-Reason").Should().ContainSingle().Which.Should().Be("guest_scope");
    }

    [Fact] // the bell renders in the shell for every signed-in user
    public async Task A_guest_may_read_their_own_notifications()
    {
        var factory = _factory;

        var response = await Client(factory, "Guest").GetAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact] // the gate must not fire for anybody else — this is the regression that would hurt most
    public async Task A_committee_member_is_unaffected()
    {
        var factory = _factory;

        var response = await Client(factory, "Member", sub: "kc-member").GetAsync("/api/topics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact] // an insider who is ALSO listed as a guest keeps their committee access
    public async Task A_principal_holding_Guest_and_a_committee_role_is_treated_as_an_insider()
    {
        var factory = _factory;

        var response = await Client(factory, "Guest,Member", sub: "kc-both").GetAsync("/api/topics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
