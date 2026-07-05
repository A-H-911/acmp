namespace Acmp.Modules.Membership.Domain.Enums;

// Canonical global roles (README section C). Sourced from Keycloak group/realm-role claims
// (ADR-0004) and mapped to these; ACMP does not assign roles itself. Member names match the
// canonical role-name strings (Acmp.Shared.Authorization.AcmpRoles) via nameof so the enum and
// the authorization vocabulary never drift. Guest = Guest/Presenter; the Presenter ability is the
// per-topic relationship (docs/domain/permission-role-matrix.md §D), not a separate global role.
public enum CommitteeRole
{
    Chairman = 0,
    Secretary = 1,
    Member = 2,
    Reviewer = 3,
    Auditor = 4,
    Administrator = 5,
    Submitter = 6,
    Guest = 7
}
