using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests through the real pipeline + policy authorization (the JWT injector P2 deferred).
public class MembershipApiTests
{
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

    private sealed record MemberRow(Guid PublicId, string Role, string Status);
    private sealed record Profile(string Role);

    [Fact]
    public async Task No_token_returns_401() // AC-008
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, roles: null).GetAsync("/api/members");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory] // AC-059: directory readable by any authenticated role
    [InlineData("Member")]
    [InlineData("Auditor")]
    [InlineData("Submitter")]
    [InlineData("Guest")]
    public async Task Directory_is_readable_by_every_role(string role)
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-dir", "Directory Member", CommitteeRole.Member));

        var response = await Client(factory, role).GetAsync("/api/members");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var members = await response.Content.ReadFromJsonAsync<List<MemberRow>>();
        members.Should().NotBeNullOrEmpty();
    }

    [Theory] // AC-005 / AC-006: non-admin write to an admin endpoint is forbidden (not 401)
    [InlineData("Submitter")]
    [InlineData("Auditor")]
    [InlineData("Member")]
    public async Task Non_admin_cannot_deactivate_member_403(string role)
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, role).PostAsync($"/api/members/{Guid.NewGuid()}/deactivate", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // AC-007 boundary: Administrator holds Admin.Users (and only platform-admin policies)
    public async Task Administrator_can_deactivate_member()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-bob", "Bob", CommitteeRole.Member));
        var admin = Client(factory, "Administrator", sub: "kc-admin");

        var members = await (await admin.GetAsync("/api/members")).Content.ReadFromJsonAsync<List<MemberRow>>();
        var bob = members!.Single(m => m.Role == nameof(CommitteeRole.Member));

        var response = await admin.PostAsync($"/api/members/{bob.PublicId}/deactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact] // AC-002: claim -> role, end to end over HTTP
    public async Task Provision_me_returns_role_from_claims()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Secretary", sub: "kc-sec").PostAsync("/api/members/me", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<Profile>();
        profile!.Role.Should().Be("Secretary");
    }
}
