using Acmp.Modules.Actions.Application.Abstractions;
using Acmp.Modules.Actions.Domain.Enums;
using Acmp.Shared.Contracts.Actions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Actions.Infrastructure.Directory;

// Actions-owned implementation of IActionDigestSource (ADR-0001) for the digest (AC-161). The overdue test
// repeats ActionItem.IsOverdue's rule in a form SQL can translate.
public sealed class ActionDigestSource : IActionDigestSource
{
    private readonly IActionsDbContext _db;

    public ActionDigestSource(IActionsDbContext db) => _db = db;

    public async Task<PendingActionCount> CountPendingAsync(string ownerUserId, DateTimeOffset now, CancellationToken ct = default)
    {
        var live = _db.Actions.AsNoTracking().Where(a => a.OwnerUserId == ownerUserId &&
            (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress || a.Status == ActionStatus.Blocked));
        return new PendingActionCount(
            await live.CountAsync(ct),
            await live.CountAsync(a => a.DueDate != null && a.DueDate < now, ct));
    }
}
