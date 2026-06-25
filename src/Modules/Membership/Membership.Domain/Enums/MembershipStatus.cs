namespace Acmp.Modules.Membership.Domain.Enums;

// ACMP-managed membership lifecycle (distinct from the Keycloak account). Active is set on first
// authenticated login (JIT provisioning, ADR-0004); Disabled blocks ACMP access while keeping all
// historical attribution (AC-058). Invited is reserved for admin pre-registration ahead of first
// login — P4 produces Active/Disabled; the directory renders all three.
public enum MembershipStatus
{
    Active = 0,
    Invited = 1,
    Disabled = 2,
}
