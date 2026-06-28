using Acmp.Modules.Notifications.Application.Abstractions;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Notifications.Application.Features.MarkRead;

// Mark ALL of the signed-in user's unread notifications read (the center's "Mark all read"). Scoped to
// ICurrentUser (guardrail 4) — only the caller's own items are touched. Returns the number flipped.
// Like MarkRead, read-status is the user's own inbox state, not a governance change, so no AuditEvent is
// emitted (mirrors MarkNotificationReadHandler).
public sealed record MarkAllNotificationsReadCommand : IRequest<int>;

public sealed class MarkAllNotificationsReadHandler : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly INotificationsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public MarkAllNotificationsReadHandler(INotificationsDbContext db, ICurrentUser user, IClock clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task<int> Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
    {
        var recipient = _user.UserId
            ?? throw new UnauthorizedAccessException("Authentication required.");

        var unread = await _db.Notifications
            .Where(n => n.RecipientUserId == recipient && !n.IsRead)
            .ToListAsync(ct);

        if (unread.Count == 0)
            return 0;

        var now = _clock.UtcNow;
        foreach (var notification in unread)
            notification.MarkRead(now);

        await _db.SaveChangesAsync(ct);
        return unread.Count;
    }
}
