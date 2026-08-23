using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Features.AgendaBuilder;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.InviteGuestPresenter;

// FR-159 / AC-092 — the Secretary invites a guest presenter FROM THE MEETING SCREEN, with access
// that expires after the meeting (DEC-037, ADR-0040 decision 1).
//
// THIS IS THE HALF THAT WAS MISSING. CommitteeMember.AccessExpiresAt, the per-request refusal
// (ADR-0039) and the hourly Keycloak-disabling sweep all shipped already and all read a value that
// NOTHING SET. This is the writer, and it is the only one.
//
// WHY THE USE CASE LIVES IN MEETINGS: the window comes from Meeting.ScheduledEnd and the slot is an
// AgendaItem — both owned here — so this handler reads its own aggregate and crosses the module
// boundary exactly ONCE, through the IGuestProvisioner port (ADR-0021's pattern). Hosting it in
// Membership would need two crossings and would put a meeting concept inside the member registry.

public static class GuestAccess
{
    /// <summary>
    /// How long a guest keeps access after their meeting's scheduled end (ADR-0040 decision 2, as
    /// amended by DEC-040).
    /// </summary>
    /// <remarks>
    /// The ADR recommended no grace at all, since <c>HasExpired</c> is exclusive and the requirement
    /// says "expires after the meeting". The operator widened it to a day, and the reason is worth
    /// keeping next to the number: refusal is per-request and IMMEDIATE, so a meeting that overruns
    /// would hand the presenter a 401 in the middle of presenting. A committee meeting running long
    /// is ordinary; cutting off the person currently speaking is a worse failure than a bounded extra
    /// day of read access to one slot. Named rather than inlined so the duration is one edit and the
    /// tests assert against this symbol instead of a copied literal.
    /// </remarks>
    public static readonly TimeSpan Grace = TimeSpan.FromHours(24);
}

public sealed record InviteGuestPresenterCommand(Guid MeetingId, Guid TopicId, string Email, string FullName)
    : IRequest<InvitedGuestPresenterDto>, IAuthorizedRequest
{
    // SECRETARY ONLY — FR-159, and deliberately NOT the Administrator-or-Secretary pair FR-156 uses.
    // That invite creates an inert account with no role; this one hands an EXTERNAL person real read
    // access to unpublished committee material, and the Secretary is the role that schedules the
    // meeting and knows who is presenting (DEC-037).
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Secretary };
}

/// <param name="AccessExpiresAt">
/// The stored value the API refusal, the expiry sweep and the /session banner all read. Returned so
/// the inviter sees the exact instant they are granting, rather than being told "after the meeting".
/// </param>
/// <param name="TemporaryPassword">Revealed ONCE and stored nowhere (AC-088) — there is no email in v1.</param>
public sealed record InvitedGuestPresenterDto(
    Guid PublicId, string FullName, string Email, DateTimeOffset AccessExpiresAt, string TemporaryPassword);

public sealed class InviteGuestPresenterValidator : AbstractValidator<InviteGuestPresenterCommand>
{
    public InviteGuestPresenterValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty();
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(256);
    }
}

public sealed class InviteGuestPresenterHandler : IRequestHandler<InviteGuestPresenterCommand, InvitedGuestPresenterDto>
{
    private readonly IMeetingsDbContext _db;
    private readonly IGuestProvisioner _guests;
    private readonly IAuditSink _audit;

    public InviteGuestPresenterHandler(IMeetingsDbContext db, IGuestProvisioner guests, IAuditSink audit)
    {
        _db = db;
        _guests = guests;
        _audit = audit;
    }

    public async Task<InvitedGuestPresenterDto> Handle(InviteGuestPresenterCommand request, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.PublicId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Meeting not found.");

        var agenda = await AgendaLoader.ForMeetingAsync(_db, request.MeetingId, ct);

        // VALIDATE THE SLOT BEFORE CREATING ANYTHING. The account and the member row are the
        // irreversible parts of this operation — DEF-029 means a member row can be disabled but never
        // deleted — so a wrong topic id must fail here, while nothing exists yet, rather than after
        // an external identity has been created for a slot that is not on this agenda.
        if (agenda.Items.All(i => i.TopicId != request.TopicId))
            throw new InvalidOperationException("That topic is not on this meeting's agenda.");

        var accessExpiresAt = meeting.ScheduledEnd + GuestAccess.Grace;

        var guest = await _guests.InviteGuestAsync(request.Email, request.FullName, accessExpiresAt, ct);

        // Two stores, two transactions — the trade-off ADR-0021 accepted and ADR-0040 restates. The
        // order is forced: the assignment needs the PublicId only the first step can produce. A crash
        // between them leaves a guest with no slot, which the Secretary fixes with the agenda's
        // existing presenter control; it never leaves a slot pointing at nobody.
        agenda.AssignPresenter(request.TopicId, guest.PublicId, guest.FullName);
        await _db.SaveChangesAsync(ct);

        // Audited HERE as well as in Membership, and the two rows say different things: Membership
        // records that an account was created, this records that an OUTSIDER was given a slot in this
        // meeting. Agenda-builder edits are otherwise unaudited (the governance event is
        // AgendaPublished) — admitting an external person is not an ordinary agenda edit.
        await _audit.EmitEnrichedAsync(
            "Meetings.GuestPresenterInvited", nameof(Meeting), meeting.PublicId.ToString(), ct: ct);

        return new InvitedGuestPresenterDto(
            guest.PublicId, guest.FullName, guest.Email, accessExpiresAt, guest.TemporaryPassword);
    }
}
