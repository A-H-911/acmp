using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.DeactivateMember;

// Administrator deactivates a member (AC-058). Blocks ACMP access while the record + all historical
// attribution remain intact. Only Administrator manages users (docs/10 row 27, SoD-5).
public sealed record DeactivateMemberCommand(Guid MemberPublicId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { nameof(CommitteeRole.Administrator) };
}

public sealed class DeactivateMemberValidator : AbstractValidator<DeactivateMemberCommand>
{
    public DeactivateMemberValidator() => RuleFor(x => x.MemberPublicId).NotEmpty();
}

public sealed class DeactivateMemberHandler : IRequestHandler<DeactivateMemberCommand>
{
    private readonly IMembershipDbContext _db;

    public DeactivateMemberHandler(IMembershipDbContext db) => _db = db;

    public async Task Handle(DeactivateMemberCommand request, CancellationToken ct)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.PublicId == request.MemberPublicId, ct)
            ?? throw new KeyNotFoundException("Committee member not found.");
        member.Deactivate();
        await _db.SaveChangesAsync(ct);
    }
}
