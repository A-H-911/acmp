using Acmp.Modules.Notifications.Application.Abstractions;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Notifications.Application.Features.MarkRead;

// Mark one of the signed-in user's notifications read. Authorization boundary (guardrail 4 / IDOR):
// the lookup filters by BOTH PublicId AND RecipientUserId == the current user, so a user can never
// touch another user's item. A miss throws KeyNotFoundException → 404 (no existence leak — a stranger's
// id is indistinguishable from a non-existent one).
public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest;

public sealed class MarkNotificationReadHandler : IRequestHandler<MarkNotificationReadCommand>
{
    private readonly INotificationsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public MarkNotificationReadHandler(INotificationsDbContext db, ICurrentUser user, IClock clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var recipient = _user.UserId
            ?? throw new UnauthorizedAccessException("Authentication required.");

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.PublicId == request.NotificationId && n.RecipientUserId == recipient, ct)
            ?? throw new KeyNotFoundException("Notification not found.");

        notification.MarkRead(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }
}
