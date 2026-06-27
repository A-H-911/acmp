namespace Acmp.Shared.Contracts.Membership;

// Cross-module seam (ADR-0001): other modules (e.g. Meetings) resolve "the committee roster" without
// reading Membership's tables. Implemented in Membership.Infrastructure against the Membership DbContext
// (mirrors how Topics implements ITopicScheduler and Membership implements the ABAC ports). Returns only
// ACTIVE members — disabled members are access-blocked (AC-058) so they receive no notifications.
public interface ICommitteeDirectory
{
    Task<IReadOnlyCollection<CommitteeRecipient>> GetActiveMembersAsync(CancellationToken ct = default);
}

// A notification recipient: the Keycloak subject (matches NotificationMessage.RecipientUserId and
// ICurrentUser.UserId) plus a display name for any caller that needs it.
public sealed record CommitteeRecipient(string UserId, string FullName);
