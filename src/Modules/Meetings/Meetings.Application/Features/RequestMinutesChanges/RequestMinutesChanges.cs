using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.RequestMinutesChanges;

// W10 (AC-037): the reviewer/Chairman requests changes on a MoM in review. InReview → Draft; the author
// (the MoM's creator) is notified so the review cycle restarts. RBAC = Minutes.Approve (the reviewer).
public sealed record RequestMinutesChangesCommand(Guid MinutesId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Secretary, AcmpRoles.Chairman };
}

public sealed class RequestMinutesChangesHandler : IRequestHandler<RequestMinutesChangesCommand>
{
    private readonly IMeetingsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly INotificationChannel _notifications;

    public RequestMinutesChangesHandler(IMeetingsDbContext db, ICurrentUser user, IClock clock,
        IAuditSink audit, INotificationChannel notifications)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task Handle(RequestMinutesChangesCommand request, CancellationToken ct)
    {
        var minutes = await _db.Minutes.FirstOrDefaultAsync(m => m.PublicId == request.MinutesId, ct)
            ?? throw new KeyNotFoundException("Minutes not found.");

        var authorSub = minutes.CreatedBy; // the MoM author (AuditableEntity stamp) — targeted, not fanned out
        minutes.RequestChanges(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(authorSub))
            await _notifications.PublishAsync(
                MinutesNotifications.ChangesRequested(authorSub, minutes.MeetingTitle, minutes.MeetingKey), ct);
        await _audit.EmitAsync("Meetings.MinutesChangesRequested", _user.UserId, new { minutes.PublicId, minutes.Key }, ct);
    }
}
