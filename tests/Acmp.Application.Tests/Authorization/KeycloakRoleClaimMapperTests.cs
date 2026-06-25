using Acmp.Shared.Authorization;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Acmp.Application.Tests.Authorization;

// AC-002 (claim -> canonical role) and AC-003 (no recognised claim -> empty, host then denies).
public class KeycloakRoleClaimMapperTests
{
    private static KeycloakRoleClaimMapper Mapper(RoleMappingOptions? options = null) =>
        new(Options.Create(options ?? new RoleMappingOptions()));

    [Theory]
    [InlineData("chairman", AcmpRoles.Chairman)]
    [InlineData("acmp-secretary", AcmpRoles.Secretary)]
    [InlineData("/acmp/member", AcmpRoles.Member)]
    [InlineData("ACMP/Auditor", AcmpRoles.Auditor)]
    [InlineData("coordinator", AcmpRoles.Secretary)] // legacy alias (renamed 2026-06-25)
    public void Maps_known_keycloak_claim_shapes_to_canonical_roles(string claim, string expected)
    {
        Mapper().Map(new[] { claim }).Should().ContainSingle().Which.Should().Be(expected);
    }

    [Fact]
    public void Maps_a_secretary_group_claim_so_secretary_actions_are_available()
    {
        // AC-002: a Keycloak group claim mapping to Secretary yields the Secretary role.
        Mapper().Map(new[] { "/acmp/coordinator" }).Should().Equal(AcmpRoles.Secretary);
    }

    [Fact]
    public void Unknown_claims_map_to_nothing()
    {
        // AC-003: a token whose claims contain no recognised ACMP role yields an empty set.
        Mapper().Map(new[] { "some-other-app-role", "" }).Should().BeEmpty();
    }

    [Fact]
    public void Distinct_roles_are_returned_once()
    {
        Mapper().Map(new[] { "chairman", "acmp-chairman", "/acmp/chairman" })
            .Should().Equal(AcmpRoles.Chairman);
    }

    [Fact]
    public void Config_override_maps_a_non_standard_claim()
    {
        var options = new RoleMappingOptions { ClaimToRole = { ["vp-architecture"] = AcmpRoles.Chairman } };
        Mapper(options).Map(new[] { "vp-architecture" }).Should().Equal(AcmpRoles.Chairman);
    }
}
