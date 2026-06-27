using Acmp.Modules.Notifications.Application.Abstractions;
using Acmp.Modules.Notifications.Application.Contracts;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Notifications.Application.Features.GetNotifications;

// The signed-in user's own notification center feed. Scoped to ICurrentUser.UserId — a user only ever
// sees their own items (no parameter to widen the scope). Newest first, bounded to the most recent
// MaxItems (the center is a recent-activity panel, not an archive).
public sealed record GetNotificationsQuery : IRequest<NotificationListDto>;

public sealed class GetNotificationsHandler : IRequestHandler<GetNotificationsQuery, NotificationListDto>
{
    public const int MaxItems = 50;

    private readonly INotificationsDbContext _db;
    private readonly ICurrentUser _user;

    public GetNotificationsHandler(INotificationsDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<NotificationListDto> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var recipient = _user.UserId
            ?? throw new UnauthorizedAccessException("Authentication required.");

        var mine = _db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == recipient);

        var unreadCount = await mine.CountAsync(n => !n.IsRead, ct);

        var items = await mine
            .OrderByDescending(n => n.CreatedAt)
            .Take(MaxItems)
            .Select(n => new NotificationDto(
                n.PublicId, n.Title.En, n.Title.Ar, n.Body.En, n.Body.Ar,
                n.Category, n.DeepLink, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

        return new NotificationListDto(items, unreadCount);
    }
}
