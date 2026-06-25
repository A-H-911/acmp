using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Membership.Domain.Events;

// Raised when a member's local profile is first provisioned just-in-time from Keycloak claims on
// login (ADR-0004 — no self-registration; ACMP creates the display record, not the identity).
public sealed record CommitteeMemberProvisionedEvent(Guid MemberPublicId, string Email, DateTimeOffset OccurredOn)
    : IDomainEvent;
