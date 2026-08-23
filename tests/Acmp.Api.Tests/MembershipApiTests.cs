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

    [Theory] // AC-059: directory readable by any authenticated COMMITTEE role
    [InlineData("Member")]
    [InlineData("Auditor")]
    [InlineData("Submitter")]
    // ⚠ GUEST WAS HERE AND WAS REMOVED DELIBERATELY (SC-007, authorized by DEC-040). AC-059 says
    // "any authenticated user of any role", and it was written when every principal was a committee
    // member. FR-159 creates the first EXTERNAL one, and its own wording is "their own session
    // material and nothing else" — the directory is 26 people's names and email addresses. The
    // narrowing is recorded rather than made silently; the refusal itself is proven in
    // GuestSurfaceApiTests.
    public async Task Directory_is_readable_by_every_committee_role(string role)
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

    // FR-162 / AC-111 / DEF-085 — the counterpart of deactivate, over HTTP. Before this endpoint a
    // disabled member was locked out permanently: re-invite throws on the duplicate email and the
    // Keycloak user can never be deleted.
    [Fact]
    public async Task Administrator_can_reactivate_a_deactivated_member()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-bob", "Bob", CommitteeRole.Member));
        var admin = Client(factory, "Administrator", sub: "kc-admin");

        var members = await (await admin.GetAsync("/api/members")).Content.ReadFromJsonAsync<List<MemberRow>>();
        var bob = members!.Single(m => m.Role == nameof(CommitteeRole.Member));

        (await admin.PostAsync($"/api/members/{bob.PublicId}/deactivate", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PostAsync($"/api/members/{bob.PublicId}/reactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        // Read it back rather than trusting the 204 — the whole defect was a state that LOOKED fixed.
        var after = await (await admin.GetAsync("/api/members")).Content.ReadFromJsonAsync<List<MemberRow>>();
        after!.Single(m => m.PublicId == bob.PublicId).Status.Should().Be(nameof(MembershipStatus.Active));
    }

    [Theory] // same admin-only boundary as deactivate (AC-005 / AC-006)
    [InlineData("Submitter")]
    [InlineData("Member")]
    public async Task Non_admin_cannot_reactivate_member_403(string role)
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, role).PostAsync($"/api/members/{Guid.NewGuid()}/reactivate", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    [Fact] // BL-024: Administrator assigns a member's streams (empty set clears them) -> 204
    public async Task Administrator_assigns_streams_returns_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-mem", "Mem One", CommitteeRole.Member));
        var admin = Client(factory, "Administrator", sub: "kc-admin");

        var member = (await (await admin.GetAsync("/api/members")).Content.ReadFromJsonAsync<List<MemberRow>>())!
            .Single(m => m.Role == nameof(CommitteeRole.Member));

        var response = await admin.PutAsJsonAsync($"/api/members/{member.PublicId}/streams", Array.Empty<Guid>());

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact] // docs/10 §E.3 (Auth.Delegate): Secretary delegates a capability for a bounded window -> 201
    public async Task Secretary_creates_a_delegation_returns_201()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(
            ("kc-sec", "Sec One", CommitteeRole.Secretary),
            ("kc-target", "Target One", CommitteeRole.Member));
        var sec = Client(factory, "Secretary", sub: "kc-sec");

        var target = (await (await sec.GetAsync("/api/members")).Content.ReadFromJsonAsync<List<MemberRow>>())!
            .Single(m => m.Role == nameof(CommitteeRole.Member));
        var body = new
        {
            delegateMemberPublicId = target.PublicId,
            capability = "Vote.Cast",
            validFrom = DateTimeOffset.UtcNow,
            validTo = DateTimeOffset.UtcNow.AddDays(7),
        };

        var response = await sec.PostAsJsonAsync("/api/members/delegations", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
