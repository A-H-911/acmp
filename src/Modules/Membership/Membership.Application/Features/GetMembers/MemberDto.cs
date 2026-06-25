namespace Acmp.Modules.Membership.Application.Features.GetMembers;

// Directory row (AC-059). Role is the claims-derived cache; Status is the ACMP-managed lifecycle.
public sealed record MemberDto(
    Guid PublicId,
    string FullName,
    string Email,
    string Role,
    string Status,
    bool IsActive,
    bool IsVotingEligible,
    IReadOnlyList<StreamRefDto> Streams);

public sealed record StreamRefDto(Guid PublicId, string Code, string NameEn, string NameAr);
