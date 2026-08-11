using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
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
public sealed record InviteUserCommand(string Email, string FullName) : IRequest<InvitedUserDto>, IAuthorizedRequest
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
        var email = request.Email.Trim().ToLowerInvariant();

        // Refuse a duplicate BEFORE touching Keycloak. Email is uniquely indexed where non-empty, so
        // the insert would fail anyway — but only AFTER the account existed in Keycloak, leaving a
        // real user behind for a request that reported failure.
        if (await _db.Members.AnyAsync(m => m.Email == email, ct))
            throw new InvalidOperationException($"A member with the email {email} already exists.");

        var account = await _identity.CreateUserAsync(email, request.FullName, ct);

        // Status=Invited with the subject id Keycloak just returned. First login flips it to Active
        // through the existing SyncFromClaims path (SC-003) — there is no second creation path and
        // nothing to reconcile, because the identity is known here rather than guessed.
        //
        // ORDER MATTERS AND THE FAILURE MODE IS DELIBERATE: if this insert fails after the account
        // exists, what is left is a Keycloak user with NO member row, which JIT provisioning creates
        // on first login (ADR-0004). The permanent damage in this system lives in the member row —
        // DEF-029 means it can be disabled but never deleted — and no member row was written.
        var member = CommitteeMember.PreRegister(account.SubjectId, request.FullName, email, CommitteeRole.Guest, _clock.UtcNow);
        _db.Members.Add(member);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Membership.UserInvited", nameof(CommitteeMember), member.PublicId.ToString(), ct: ct);

        return new InvitedUserDto(
            member.PublicId, member.FullName, member.Email, member.Status.ToString(), account.TemporaryPassword);
    }
}
