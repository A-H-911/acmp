using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.ReactivateMember;

// FR-162 / AC-111 — the counterpart of DeactivateMember, and the fix for DEF-085: without it a
// disabled member was locked out permanently, because re-inviting throws on the duplicate email
// (MemberInvitation.InviteAsync) and deleting the Keycloak user strands the member row forever.
//
// Only Administrator manages users (permission-role-matrix row 27, SoD-5), same as deactivate.
//
// ⚠ AccessExpiresAt IS PART OF THE COMMAND, NOT AN AFTERTHOUGHT. PrincipalRevalidator refuses a
// member whose window has passed INDEPENDENTLY of Status, so flipping Status alone produces an
// Active-but-refused member — a state that reads as repaired and is not. The window is therefore
// ALWAYS written: pass null to clear it (re-admitting a guest as an ordinary member), or a new
// instant to re-window them for another meeting. There is no code path that leaves it stale.
public sealed record ReactivateMemberCommand(Guid MemberPublicId, DateTimeOffset? AccessExpiresAt = null)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { nameof(CommitteeRole.Administrator) };
}

public sealed class ReactivateMemberValidator : AbstractValidator<ReactivateMemberCommand>
{
    public ReactivateMemberValidator() => RuleFor(x => x.MemberPublicId).NotEmpty();
}

public sealed class ReactivateMemberHandler : IRequestHandler<ReactivateMemberCommand>
{
    private readonly IMembershipDbContext _db;
    private readonly IAuditSink _audit;

    // IEnumerable, not the interface — IIdentityProvider is registered ONLY when the Keycloak admin
    // client is configured (ADR-0038), so demanding it would make this handler uninjectable and the
    // API unbootable wherever in-app user management is off. Exactly the shape ExpireGuestAccess
    // uses, and for exactly the same reason. Empty here means the Keycloak half is skipped.
    private readonly IEnumerable<IIdentityProvider> _identity;

    public ReactivateMemberHandler(IMembershipDbContext db, IAuditSink audit, IEnumerable<IIdentityProvider> identity)
    {
        _db = db;
        _audit = audit;
        _identity = identity;
    }

    public async Task Handle(ReactivateMemberCommand request, CancellationToken ct)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.PublicId == request.MemberPublicId, ct)
            ?? throw new KeyNotFoundException("Committee member not found.");

        // KEYCLOAK FIRST, WHICH IS THE OPPOSITE ORDER TO THE SWEEP — deliberately, because "fail
        // closed" points the other way when you are GRANTING access. The sweep disables locally
        // first so a Keycloak outage leaves access shut with the login open; here, saving locally
        // first would leave a member marked Active whose login is still dead, which is precisely the
        // state AC-111 exists to refuse. Calling Keycloak before SaveChangesAsync means a failure
        // rolls the local change back and the member stays consistently disabled, so the operation
        // is retryable rather than half-applied.
        //
        // Called unconditionally rather than only when Status is Disabled: the PUT is idempotent, it
        // costs one request on a <=20-user system, and doing it every time REPAIRS the drift left by
        // any earlier half-failed attempt instead of skipping over it.
        var identity = _identity.FirstOrDefault();
        if (identity is not null)
            await identity.EnableUserAsync(member.KeycloakUserId, ct);

        member.Reactivate();
        member.SetAccessWindow(request.AccessExpiresAt);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync(
            "Membership.MemberReactivated", nameof(CommitteeMember), member.PublicId.ToString(), ct: ct);
    }
}
