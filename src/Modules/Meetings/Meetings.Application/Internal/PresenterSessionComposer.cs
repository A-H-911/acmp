using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Features.GetMySession;
using Acmp.Shared.Contracts.Topics;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Internal;

// FR-159 / FR-165 — ONE renderer for the presenter session view, used by both the caller-scoped
// /session read and the Chairman/Secretary preview (DEC-086 d1).
//
// WHY IT IS SHARED, and it is the only reason: a preview that can disagree with the thing it previews
// is worse than no preview. Two independent compositions would drift on the first change to either —
// silently, because nothing compares them and no test can assert "these two renderers agree" without
// being a third copy of the same logic. Extracting the composition makes agreement structural rather
// than something a reviewer has to remember.
//
// THE SPLIT IS "WHICH SLOT" vs "RENDER THE SLOT", and the two callers differ ONLY in the first half.
// GetMySessionHandler asks "which of the caller's slots is current" and answers with a heuristic over
// their own agenda items. The preview is handed a meeting and a topic, so it has no heuristic at all.
// Everything after that point — the empty states included — belongs here.
//
// ⚠ THE EMPTY STATES ARE PART OF THE CONTRACT, NOT AN ABSENCE OF ONE. A cancelled meeting and an
// agenda item that no longer exists BOTH resolve to null here, so the preview reproduces the
// presenter's "you are not presenting" screen exactly rather than showing a Secretary more than the
// presenter would get. That parity is what FR-165 requires in as many words, and it holds because
// there is one implementation of it rather than two that happen to match today.
internal static class PresenterSessionComposer
{
    /// <summary>
    /// Renders one agenda slot as the presenter sees it, or null when there is nothing to show.
    /// </summary>
    /// <param name="accessExpiresAt">
    /// The window of the person whose view this is — the CALLER's for /session, the TARGET's for a
    /// preview. It is passed in rather than looked up because only the caller knows whose view is being
    /// rendered, and getting that wrong is the difference between a real preview and a Chairman looking
    /// at their own never-expiring banner.
    /// </param>
    public static async Task<PresenterSessionDto?> ComposeAsync(
        IMeetingsDbContext db,
        ITopicReader topics,
        Guid meetingId,
        Guid topicId,
        DateTimeOffset? accessExpiresAt,
        CancellationToken ct)
    {
        // Cancelled meetings are excluded: a slot on a meeting that will not happen is not a session,
        // and showing one would tell a presenter to prepare.
        var meeting = await db.Meetings.AsNoTracking()
            .Where(m => m.PublicId == meetingId && m.Status != Domain.Enums.MeetingStatus.Cancelled)
            .Select(m => new { m.Key, m.Title, m.ScheduledStart })
            .FirstOrDefaultAsync(ct);

        if (meeting is null)
            return null;

        // "Item 3 of 6" and the planned clock time both need the WHOLE agenda, not just this item.
        var agendaItems = await db.Agendas.AsNoTracking()
            .Where(a => a.MeetingId == meetingId)
            .SelectMany(a => a.Items.Select(i => new { i.Order, i.TimeboxMinutes, i.TopicId }))
            .ToListAsync(ct);

        var ordered = agendaItems.OrderBy(i => i.Order).ToList();
        var index = ordered.FindIndex(i => i.TopicId == topicId);
        if (index < 0)
            return null;

        var slot = ordered[index];
        var minutesBefore = ordered.Take(index).Sum(i => i.TimeboxMinutes);
        var slotStart = meeting.ScheduledStart.AddMinutes(minutesBefore);

        var topic = await topics.GetBriefAsync(topicId, ct);

        return new PresenterSessionDto(
            accessExpiresAt,
            meeting.Key,
            meeting.Title,
            slotStart,
            slotStart.AddMinutes(slot.TimeboxMinutes),
            index + 1,
            ordered.Count,
            slot.TimeboxMinutes,
            // The agenda item carries key/title snapshots, but the SUMMARY and the materials only exist
            // in Topics. A topic deleted underneath the slot degrades to the snapshot rather than 404s:
            // the presenter still learns when and where they are presenting.
            topic?.Key ?? string.Empty,
            topic?.Title ?? string.Empty,
            topic?.Summary ?? string.Empty,
            topic?.Materials.Select(m => new SessionMaterialDto(m.Id, m.FileName, m.ContentType, m.SizeBytes)).ToList()
                ?? new List<SessionMaterialDto>());
    }
}
