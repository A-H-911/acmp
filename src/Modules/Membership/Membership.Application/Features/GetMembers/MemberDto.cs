namespace Acmp.Modules.Membership.Application.Features.GetMembers;

// Directory row (AC-059). Role is the claims-derived cache; Status is the ACMP-managed lifecycle.
// KeycloakUserId (the OIDC subject) is exposed so committee UIs can assign work to a member by their
// stable identity — e.g. the Actions "Owner" select sends it as OwnerUserId (P8b2b). Committee-wide
// readable, like the rest of the directory; not sensitive for a single ≤20-user on-prem committee.
public sealed record MemberDto(
    Guid PublicId,
    string KeycloakUserId,
    string FullName,
    string Email,
    string Role,
    string Status,
    bool IsActive,
    bool IsVotingEligible,
    IReadOnlyList<StreamRefDto> Streams);

public sealed record StreamRefDto(Guid PublicId, string Code, string NameEn, string NameAr);
