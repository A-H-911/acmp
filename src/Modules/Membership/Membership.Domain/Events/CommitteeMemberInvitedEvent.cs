using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Membership.Domain.Events;

// Raised when a member is provisioned by invitation (no self-registration, R-06).
public sealed record CommitteeMemberInvitedEvent(Guid MemberPublicId, string Email, DateTimeOffset OccurredOn)
    : IDomainEvent;
