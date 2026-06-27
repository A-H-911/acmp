using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
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

    public CancelMeetingHandler(IMeetingsDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _user = user;
    }

    public async Task Handle(CancelMeetingCommand request, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.PublicId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Meeting not found.");

        meeting.Cancel(request.Reason, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitAsync("Meetings.MeetingCancelled", _user.UserId, new { meeting.PublicId, meeting.Key }, ct);
    }
}
