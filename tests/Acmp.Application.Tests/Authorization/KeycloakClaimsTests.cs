using System.Security.Claims;
using Acmp.Shared.Authorization;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Acmp.Application.Tests.Authorization;

// The host's token claim extraction (KeycloakClaims.RoleValues) — the Keycloak realm_access /
// resource_access nested-JSON shapes plus flat groups/role claims that AC-002 ultimately rides on.
public class KeycloakClaimsTests
{
    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Test"));

    [Fact]
    public void Extracts_realm_access_resource_access_groups_and_flat_role_claims()
    {
        var principal = Principal(
            new Claim("realm_access", """{"roles":["chairman","acmp-secretary"]}"""),
            new Claim("resource_access", """{"acmp-api":{"roles":["administrator"]}}"""),
            new Claim("groups", "/acmp/member"),
            new Claim("role", "auditor"));

        var values = KeycloakClaims.RoleValues(principal);

        values.Should().BeEquivalentTo(new[] { "chairman", "acmp-secretary", "administrator", "/acmp/member", "auditor" });
    }

    [Fact]
    public void Ignores_a_non_json_realm_access_value_gracefully()
    {
        var principal = Principal(new Claim("realm_access", "not-json"), new Claim("groups", "chairman"));

        KeycloakClaims.RoleValues(principal).Should().Equal("chairman");
    }

    [Fact]
    public void Extracted_values_map_to_canonical_roles_end_to_end()
    {
        // KeycloakClaims (extract) -> KeycloakRoleClaimMapper (canonicalize), the full AC-002 path.
        var principal = Principal(new Claim("realm_access", """{"roles":["acmp-secretary"]}"""));
        var mapper = new KeycloakRoleClaimMapper(Options.Create(new RoleMappingOptions()));

        var roles = mapper.Map(KeycloakClaims.RoleValues(principal));

        roles.Should().Equal(AcmpRoles.Secretary);
    }
}
