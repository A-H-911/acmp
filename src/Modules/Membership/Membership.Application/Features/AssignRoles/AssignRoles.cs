using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Internal;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.AssignRoles;

// FR-157 / AC-089 / AC-090 — Administrator or Secretary assigns committee roles from inside ACMP.
//
// ⚠ THIS IS A DELIBERATE DEVIATION FROM THE DESIGN REFERENCE, recorded in DEC-038 and ADR-0038. The
// design note reads "an invitation provisions the account in Keycloak; the role is assigned there
// and synced read-only", and that note was doing real security work: AN APP THAT CANNOT WRITE ROLES
// CANNOT ESCALATE PRIVILEGE. Writing them puts that risk back, and the guards below are what replace
// the structural guarantee. They are therefore not defensive polish — they ARE the control, and each
// one has a test that forces its refusal (AC-089).
//
// The role still reaches the app as a claims-derived cache: this writes the mapping to KEYCLOAK, the
// source of truth, and the local CommitteeMember.Role refreshes on the member's next login. That is
// why the forced sign-out below is part of the operation rather than a nicety.
public sealed record AssignRolesCommand(Guid MemberPublicId, IReadOnlyCollection<string> Roles, bool ConfirmedPrivileged = false)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { nameof(CommitteeRole.Administrator), nameof(CommitteeRole.Secretary) };
}

public sealed class AssignRolesValidator : AbstractValidator<AssignRolesCommand>
{
    public AssignRolesValidator()
    {
        RuleFor(x => x.MemberPublicId).NotEmpty();
        RuleFor(x => x.Roles).NotEmpty();
        RuleForEach(x => x.Roles)
            .Must(r => Enum.TryParse<CommitteeRole>(r, ignoreCase: false, out _))
            .WithMessage("'{PropertyValue}' is not a committee role.");
    }
}

public sealed class AssignRolesHandler : IRequestHandler<AssignRolesCommand>
{
    // Granting either of these is a separate, explicitly confirmed step (guard 2) rather than one
    // option in a dropdown beside Member — cheap, and it stops a mis-click from creating an admin.
    private static readonly string[] PrivilegedRoles =
        { nameof(CommitteeRole.Administrator), nameof(CommitteeRole.Chairman) };

    private readonly IMembershipDbContext _db;
    private readonly IIdentityProvider _identity;
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;

    public AssignRolesHandler(IMembershipDbContext db, IIdentityProvider identity, ICurrentUser user, IAuditSink audit)
    {
        _db = db;
        _identity = identity;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(AssignRolesCommand request, CancellationToken ct)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.PublicId == request.MemberPublicId, ct)
            ?? throw new KeyNotFoundException("Committee member not found.");

        // GUARD 1 — nobody changes their own roles. The simplest self-escalation, and no legitimate
        // workflow needs it: another administrator can always do it.
        if (string.Equals(member.KeycloakUserId, _user.UserId, StringComparison.Ordinal))
            throw new ForbiddenAccessException("You cannot change your own roles.");

        // GUARD 2 — granting Administrator or Chairman needs an explicit confirmation.
        var privileged = request.Roles.Where(r => PrivilegedRoles.Contains(r, StringComparer.Ordinal)).ToArray();
        if (privileged.Length > 0 && !request.ConfirmedPrivileged)
            throw new InvalidOperationException(
                $"Granting {string.Join(" and ", privileged)} must be confirmed explicitly.");

        // GUARD 3 — the committee may never be left with zero Administrators. Without this, one bad
        // edit locks everyone out of user management permanently and recovery means going into the
        // Keycloak console by hand — the very thing this feature exists to avoid.
        var losingAdmin = member.Role == CommitteeRole.Administrator
                          && !request.Roles.Contains(nameof(CommitteeRole.Administrator), StringComparer.Ordinal);
        if (losingAdmin)
        {
            var otherAdmins = await _db.Members.CountAsync(
                m => m.Role == CommitteeRole.Administrator
                     && m.Status == MembershipStatus.Active
                     && m.PublicId != member.PublicId, ct);
            if (otherAdmins == 0)
                throw new InvalidOperationException("The committee would be left with no Administrator.");
        }

        // Keycloak is the source of truth, so it is written FIRST. If it refuses, nothing local moved.
        await _identity.SetRealmRolesAsync(member.KeycloakUserId, request.Roles, ct);

        // Mirror the new primary role onto the local claims cache, using the SAME lowest-enum-wins
        // rule the token path uses — so the cached value matches what the next login would compute
        // rather than a second, divergent interpretation of the same role set.
        var primary = CommitteeRoleResolver.PrimaryRole(request.Roles)
            ?? throw new InvalidOperationException("No recognised committee role in the requested set.");
        member.ApplyAssignedRole(primary);
        await _db.SaveChangesAsync(ct);

        // AC-090 — take effect NOW. Roles reach the app through the token, so a REMOVED role would
        // otherwise stay usable until the 60-minute idle timeout.
        await _identity.SignOutEverywhereAsync(member.KeycloakUserId, ct);

        // GUARD 4 — the actual control for the residual SoD risk DEC-038 records the operator
        // accepting, and it is DETECTION not prevention, so it has to be real. before/after are
        // drained from the EF capture buffer by (subjectType, subjectId), which is why the local
        // write above happens BEFORE this call — without it the row would record that a role changed
        // while being unable to say from what. Asserted by a test that checks the ROW EXISTS after a
        // real change, never that a handler was called (AC-093).
        await _audit.EmitEnrichedAsync(
            "Membership.RolesAssigned", nameof(CommitteeMember), member.PublicId.ToString(), ct: ct);
    }
}
