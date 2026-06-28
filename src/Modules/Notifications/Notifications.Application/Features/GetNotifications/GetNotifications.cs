using Acmp.Modules.Notifications.Application.Abstractions;
using Acmp.Modules.Notifications.Application.Contracts;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Notifications.Application.Features.GetNotifications;

// The signed-in user's own notification center feed. Scoped to ICurrentUser.UserId — a user only ever
// sees their own items (no parameter to widen the scope). Newest first, paged: the bell popover reads a
// small first page; the full-page center (#79) pages lazily via Page/HasMore. UnreadCount is always the
// TOTAL unread (the badge), not just this page. PageSize is clamped to MaxPageSize.
public sealed record GetNotificationsQuery(int Page = 1, int PageSize = 20) : IRequest<NotificationListDto>
{
    public const int MaxPageSize = 50;
}

public sealed class GetNotificationsHandler : IRequestHandler<GetNotificationsQuery, NotificationListDto>
{
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

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, GetNotificationsQuery.MaxPageSize);
        var skip = (page - 1) * pageSize;

        var mine = _db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == recipient);

        var total = await mine.CountAsync(ct);
        var unreadCount = await mine.CountAsync(n => !n.IsRead, ct);

        var items = await mine
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(n => new NotificationDto(
                n.PublicId, n.Title.En, n.Title.Ar, n.Body.En, n.Body.Ar,
                n.Category, n.DeepLink, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

        var hasMore = skip + items.Count < total;

        return new NotificationListDto(items, unreadCount, total, hasMore);
    }
}
