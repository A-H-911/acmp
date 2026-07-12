using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.ApproveMinutes;

// W10 (AC-014, SoD-2 soft): approve the MoM. InReview → Approved. SoD-2 is a SOFT rule — approving a MoM
// you solely authored is ALLOWED, but the act is flagged (MinutesApprovedBySoleAuthor audit + the
// ApprovedBySoleAuthor field the admin UI surfaces as a warning). Approval does NOT publish or notify —
// that is a separate transition (operator-locked 5-state). RBAC = Minutes.Approve (docs/domain/permission-role-matrix.md row 9).
public sealed record ApproveMinutesCommand(Guid MinutesId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Secretary, AcmpRoles.Chairman };
}

public sealed class ApproveMinutesHandler : IRequestHandler<ApproveMinutesCommand>
{
    private readonly IMeetingsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public ApproveMinutesHandler(IMeetingsDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(ApproveMinutesCommand request, CancellationToken ct)
    {
        var minutes = await _db.Minutes.FirstOrDefaultAsync(m => m.PublicId == request.MinutesId, ct)
            ?? throw new KeyNotFoundException("Minutes not found.");

        var (sub, name) = CurrentActor.Of(_user);
        // SoD-2 (soft): the approver is the sole author when the acting subject IS the MoM's creator.
        var isSoleAuthor = !string.IsNullOrWhiteSpace(minutes.CreatedBy) && minutes.CreatedBy == sub;

        minutes.Approve(sub, name, isSoleAuthor, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Meetings.MinutesApproved", nameof(MinutesOfMeeting), minutes.PublicId.ToString(), ct: ct);
        if (isSoleAuthor) // AC-014 flagged audit event for four-eyes review
            await _audit.EmitEnrichedAsync("Meetings.MinutesApprovedBySoleAuthor", nameof(MinutesOfMeeting), minutes.PublicId.ToString(), ct: ct);
    }
}
