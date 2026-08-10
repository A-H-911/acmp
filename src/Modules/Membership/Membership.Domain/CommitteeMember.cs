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
