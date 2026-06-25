namespace Acmp.Modules.Membership.Domain.Enums;

// Canonical global roles (README section C). Sourced from Keycloak group/realm-role claims
// (ADR-0004) and mapped to these; ACMP does not assign roles itself.
public enum CommitteeRole
{
    Chairman = 0,
    Secretary = 1,
    Member = 2,
    Reviewer = 3,
    Auditor = 4,
    Administrator = 5,
    Submitter = 6,
    GuestPresenter = 7
}
