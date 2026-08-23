using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.SetVotingEligibility;

// DEF-041 / DEC-046 d4 — who may vote is an ACMP-managed attribute, and until now nothing could
// change it. CommitteeMember.SetVotingEligibility has existed in the domain since P4 with no caller:
// the directory drew a switch the design intends to be operable, and the app rendered it as an inert
// span. This is the missing half.
//
// CHAIRMAN OR SECRETARY, AND ADMINISTRATOR IS DELIBERATELY EXCLUDED (DEC-046 d4). That is not
// seniority reasoning: SoD-5 structurally excludes Administrator from committee CONTENT, and who is
// eligible to vote is the most content-like decision on the roster — giving the platform
// administrator influence over the composition of a quorum would cross it and would have needed its
// own ADR. Secretary is included so the roster can be maintained without pulling in the Chairman for
// routine changes, which is how the matrix grants most governance writes.
//
// ⚠ NO PER-ENDPOINT POLICY, AND THAT IS MEASURED RATHER THAN ASSUMED. AuthorizationBehavior enforces
// AllowedRoles for every IAuthorizedRequest, which was confirmed this session by deleting an endpoint
// policy and watching all nine of its API tests stay green. The invite and role-assignment endpoints
// already rely on exactly this. Adding a policy here would need a new row in the permission matrix
// and would ship a 403 nobody audits (DEF-056), for a gate that is already enforced.
public sealed record SetVotingEligibilityCommand(Guid MemberPublicId, bool IsVotingEligible)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { nameof(CommitteeRole.Chairman), nameof(CommitteeRole.Secretary) };
}

public sealed class SetVotingEligibilityValidator : AbstractValidator<SetVotingEligibilityCommand>
{
    public SetVotingEligibilityValidator() => RuleFor(x => x.MemberPublicId).NotEmpty();
}

public sealed class SetVotingEligibilityHandler : IRequestHandler<SetVotingEligibilityCommand>
{
    private readonly IMembershipDbContext _db;
    private readonly IAuditSink _audit;

    public SetVotingEligibilityHandler(IMembershipDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task Handle(SetVotingEligibilityCommand request, CancellationToken ct)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.PublicId == request.MemberPublicId, ct)
            ?? throw new KeyNotFoundException("Committee member not found.");

        // ⚠ A DISABLED MEMBER IS REFUSED. Deactivation blocks ACMP access while keeping the record so
        // votes and authorship stay attributed (AC-058), so a disabled row is HISTORY. Making one
        // eligible to vote would put someone who cannot sign in into the quorum arithmetic, where
        // they would count toward a threshold nobody can meet.
        if (!member.IsActive)
            throw new InvalidOperationException(
                "Voting eligibility cannot be changed for a member who is not active; reactivate them first.");

        member.SetVotingEligibility(request.IsVotingEligible);
        await _db.SaveChangesAsync(ct);

        // AC-093 wants "what changed from and to", and before/after are drained from the EF capture
        // buffer by (subjectType, subjectId) — so the SaveChanges above is what gives this row its
        // values. Emitting before the save would produce an audit entry that names a change without
        // being able to say what it was.
        await _audit.EmitEnrichedAsync("Membership.VotingEligibilityChanged", nameof(CommitteeMember),
            request.MemberPublicId.ToString(), ct: ct);
    }
}
