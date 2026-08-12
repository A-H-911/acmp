using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Shared.Contracts.Membership;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Internal;

// DW-025 / DEC-041 — guest access follows the meeting.
//
// A guest presenter's window is granted for ONE slot (FR-159). When that slot stops existing — the
// meeting is cancelled, the item is removed, or somebody else is made the presenter — the access
// granted for it should stop too. Left alone, an outsider keeps live read access to unpublished
// topic material for a meeting that will not happen, which is exposure rather than untidiness; that
// is the reasoning that overturned this row's original deferral.
//
// ⚠ THE CASE THAT MAKES THIS NON-TRIVIAL: a guest may hold MORE THAN ONE slot. Closing their window
// because one changed would revoke access they still need for another, so the rule is not "close the
// window of the affected presenter" but "close it only if they no longer present ANY slot on a live
// meeting". That is checked after the change, against the store, rather than reasoned about.
//
// NOTE ON RESCHEDULE: DW-025 as raised also asked for windows to MOVE when a meeting is rescheduled.
// ACMP HAS NO RESCHEDULE — Meeting.ScheduledStart/End are private-set and assigned only in the
// Schedule factory, there is no PUT/PATCH on /api/meetings, and no feature mutates them. There is
// nothing to hook, so nothing is built for it. IGuestWindowWriter takes an arbitrary instant
// precisely so a future reschedule can call it with the new end plus GuestAccess.Grace.
internal static class GuestWindows
{
    /// <summary>
    /// Closes the access window of every candidate who no longer presents any slot on a live meeting.
    /// </summary>
    /// <remarks>
    /// Safe to call with committee members mixed in: the port only touches rows that already hold a
    /// window, so an ordinary member's null AccessExpiresAt survives untouched.
    /// </remarks>
    public static async Task CloseOrphanedAsync(
        IMeetingsDbContext db, IGuestWindowWriter windows, DateTimeOffset now,
        IReadOnlyCollection<Guid> candidates, CancellationToken ct)
    {
        var ids = candidates.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
            return;

        // Every meeting id that is still live. A cancelled meeting's slots do not keep access alive —
        // that is the whole point of the cancel case.
        var liveMeetingIds = await db.Meetings.AsNoTracking()
            .Where(m => m.Status != MeetingStatus.Cancelled)
            .Select(m => m.PublicId)
            .ToListAsync(ct);

        var stillPresenting = await db.Agendas.AsNoTracking()
            .Where(a => liveMeetingIds.Contains(a.MeetingId))
            .SelectMany(a => a.Items
                .Where(i => i.PresenterUserId != null && ids.Contains(i.PresenterUserId.Value))
                .Select(i => i.PresenterUserId!.Value))
            .Distinct()
            .ToListAsync(ct);

        var orphaned = ids.Except(stillPresenting).ToArray();
        if (orphaned.Length == 0)
            return;

        // `now` closes it: HasExpired is exclusive, so access stops on the very next request.
        await windows.SetGuestWindowsAsync(orphaned, now, ct);
    }
}
