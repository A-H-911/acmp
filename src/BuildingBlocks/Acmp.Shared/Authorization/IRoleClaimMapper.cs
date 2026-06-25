namespace Acmp.Shared.Authorization;

// Translates raw Keycloak role/group claim values into the distinct canonical ACMP role names
// they denote (AcmpRoles.*). ACMP trusts the claim — it never assigns roles itself (ADR-0004).
public interface IRoleClaimMapper
{
    IReadOnlyCollection<string> Map(IEnumerable<string> rawClaims);
}
