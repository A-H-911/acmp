using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.PublishMinutes;

// W10 (AC-038): publish the approved MoM. Approved → Published (immutable). On this transition — and only
// this one — an in-app notification with a deep link fans out to every active committee member. RBAC =
// Minutes.Approve (docs/domain/permission-role-matrix.md row 9).
public sealed record PublishMinutesCommand(Guid MinutesId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Secretary, AcmpRoles.Chairman };
}

public sealed class PublishMinutesHandler : IRequestHandler<PublishMinutesCommand>
{
    private readonly IMeetingsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICommitteeDirectory _directory;
    private readonly INotificationChannel _notifications;

    public PublishMinutesHandler(IMeetingsDbContext db, ICurrentUser user, IClock clock, IAuditSink audit,
        ICommitteeDirectory directory, INotificationChannel notifications)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
        _directory = directory;
        _notifications = notifications;
    }

    public async Task Handle(PublishMinutesCommand request, CancellationToken ct)
    {
        var minutes = await _db.Minutes.FirstOrDefaultAsync(m => m.PublicId == request.MinutesId, ct)
            ?? throw new KeyNotFoundException("Minutes not found.");

        minutes.Publish(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await MinutesNotifications.FanOutAsync(_directory, _notifications,
            MinutesNotifications.MinutesPublished(minutes.MeetingTitle, minutes.MeetingKey), ct);
        await _audit.EmitEnrichedAsync("Meetings.MinutesPublished", nameof(MinutesOfMeeting), minutes.PublicId.ToString(), ct: ct);
    }
}
