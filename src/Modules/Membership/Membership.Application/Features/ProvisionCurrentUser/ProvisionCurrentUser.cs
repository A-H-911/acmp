using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Internal;
using Acmp.Modules.Membership.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.ProvisionCurrentUser;

// Just-in-time provisioning of the caller's local profile from Keycloak claims (ADR-0004). The SPA
// calls this on login (POST /api/members/me). Authenticated + a resolvable canonical role only;
// a validated identity with no recognised role is denied (AC-003 deny path, post-authentication).
public sealed record ProvisionCurrentUserCommand : IRequest<MemberProfileDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed record MemberProfileDto(
    Guid PublicId, string FullName, string Email, string Role, IReadOnlyList<string> Roles, bool IsVotingEligible);

public sealed class ProvisionCurrentUserHandler : IRequestHandler<ProvisionCurrentUserCommand, MemberProfileDto>
{
    private readonly IMembershipDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public ProvisionCurrentUserHandler(IMembershipDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<MemberProfileDto> Handle(ProvisionCurrentUserCommand request, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || string.IsNullOrEmpty(_user.UserId))
            throw new UnauthorizedAccessException("Authentication required.");

        var role = CommitteeRoleResolver.PrimaryRole(_user.Roles)
            ?? throw new ForbiddenAccessException("No committee role is assigned to this account.");

        var sub = _user.UserId!;
        var displayName = _user.DisplayName ?? _user.UserName ?? _user.Email ?? sub;
        var email = _user.Email ?? string.Empty;

        var member = await _db.Members.FirstOrDefaultAsync(m => m.KeycloakUserId == sub, ct);
        var created = member is null;
        if (member is null)
        {
            member = CommitteeMember.Provision(sub, displayName, email, role, _clock.UtcNow);
            _db.Members.Add(member);
        }
        else
        {
            member.SyncFromClaims(displayName, email, role);
        }

        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync(created ? "Membership.MemberProvisioned" : "Membership.ProfileSynced", sub,
            new { member.PublicId, role = member.Role.ToString() }, ct);

        return new MemberProfileDto(
            member.PublicId, member.FullName, member.Email, member.Role.ToString(),
            _user.Roles.ToArray(), member.IsVotingEligible);
    }
}
