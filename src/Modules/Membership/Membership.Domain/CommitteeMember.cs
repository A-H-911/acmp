using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Domain.Events;
using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Membership.Domain;

// A member of the single Architecture Committee. Identity is federated to Keycloak
// (KeycloakUserId = the OIDC subject). Deactivation never deletes: historical attribution
// is preserved (AC-058).
public sealed class CommitteeMember : AuditableEntity
{
    private CommitteeMember() { }

    public string KeycloakUserId { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public CommitteeRole Role { get; private set; }
    public bool IsActive { get; private set; }

    public static CommitteeMember Invite(string keycloakUserId, string fullName, string email, CommitteeRole role, DateTimeOffset now)
    {
        var member = new CommitteeMember
        {
            KeycloakUserId = keycloakUserId.Trim(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            IsActive = true
        };
        member.Raise(new CommitteeMemberInvitedEvent(member.PublicId, member.Email, now));
        return member;
    }

    public void Deactivate() => IsActive = false;
}
