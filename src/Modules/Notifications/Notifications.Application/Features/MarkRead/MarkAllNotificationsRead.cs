using Acmp.Modules.Notifications.Application.Abstractions;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Notifications.Application.Features.MarkRead;

// Mark ALL of the signed-in user's unread notifications read (the center's "Mark all read"). Scoped to
// ICurrentUser (guardrail 4) — only the caller's own items are touched. Returns the number flipped.
// Unlike the single MarkRead, a bulk clear emits an AuditEvent after persistence (guardrail 5 / docs/domain/audit-and-records.md):
// it's a one-click sweep of the whole inbox, worth a record. This reverses P6e's no-audit choice for
// read-all per the operator's P6b Option-B decision (2026-06-30); single MarkRead stays un-audited.
// The emit sits after SaveChanges and only when something actually changed (the count==0 short-circuit
// returns before persistence), so no-op clears produce no audit noise.
public sealed record MarkAllNotificationsReadCommand : IRequest<int>;

public sealed class MarkAllNotificationsReadHandler : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly INotificationsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public MarkAllNotificationsReadHandler(INotificationsDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
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
        await _audit.EmitAsync("Notifications.AllRead", recipient, new { marked = unread.Count }, ct);
        return unread.Count;
    }
}
