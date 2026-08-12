using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.CancelMeeting;

// W5: cancel a scheduled meeting with a mandatory reason. RBAC = Meeting.Schedule (Chairman/Secretary).
public sealed record CancelMeetingCommand(Guid MeetingId, string Reason) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class CancelMeetingValidator : AbstractValidator<CancelMeetingCommand>
{
    public CancelMeetingValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A cancellation reason is required.");
    }
}

public sealed class CancelMeetingHandler : IRequestHandler<CancelMeetingCommand>
{
    private readonly IMeetingsDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;
    private readonly IGuestWindowWriter _windows;

    public CancelMeetingHandler(IMeetingsDbContext db, IClock clock, IAuditSink audit, ICurrentUser user,
        IGuestWindowWriter windows)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _user = user;
        _windows = windows;
    }

    public async Task Handle(CancelMeetingCommand request, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.PublicId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Meeting not found.");

        var presenters = await _db.Agendas.AsNoTracking()
            .Where(a => a.MeetingId == meeting.PublicId)
            .SelectMany(a => a.Items.Where(i => i.PresenterUserId != null).Select(i => i.PresenterUserId!.Value))
            .ToListAsync(ct);

        meeting.Cancel(request.Reason, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Meetings.MeetingCancelled", nameof(Meeting), meeting.PublicId.ToString(), ct: ct);

        // DW-025 — a meeting that will not happen must not leave an external presenter holding live
        // access to its unpublished material. AFTER the save, so the cancelled status is what the
        // still-presenting check reads; a guest who also presents elsewhere keeps their access.
        await GuestWindows.CloseOrphanedAsync(_db, _windows, _clock.UtcNow, presenters, ct);
    }
}
