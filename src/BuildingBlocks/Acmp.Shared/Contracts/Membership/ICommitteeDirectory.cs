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

    /// <summary>
    /// Who a Keycloak subject is on the ACMP side, or null when no member row exists.
    /// </summary>
    /// <remarks>
    /// FR-159 / AC-092. A caller knows WHO is asking as a Keycloak subject (that is what the token
    /// carries), but everything else in the system identifies a person by PublicId — including
    /// <c>AgendaItem.PresenterUserId</c>. Without this, /session could not find the caller's own slot
    /// without Meetings reading Membership's table.
    ///
    /// INCLUDES DISABLED AND INVITED MEMBERS, like ResolveDisplayNamesAsync and unlike the two active-only
    /// lookups above: a guest presenter is <c>Invited</c> until their first login, so an active-only
    /// answer would hide their own slot from them on the one visit that matters most.
    /// </remarks>
    Task<CommitteeMemberRef?> ResolveMemberAsync(string keycloakUserId, CancellationToken ct = default);

    /// <summary>
    /// Who a PublicId is, or null when no member row carries it.
    /// </summary>
    /// <remarks>
    /// FR-165 / DEC-086 d1 — the presenter preview. This is <see cref="ResolveMemberAsync"/>'s question
    /// asked from the other end. That one answers "who is the CALLER", because a token carries a Keycloak
    /// subject. The preview never starts from a token: a Chairman or Secretary names an agenda slot, the
    /// slot carries <c>AgendaItem.PresenterUserId</c>, and that is a PublicId. So this is the only way to
    /// reach the TARGETED person's access window without Meetings reading Membership's tables (ADR-0001).
    ///
    /// ⚠ INCLUDES DISABLED AND INVITED MEMBERS, exactly like <see cref="ResolveMemberAsync"/> and unlike the
    /// two active-only lookups above — and here the Invited case is not an edge case but the MAIN one. A
    /// guest presenter is <c>Invited</c> until their first login, and the whole point of the preview is to
    /// check their view BEFORE the meeting, which is before that login. An active-only answer would return
    /// null for precisely the population being previewed.
    ///
    /// The returned <c>AccessExpiresAt</c> is the TARGET's window, not the caller's. That is the difference
    /// that makes the preview worth having: a Chairman's own access does not expire, so a preview rendered
    /// from the caller's row would show a banner no presenter will ever see.
    /// </remarks>
    Task<CommitteeMemberRef?> ResolveMemberByPublicIdAsync(Guid publicId, CancellationToken ct = default);
}

/// <summary>Who a subject is here: the id everything else identifies them by, and when their access ends.</summary>
/// <param name="AccessExpiresAt">
/// Null for every ordinary member. For a guest presenter it is THE value the API refusal
/// (ADR-0039) and the expiry sweep read, so a banner rendered from it cannot disagree with the server
/// — DEC-037 requires exactly that, and one column is the only structural way to guarantee it.
/// </param>
public sealed record CommitteeMemberRef(Guid PublicId, DateTimeOffset? AccessExpiresAt);

// A notification recipient: the Keycloak subject (matches NotificationMessage.RecipientUserId and
// ICurrentUser.UserId) plus a display name for any caller that needs it.
public sealed record CommitteeRecipient(string UserId, string FullName);
