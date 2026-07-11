using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.DeactivateMember;

// Administrator deactivates a member (AC-058). Blocks ACMP access while the record + all historical
// attribution remain intact. Only Administrator manages users (docs/domain/permission-role-matrix.md row 27, SoD-5).
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
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;

    public DeactivateMemberHandler(IMembershipDbContext db, ICurrentUser user, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(DeactivateMemberCommand request, CancellationToken ct)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.PublicId == request.MemberPublicId, ct)
            ?? throw new KeyNotFoundException("Committee member not found.");

        var before = member.Status;
        member.Deactivate();
        await _db.SaveChangesAsync(ct);

        // State change on a governed record -> audit (docs/domain/audit-and-records.md, guardrail 5). Interim sink (Serilog->Seq);
        // the immutable hash-chained AuditEvent store is BL-066.
        await _audit.EmitEnrichedAsync("Membership.MemberDeactivated", nameof(CommitteeMember), member.PublicId.ToString(), ct: ct);
    }
}
