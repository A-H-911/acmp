using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using FluentAssertions;

namespace Acmp.Api.Tests;

// FR-156/157 · AC-089 — the HTTP contract for in-app user management (ADR-0038).
//
// These exist because AC-089 says the guards hold "server-side, REGARDLESS OF WHAT THE UI OFFERS".
// A handler test proves the rule; only a request through the real pipeline proves that an
// unauthorised caller never reaches the handler at all. The two are not substitutes: hiding a
// control in the SPA is presentation gating, and navModel.ts says so in as many words — "this is
// presentation gating only; the API enforces authorization".
public class UserManagementApiTests : IClassFixture<AcmpWebApplicationFactory>
{
    // WBS-27.2 / DEC-124 d1 - ONE host per class instead of one per test method. Every method
    // below now shares this host's fourteen InMemory databases, so a test that asserts over a
    // global count sees what its siblings wrote. SharedHostOrderGuard is the control for that.
    private readonly AcmpWebApplicationFactory _factory;

    public UserManagementApiTests(AcmpWebApplicationFactory factory) => _factory = factory.Reset();

    private static HttpClient Client(AcmpWebApplicationFactory factory, string? roles, string sub = "u1")
    {
        var client = factory.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    private sealed record RolesBody(string[] Roles, bool ConfirmedPrivileged);

    [Fact]
    public async Task Invite_without_a_token_is_401()
    {
        var factory = _factory;

        var response = await Client(factory, roles: null)
            .PostAsJsonAsync("/api/members/invite", new { Email = "x@acmp.gov", FullName = "X Y" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Chairman")]
    [InlineData("Reviewer")]
    [InlineData("Auditor")]
    [InlineData("Guest")]
    public async Task Invite_is_403_for_every_role_except_Administrator_and_Secretary(string role)
    {
        var factory = _factory;

        var response = await Client(factory, role)
            .PostAsJsonAsync("/api/members/invite", new { Email = "x@acmp.gov", FullName = "X Y" });

        // Chairman is in this list deliberately: seniority is not the axis. DEC-038 grants the
        // capability to the two roles that administer the committee, not to the one that leads it.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Reviewer")]
    [InlineData("Auditor")]
    [InlineData("Guest")]
    public async Task Assigning_roles_is_403_for_a_role_that_may_not(string role)
    {
        var factory = _factory;
        await factory.SeedMembersAsync(("kc-target", "Target Person", CommitteeRole.Member));

        var members = await Client(factory, "Administrator", "kc-admin")
            .GetFromJsonAsync<List<MemberRow>>("/api/members");
        var target = members!.Single(m => m.Role == nameof(CommitteeRole.Member));

        var response = await Client(factory, role, "kc-other").PutAsJsonAsync(
            $"/api/members/{target.PublicId}/roles", new RolesBody(new[] { nameof(CommitteeRole.Reviewer) }, false));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Assigning_roles_without_a_token_is_401()
    {
        var factory = _factory;

        var response = await Client(factory, roles: null).PutAsJsonAsync(
            $"/api/members/{Guid.NewGuid()}/roles", new RolesBody(new[] { nameof(CommitteeRole.Member) }, false));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_authorised_caller_still_cannot_invite_when_no_identity_provider_is_configured()
    {
        var factory = _factory;

        var response = await Client(factory, "Administrator", "kc-admin")
            .PostAsJsonAsync("/api/members/invite", new { Email = "x@acmp.gov", FullName = "X Y" });

        // The test host configures no Keycloak service account, so IIdentityProvider is ABSENT
        // rather than registered-and-disabled. The request fails at composition instead of silently
        // creating a member row with no identity behind it — which would be an account nobody can
        // ever sign in to, and DEF-029 means that row could never be deleted.
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, "authorization passed; it is the missing dependency that stops it");
    }

    private sealed record MemberRow(Guid PublicId, string Role, string Status);
}
