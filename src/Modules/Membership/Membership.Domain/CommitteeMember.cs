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

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → API 409 (docs/16 §1.5, ADR-0018).
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
    public void SyncFromClaims(string fullName, string email, CommitteeRole role)
    {
        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Role = role;
        if (Status == MembershipStatus.Invited)
            Status = MembershipStatus.Active;
    }

    // AC-058: blocks ACMP access but keeps the record so votes/authorship/assignments stay attributed.
    public void Deactivate() => Status = MembershipStatus.Disabled;

    public void Reactivate() => Status = MembershipStatus.Active;

    public void SetVotingEligibility(bool eligible) => IsVotingEligible = eligible;

    // Replaces the member's stream assignments (docs/10 §E.1). Idempotent on duplicates.
    public void AssignStreams(IEnumerable<long> streamIds)
    {
        _streams.Clear();
        foreach (var id in streamIds.Distinct())
            _streams.Add(new MemberStreamAssignment(id));
    }

    // Vote casting is Chairman/Member only (docs/10 §C row 11); seed eligibility accordingly.
    private static bool DefaultVotingEligibility(CommitteeRole role) =>
        role is CommitteeRole.Chairman or CommitteeRole.Member;
}
