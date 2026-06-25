namespace Acmp.Shared.Authorization;

// Config-driven Keycloak claim -> canonical role mapping (ADR-0004). Bound from
// "Authorization:RoleMapping". The mapper already understands the common Keycloak claim shapes
// (bare role, "acmp-"/"/acmp/" prefixed, group paths, the "coordinator" -> Secretary alias);
// ClaimToRole adds explicit overrides for non-standard realm naming without a code change.
public sealed class RoleMappingOptions
{
    public const string SectionName = "Authorization:RoleMapping";

    // Explicit raw-claim-value (case-insensitive) -> canonical role name (AcmpRoles.*).
    public Dictionary<string, string> ClaimToRole { get; set; } = new();

    // What to grant when a VALIDATED token carries no recognised ACMP role claim.
    // null = deny (fail-closed default, AC-003). Set to a role name (e.g. "Submitter") to assign
    // the configured minimum role instead. An AuthEvent is emitted either way.
    public string? DefaultRole { get; set; }
}
