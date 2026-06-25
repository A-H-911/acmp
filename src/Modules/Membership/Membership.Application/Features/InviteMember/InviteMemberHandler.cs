using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.InviteMember;

public sealed class InviteMemberHandler : IRequestHandler<InviteMemberCommand, InviteMemberResponse>
{
    private readonly IMembershipDbContext _db;
    private readonly IClock _clock;

    public InviteMemberHandler(IMembershipDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<InviteMemberResponse> Handle(InviteMemberCommand request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Members.AnyAsync(m => m.Email == email || m.KeycloakUserId == request.KeycloakUserId, ct);
        if (exists)
            throw new InvalidOperationException("A committee member with this email or Keycloak id already exists.");

        var member = CommitteeMember.Invite(request.KeycloakUserId, request.FullName, request.Email, request.Role, _clock.UtcNow);
        _db.Members.Add(member);
        await _db.SaveChangesAsync(ct);

        return new InviteMemberResponse(member.Id, member.PublicId);
    }
}
