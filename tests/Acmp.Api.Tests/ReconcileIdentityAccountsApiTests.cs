using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MembershipStream = Acmp.Modules.Membership.Domain.Stream;

namespace Acmp.Api.Tests;

// DEC-046 / DEF-065 — the HTTP contract for reconciling identity accounts into committee_members.
//
// A handler test proves the rule; only a request through the real pipeline proves that an
// unauthorised caller never reaches the handler at all.
//
// ⚠ WHAT THESE TESTS DO AND DO NOT PROVE, MEASURED RATHER THAN CLAIMED. Removing
// `.RequireAuthorization(Policies.AdminUsers)` from the endpoint leaves all nine GREEN: the refusal
// is also enforced by AuthorizationBehavior from the command's own AllowedRoles, so what is proven
// here is the RULE, not the endpoint policy. That is DEF-056's layering fact seen from the other
// side — two layers encode the same matrix and the outer one wins. The endpoint policy is kept for
// consistency with its two sibling admin endpoints, and it carries the same consequence they do:
// because it short-circuits before MediatR, a 403 from it is NOT audited until DEF-056 is fixed.
public class ReconcileIdentityAccountsApiTests
{
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

    [Fact]
    public async Task Reconcile_without_a_token_is_401()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, roles: null).PostAsync("/api/members/reconcile", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Chairman")]
    [InlineData("Secretary")]
    [InlineData("Member")]
    [InlineData("Reviewer")]
    [InlineData("Auditor")]
    [InlineData("Guest")]
    public async Task Reconcile_is_403_for_every_role_except_Administrator(string role)
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, role, "kc-other").PostAsync("/api/members/reconcile", null);

        // ⚠ SECRETARY IS IN THIS LIST DELIBERATELY, and it is the discriminating case. Secretary MAY
        // invite (DEC-038), so "the roles that administer the committee" is not the axis here. This
        // creates member rows and grants them the wildcard stream, which is what
        // PUT /api/members/{id}/streams does — Administrator only, permission-role-matrix row 27.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_authorised_caller_still_cannot_reconcile_when_no_identity_provider_is_configured()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Administrator").PostAsync("/api/members/reconcile", null);

        // The default test host configures no Keycloak service account, so IIdentityProvider is ABSENT.
        // There is nothing to reconcile against, so the command refuses rather than reporting a run
        // that saw an empty realm — "no accounts found" and "could not look" must not be the same answer.
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, "authorization passed; it is the missing dependency that stops it");
    }

    [Fact]
    public async Task Reconcile_creates_the_member_over_HTTP_and_the_roster_then_shows_them()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var wildcardId = SeedWildcard(factory);

        factory.Identity.Accounts.Add(new IdentityAccount(
            "kc-seeded", "seeded@acmp.gov", "Seeded Person", Enabled: true, new[] { nameof(CommitteeRole.Member) }));

        var response = await Client(factory, "Administrator").PostAsync("/api/members/reconcile", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ReconcileResponse>();
        result!.Created.Should().Be(1);
        result.IdentityAccounts.Should().Be(1);

        // DEC-046 d2: reconciling is also what answers DEF-038 — the roster lists these people as
        // ordinary members, so no parallel Keycloak listing is needed to see them.
        var members = await Client(factory, "Administrator").GetFromJsonAsync<List<MemberRow>>("/api/members");
        var seeded = members!.Single(m => m.FullName == "Seeded Person");
        seeded.Status.Should().Be(nameof(MembershipStatus.Invited));
        seeded.Streams.Should().ContainSingle().Which.IsWildcard.Should().BeTrue(
            "a row created without the wildcard is refused every stream-scoped write from its owner's first login (DEF-071)");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        db.Members.Single(m => m.KeycloakUserId == "kc-seeded")
            .Streams.Select(s => s.StreamId).Should().ContainSingle().Which.Should().Be(wildcardId);
    }

    /// <summary>
    /// The wildcard, added per test rather than to the harness's shared seed. That seed leaves it out
    /// on purpose — StreamCatalog excludes the wildcard from the assignable set, so seeding it
    /// globally could only mask a bug in that filter. This command is the one caller that needs it.
    /// </summary>
    private static long SeedWildcard(AcmpWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        var wildcard = MembershipStream.Create("all-streams", LocalizedString.Create("All streams", "كل المسارات"));
        typeof(MembershipStream).GetProperty(nameof(MembershipStream.IsWildcard))!.SetValue(wildcard, true);
        db.Streams.Add(wildcard);
        db.SaveChanges();
        return wildcard.Id;
    }

    private sealed record ReconcileResponse(int IdentityAccounts, int Created, int AlreadyProvisioned);

    private sealed record StreamRow(bool IsWildcard);

    private sealed record MemberRow(Guid PublicId, string FullName, string Status, List<StreamRow> Streams);
}
