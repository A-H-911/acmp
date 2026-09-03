using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using FluentAssertions;

namespace Acmp.Api.Tests;

// DEF-041 / DEC-046 d4 — the HTTP contract for changing who may vote.
//
// The gate is the command's own AllowedRoles rather than a per-endpoint policy, so THIS is where it
// is proven end to end: a handler test shows the rule, a request through the real pipeline shows an
// unauthorised caller never reaching the handler.
public class VotingEligibilityApiTests : IClassFixture<AcmpWebApplicationFactory>
{
    // WBS-27.2 / DEC-124 d1 - ONE host per class instead of one per test method. Every method
    // below now shares this host's fourteen InMemory databases, so a test that asserts over a
    // global count sees what its siblings wrote. SharedHostOrderGuard is the control for that.
    private readonly AcmpWebApplicationFactory _factory;

    public VotingEligibilityApiTests(AcmpWebApplicationFactory factory) => _factory = factory.Reset();

    private sealed record Body(bool IsVotingEligible);

    private sealed record MemberRow(Guid PublicId, string Role, bool IsVotingEligible);

    private static HttpClient Client(AcmpWebApplicationFactory factory, string? roles, string sub = "kc-actor")
    {
        var client = factory.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    private static async Task<Guid> SeedTargetAsync(AcmpWebApplicationFactory factory)
    {
        await factory.SeedMembersAsync(("kc-voter", "Voter Person", CommitteeRole.Member));
        var members = await Client(factory, "Chairman", "kc-chair").GetFromJsonAsync<List<MemberRow>>("/api/members");
        return members!.Single(m => m.PublicId != Guid.Empty && m.Role == nameof(CommitteeRole.Member)).PublicId;
    }

    [Fact]
    public async Task Changing_voting_eligibility_without_a_token_is_401()
    {
        var factory = _factory;

        var response = await Client(factory, roles: null)
            .PutAsJsonAsync($"/api/members/{Guid.NewGuid()}/voting-eligibility", new Body(false));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Member")]
    [InlineData("Reviewer")]
    [InlineData("Auditor")]
    [InlineData("Guest")]
    public async Task Changing_voting_eligibility_is_403_for_every_role_except_Chairman_and_Secretary(string role)
    {
        var factory = _factory;
        var target = await SeedTargetAsync(factory);

        var response = await Client(factory, role, "kc-other")
            .PutAsJsonAsync($"/api/members/{target}/voting-eligibility", new Body(false));

        // ⚠ ADMINISTRATOR IS THE DISCRIMINATING CASE, and it is first in the list for that reason.
        // It is the role that administers the roster and may assign streams and deactivate members —
        // so "the admin roles" is exactly the wrong generalisation here. SoD-5 keeps Administrator
        // out of committee content, and who may vote is content (DEC-046 d4).
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Chairman")]
    [InlineData("Secretary")]
    public async Task Chairman_and_Secretary_can_both_turn_eligibility_off(string role)
    {
        var factory = _factory;
        var target = await SeedTargetAsync(factory);

        var response = await Client(factory, role, "kc-" + role.ToLowerInvariant())
            .PutAsJsonAsync($"/api/members/{target}/voting-eligibility", new Body(false));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Read back through the API rather than the DbContext: the point is that the change is
        // visible to the roster the committee actually looks at.
        var members = await Client(factory, "Chairman", "kc-chair").GetFromJsonAsync<List<MemberRow>>("/api/members");
        members!.Single(m => m.PublicId == target).IsVotingEligible.Should().BeFalse();
    }

    [Fact]
    public async Task An_unknown_member_is_404()
    {
        var factory = _factory;

        var response = await Client(factory, "Chairman", "kc-chair")
            .PutAsJsonAsync($"/api/members/{Guid.NewGuid()}/voting-eligibility", new Body(true));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
