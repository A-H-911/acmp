namespace Acmp.Shared.Contracts.Actions;

// Cross-module read seam (ADR-0001) for the notification digest (FR-134 / AC-161, DEC-188): the Notifications
// module asks "how many live actions does this member own, and how many are overdue?" without reading the
// Actions tables. Live = Open, InProgress or Blocked; overdue = ActionItem.IsOverdue's rule (due date passed
// while live). Implemented in Actions.Infrastructure.
public sealed record PendingActionCount(int Pending, int Overdue);

public interface IActionDigestSource
{
    Task<PendingActionCount> CountPendingAsync(string ownerUserId, DateTimeOffset now, CancellationToken ct = default);
}
