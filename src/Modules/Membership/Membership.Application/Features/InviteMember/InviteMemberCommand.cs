using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using MediatR;

namespace Acmp.Modules.Membership.Application.Features.InviteMember;

// Provision a committee member by invitation. Only Administrator/Secretary may do so (R-06).
public sealed record InviteMemberCommand(string KeycloakUserId, string FullName, string Email, CommitteeRole Role)
    : IRequest<InviteMemberResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { nameof(CommitteeRole.Administrator), nameof(CommitteeRole.Secretary) };
}
