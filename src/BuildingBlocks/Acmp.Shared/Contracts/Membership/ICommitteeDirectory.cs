namespace Acmp.Shared.Contracts.Membership;

// Cross-module seam (ADR-0001): other modules (e.g. Meetings) resolve "the committee roster" without
// reading Membership's tables. Implemented in Membership.Infrastructure against the Membership DbContext
// (mirrors how Topics implements ITopicScheduler and Membership implements the ABAC ports). Returns only
// ACTIVE members — disabled members are access-blocked (AC-058) so they receive no notifications.
public interface ICommitteeDirectory
{
    Task<IReadOnlyCollection<CommitteeRecipient>> GetActiveMembersAsync(CancellationToken ct = default);

    // Active members holding a specific global role (role-name = Acmp.Shared.Authorization.AcmpRoles.*),
    // for HEADLESS recipient resolution (e.g. the overdue-escalation sweep copying the Secretary/Chairman —
    // no HTTP context, so the "who is the Secretary" answer must come from the roster). Empty when nobody
    // currently holds the role; callers must tolerate no recipients.
    Task<IReadOnlyCollection<CommitteeRecipient>> GetActiveMembersInRoleAsync(string role, CancellationToken ct = default);

    // Display names for the given Keycloak subjects, INCLUDING DISABLED MEMBERS — deliberately unlike the
    // two methods above.
    //
    // Those exclude disabled members because they are access-blocked and receive no notifications (AC-058).
    // The audit log is the opposite case: AC-058 keeps a disabled member's record precisely so historical
    // attribution survives (DEF-029 — disable, never delete, because deleting orphans the rows forever).
    // Resolving an old event's actor through an active-only lookup would therefore render a raw GUID for
    // exactly the departed-member entries that matter most to a reviewer.
    //
    // Returns a map keyed by Keycloak subject; ids with no member row are simply absent, so callers must
    // fall back to the raw id rather than assume a hit (system and integration actors have no member row).
    Task<IReadOnlyDictionary<string, string>> ResolveDisplayNamesAsync(
        IReadOnlyCollection<string> userIds, CancellationToken ct = default);
}

// A notification recipient: the Keycloak subject (matches NotificationMessage.RecipientUserId and
// ICurrentUser.UserId) plus a display name for any caller that needs it.
public sealed record CommitteeRecipient(string UserId, string FullName);
