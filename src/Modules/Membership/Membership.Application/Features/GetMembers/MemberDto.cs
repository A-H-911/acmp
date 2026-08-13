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

// IsWildcard is ADR-0042 clause (3)'s "unrestricted" marker, carried to the client as a FLAG rather
// than left to be inferred from Code: the roster must show it distinctly (DEC-043) and the topic
// picker must omit it, and both would silently break if either matched on the string "all-streams".
public sealed record StreamRefDto(Guid PublicId, string Code, string NameEn, string NameAr, bool IsWildcard);
