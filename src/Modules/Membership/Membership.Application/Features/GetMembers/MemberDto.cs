namespace Acmp.Modules.Membership.Application.Features.GetMembers;

public sealed record MemberDto(Guid PublicId, string FullName, string Email, string Role, bool IsActive);
