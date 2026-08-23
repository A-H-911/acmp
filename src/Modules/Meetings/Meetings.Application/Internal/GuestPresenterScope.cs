using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Internal;

// DEF-073 / AC-011 — WHICH MEETING ROWS A GUEST PRESENTER MAY SEE.
//
// ADR-0040 gives the committee its first EXTERNAL principal, and GuestSurfaceMiddleware admits GET on
// /api/meetings for them because the design's role matrix grants Guest "agenda (view)". That entry
// admits a PATH PREFIX and nothing more, so for a while a guest could list and read every meeting the
// committee had ever scheduled — which is AC-011's "any action outside that meeting scope" answering
// 200 where the AC says 403.
//
// WHY THE SCOPING LIVES HERE AND NOT IN THE GATE: the middleware is path-based, has no database, and
// cannot turn a display key into a meeting. The only place that can compare the caller against a
// MEETING is the handler that has already loaded one. The gate decides which paths are reachable; this
// decides which rows are visible, and the two questions are genuinely different — treating them as one
// is what produced the defect.
internal static class GuestPresenterScope
{
    /// <summary>
    /// The meetings this caller is presenting at, or <c>null</c> when no scoping applies because the
    /// caller is not a guest-only principal.
    /// </summary>
    /// <remarks>
    /// ⚠ NULL AND EMPTY MEAN OPPOSITE THINGS, AND THE DIFFERENCE IS THE WHOLE CONTROL. Null means "this
    /// caller is an insider, do not filter"; an EMPTY set means "this guest may see nothing". A guest
    /// whose member row has not resolved yet therefore returns the empty set, never null — failing
    /// closed. Returning null there would hand an unresolvable guest the committee-wide read this
    /// exists to remove, and it is the shape a later refactor is most likely to get wrong, which is
    /// why <c>GuestPresenterScopeTests</c> asserts that case directly.
    ///
    /// Presenter identity is <c>AgendaItem.PresenterUserId</c>, a member PublicId — hence the directory
    /// hop, which is the same one GetMySessionHandler makes for the same reason (ADR-0001: Meetings
    /// never reads Membership's tables).
    /// </remarks>
    public static async Task<IReadOnlySet<Guid>?> MeetingIdsAsync(
        IMeetingsDbContext db,
        ICommitteeDirectory directory,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (!AcmpRoles.IsGuestOnly(currentUser.IsInRole))
            return null;

        var me = await directory.ResolveMemberAsync(currentUser.UserId ?? string.Empty, ct);
        if (me is null)
            return new HashSet<Guid>();

        var ids = await db.Agendas.AsNoTracking()
            .Where(a => a.Items.Any(i => i.PresenterUserId == me.PublicId))
            .Select(a => a.MeetingId)
            .ToListAsync(ct);

        return ids.ToHashSet();
    }
}
