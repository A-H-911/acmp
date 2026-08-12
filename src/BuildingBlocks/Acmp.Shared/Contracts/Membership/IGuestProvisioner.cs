namespace Acmp.Shared.Contracts.Membership;

// Cross-module WRITE port (ADR-0021's sanctioned pattern; ADR-0040 decision 1): the Meetings module
// invites a guest presenter without reading Membership's tables and without acquiring a Keycloak
// dependency of its own. Implemented in Membership.Infrastructure over the Membership store and the
// module's IIdentityProvider — the same shape as ITraceabilityWriter, and the mirror image of the
// read seams (ICommitteeDirectory, IMeetingQuorumSource).
//
// WHY MEETINGS CALLS MEMBERSHIP AND NOT THE REVERSE: the window comes from Meeting.ScheduledEnd and
// the slot is an AgendaItem, both Meetings-owned, so hosting the use case in Meetings means it reads
// its own aggregate and needs exactly ONE crossing. Membership hosting it would need two — a read
// port for ScheduledEnd and a write port for the presenter assignment.
//
// UNAUTHORIZED AT THE PORT, per ADR-0021: it carries no RBAC of its own because the CALLING action is
// separately authorized (FR-159 puts the invite on the Secretary). A port that re-checked would be a
// second place for the answer to live, and two places drift.

/// <summary>A guest just provisioned in both Keycloak and the ACMP member store.</summary>
/// <param name="TemporaryPassword">Revealed ONCE to the inviter and stored nowhere (AC-088).</param>
public sealed record ProvisionedGuest(Guid PublicId, string FullName, string Email, string TemporaryPassword);

public interface IGuestProvisioner
{
    /// <summary>
    /// Creates the identity-provider account and the local <c>Invited</c> member at role Guest, whose
    /// access ends at <paramref name="accessExpiresAt"/>.
    /// </summary>
    /// <remarks>
    /// The window is PASSED IN rather than computed here, because only the module that owns the
    /// meeting knows when the meeting ends (ADR-0001). It lands in one stored column, which is the
    /// single value the per-request refusal, the expiry sweep and the /session banner all read —
    /// DEC-037 requires that they cannot disagree, and one column is the only structural guarantee.
    /// </remarks>
    Task<ProvisionedGuest> InviteGuestAsync(
        string email, string fullName, DateTimeOffset accessExpiresAt, CancellationToken ct = default);
}
