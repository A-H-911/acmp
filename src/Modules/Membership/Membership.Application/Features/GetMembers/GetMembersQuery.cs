using Acmp.Shared.Application.Abstractions;
using MediatR;

namespace Acmp.Modules.Membership.Application.Features.GetMembers;

// Member directory. Readable by any authenticated user regardless of role (AC-059), so
// AllowedRoles is empty and the AuthorizationBehavior only requires authentication.
public sealed record GetMembersQuery(bool IncludeInactive = false)
    : IRequest<IReadOnlyList<MemberDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}
