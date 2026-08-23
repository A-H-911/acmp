using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Internal;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.InviteUser;

// FR-156 / AC-088 — Administrator or Secretary invites a user from inside ACMP, so adding a member
// no longer requires a Keycloak console session (DEC-038, ADR-0038).
//
// SECRETARY IS DELIBERATELY INCLUDED. An invited account has NO role until one is granted, so on its
// own this creates something inert: it can sign in and reach nothing. That is the whole reason the
// operator was comfortable widening it beyond Administrator.
public sealed record InviteUserCommand(string Email, string FullName, IReadOnlyList<Guid> StreamPublicIds)
    : IRequest<InvitedUserDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { nameof(CommitteeRole.Administrator), nameof(CommitteeRole.Secretary) };
}

/// <summary>
/// The created member, plus a temporary password that is revealed ONCE and never stored.
/// </summary>
/// <remarks>
/// "No email in v1" is a hard constraint, so the design's "Send invitation" resolves to showing the
/// password to the inviter to pass on. It is returned here and nowhere else: not persisted, not
/// logged, and not re-readable from any later query (AC-088).
/// </remarks>
public sealed record InvitedUserDto(Guid PublicId, string FullName, string Email, string Status, string TemporaryPassword);

public sealed class InviteUserValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(256);
        // ADR-0043 clause (2): the stream field is REQUIRED, which is what makes the fail-closed
        // posture survivable — anyone invited through the app is never unassigned, so they never
        // land in the state that refuses every write once step 7 wires the requirement.
        RuleFor(x => x.StreamPublicIds).NotEmpty()
            .WithMessage("At least one stream is required so the member can act on anything.");
    }
}

public sealed class InviteUserHandler : IRequestHandler<InviteUserCommand, InvitedUserDto>
{
    private readonly IMembershipDbContext _db;
    private readonly IIdentityProvider _identity;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public InviteUserHandler(IMembershipDbContext db, IIdentityProvider identity, IClock clock, IAuditSink audit)
    {
        _db = db;
        _identity = identity;
        _clock = clock;
        _audit = audit;
    }

    public async Task<InvitedUserDto> Handle(InviteUserCommand request, CancellationToken ct)
    {
        // NO ACCESS WINDOW: an ordinary invited member's access does not expire (FR-156). The guest
        // presenter path passes one (FR-159) — same creation code, one differing argument, so the
        // duplicate check, the Keycloak-then-local ordering and the audit cannot drift apart.
        var (member, temporaryPassword) = await MemberInvitation.InviteAsync(
            _db, _identity, _audit, _clock,
            request.Email, request.FullName, CommitteeRole.Guest, accessExpiresAt: null,
            auditAction: "Membership.UserInvited", ct);

        // ⚠ ASSIGNED HERE RATHER THAN INSIDE InviteAsync, DELIBERATELY. That helper is shared with
        // GuestProvisioner (FR-159), and permission-role-matrix E.1 does NOT make Guest
        // stream-bounded — E.3 bounds a guest by a time window instead (DEF-060, ADR-0043). Pushing
        // streams into the shared helper would either force a meaningless assignment on guest
        // presenters or add a nullable parameter that silently permits the unassigned state this
        // command exists to prevent.
        //
        // Unknown ids resolve to nothing, exactly as AssignStreamsHandler treats them; the validator
        // has already refused an EMPTY list, which is the case that actually matters.
        var streamIds = await _db.Streams
            .Where(s => request.StreamPublicIds.Contains(s.PublicId))
            .Select(s => s.Id)
            .ToListAsync(ct);

        member.AssignStreams(streamIds);
        await _db.SaveChangesAsync(ct);

        return new InvitedUserDto(
            member.PublicId, member.FullName, member.Email, member.Status.ToString(), temporaryPassword);
    }
}
