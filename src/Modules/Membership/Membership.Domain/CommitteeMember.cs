using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Domain.Events;
using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Membership.Domain;

// A member of the single Architecture Committee. Identity is federated to Keycloak
// (KeycloakUserId = the OIDC subject) and the local record is provisioned just-in-time on first
// login (ADR-0004). Role is a CLAIMS-DERIVED CACHE refreshed each login — never set by an admin.
// Stream assignments, voting eligibility, and active/disabled status are ACMP-managed here.
// Deactivation never deletes: historical attribution is preserved (AC-058).
public sealed class CommitteeMember : AuditableEntity
{
    private readonly List<MemberStreamAssignment> _streams = new();

    private CommitteeMember() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → API 409 (docs/domain/data-architecture.md §1.5, ADR-0018).
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string KeycloakUserId { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public CommitteeRole Role { get; private set; }
    public MembershipStatus Status { get; private set; }
    public bool IsVotingEligible { get; private set; }

    public IReadOnlyCollection<MemberStreamAssignment> Streams => _streams.AsReadOnly();
    public bool IsActive => Status == MembershipStatus.Active;

    // JIT provisioning of the local profile on first authenticated login. Identity + role come from
    // Keycloak; ACMP creates only the display record and its managed attributes.
    public static CommitteeMember Provision(string keycloakUserId, string fullName, string email, CommitteeRole role, DateTimeOffset now)
    {
        var member = new CommitteeMember
        {
            KeycloakUserId = keycloakUserId.Trim(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            Status = MembershipStatus.Active,
            IsVotingEligible = DefaultVotingEligibility(role),
        };
        member.Raise(new CommitteeMemberProvisionedEvent(member.PublicId, member.Email, now));
        return member;
    }

    // Admin pre-registration ahead of first login (ADR-0038 / SC-003), which is exactly what
    // MembershipStatus.Invited was reserved for. The caller has ALREADY created the Keycloak account
    // and passes back the subject id it returned, so unlike a blind pre-seed there is nothing to
    // reconcile later — the identity is known at creation and SyncFromClaims flips the row to Active
    // on first login through the existing path.
    //
    // Role is still NOT an admin-set field here: it arrives as the claims-derived cache's initial
    // value, matching whatever realm roles were granted in Keycloak, and every later login refreshes
    // it. Nothing about the claims-are-the-truth rule changes.
    public static CommitteeMember PreRegister(string keycloakUserId, string fullName, string email, CommitteeRole role, DateTimeOffset now)
    {
        var member = new CommitteeMember
        {
            KeycloakUserId = keycloakUserId.Trim(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            Status = MembershipStatus.Invited,
            IsVotingEligible = DefaultVotingEligibility(role),
        };
        member.Raise(new CommitteeMemberProvisionedEvent(member.PublicId, member.Email, now));
        return member;
    }

    // Refresh claims-derived fields on each login. Never touches ACMP-managed attributes (status,
    // voting eligibility, streams). A login on a pre-registered Invited record flips it to Active.
    //
    // RETURNS WHETHER ANYTHING ACTUALLY CHANGED, and the caller audits only when it did. The audit
    // chain is hash-chained and append-only (INV-005), so a no-op event is not merely noise — it can
    // never be removed (DEF-029's asymmetry). The SPA calls this endpoint once per app mount, which
    // means every full page load, refresh and login previously wrote a `Membership.ProfileSynced`
    // row describing no change at all; a browsing session produced 14 of them in one minute. Left
    // alone, real governance events end up buried in permanent no-op traffic.
    //
    // The comparison happens BEFORE assignment because assigning first makes every field equal to
    // itself and the answer is always "unchanged" — the same shape as a baseline captured after the
    // change it was meant to detect.
    public bool SyncFromClaims(string fullName, string email, CommitteeRole role)
    {
        var name = fullName.Trim();
        var normalizedEmail = email.Trim().ToLowerInvariant();
        // Invited -> Active is a REAL state transition and must stay auditable even when the name,
        // email and role are all identical to what is already stored.
        var activating = Status == MembershipStatus.Invited;

        var changed = !string.Equals(FullName, name, StringComparison.Ordinal)
                      || !string.Equals(Email, normalizedEmail, StringComparison.Ordinal)
                      || Role != role
                      || activating;

        FullName = name;
        Email = normalizedEmail;
        Role = role;
        if (activating)
            Status = MembershipStatus.Active;

        return changed;
    }

    // Mirrors an in-app role assignment that has ALREADY been written to Keycloak (FR-157, ADR-0038).
    //
    // The type comment above still holds — role remains a claims-derived cache and the next login
    // re-syncs it through SyncFromClaims. This seeds that cache with exactly the value the next token
    // will carry, and it does so for two concrete reasons rather than convenience:
    //   1. AUDIT. before/after are drained from the EF capture buffer by (subjectType, subjectId), so
    //      an operation that changes nothing locally produces an audit row with NO values — and
    //      AC-093 requires "what changed from and to". Without this the record would say a role
    //      changed while being unable to say from what.
    //   2. The roster would otherwise show the OLD role until that member next signed in, which reads
    //      as the assignment having silently failed.
    // Safe because the caller forces sign-out in the same operation, so the stale token cannot outlive it.
    // ADR-0039: stamping the CHANGE TIME is what makes AC-090 literally true. Authorization reads
    // roles from the TOKEN (CurrentUserService.IsInRole -> ClaimsPrincipal), so without this an
    // already-issued token keeps its old roles for up to a full access-token lifetime — measured at
    // 300s on the live realm — and a forced Keycloak sign-out cannot revoke a token in flight.
    // Comparing this against the token's `iat` lets the API refuse a token minted BEFORE the change.
    //
    // `now` is passed in rather than read here: a domain object that reads the clock cannot be
    // tested at the boundary, and the boundary is exactly where this has to be right.
    public void ApplyAssignedRole(CommitteeRole role, DateTimeOffset now)
    {
        Role = role;
        RolesChangedAt = now;
    }

    /// <summary>
    /// When this member's roles last changed in ACMP (ADR-0039). Null for a member whose roles have
    /// never been assigned through the app — such a member's tokens are all "after" the last change,
    /// so the check passes, which is the correct default rather than a special case.
    /// </summary>
    public DateTimeOffset? RolesChangedAt { get; private set; }

    /// <summary>
    /// When this member's access ends (ADR-0039 / AC-092). Null = no expiry, which is every ordinary
    /// committee member. Set for a guest presenter, and it is the SINGLE value both the API and the
    /// /session banner read — DEC-037 requires that they cannot disagree, and one column is the only
    /// way to guarantee it.
    /// </summary>
    public DateTimeOffset? AccessExpiresAt { get; private set; }

    /// <summary>Grants time-boxed access (FR-159). Passing null clears any expiry.</summary>
    public void SetAccessWindow(DateTimeOffset? expiresAt) => AccessExpiresAt = expiresAt;

    /// <summary>
    /// True when the member's access window has closed. Exclusive of the instant itself: at exactly
    /// `expiresAt` access has not yet expired, which matches "expires AFTER the meeting" and gives
    /// the boundary test an unambiguous answer.
    /// </summary>
    public bool HasExpired(DateTimeOffset now) => AccessExpiresAt is { } end && now > end;

    // AC-058: blocks ACMP access but keeps the record so votes/authorship/assignments stay attributed.
    public void Deactivate() => Status = MembershipStatus.Disabled;

    public void Reactivate() => Status = MembershipStatus.Active;

    public void SetVotingEligibility(bool eligible) => IsVotingEligible = eligible;

    // Replaces the member's stream assignments (docs/domain/permission-role-matrix.md §E.1). Idempotent on duplicates.
    public void AssignStreams(IEnumerable<long> streamIds)
    {
        _streams.Clear();
        foreach (var id in streamIds.Distinct())
            _streams.Add(new MemberStreamAssignment(id));
    }

    // Vote casting is Chairman/Member only (docs/domain/permission-role-matrix.md §C row 11); seed eligibility accordingly.
    private static bool DefaultVotingEligibility(CommitteeRole role) =>
        role is CommitteeRole.Chairman or CommitteeRole.Member;
}
