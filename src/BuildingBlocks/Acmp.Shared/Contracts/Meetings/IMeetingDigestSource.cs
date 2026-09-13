namespace Acmp.Shared.Contracts.Meetings;

// Cross-module read seam (ADR-0001) for the notification digest (FR-134 / AC-161, DEC-188): the Notifications
// module asks "how many Scheduled meetings start in [from, to)?" without reading the Meetings tables. Meetings
// have no invitee list, so the count is committee-wide. Implemented in Meetings.Infrastructure.
public interface IMeetingDigestSource
{
    Task<int> CountStartingAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
