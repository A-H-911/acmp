using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Topics;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.GetMySession;

// FR-159 / AC-092 / DEC-037 — the read model behind /session, the GUEST / PRESENTER SHELL.
//
// CALLER-SCOPED BY CONSTRUCTION. There is no parameter naming a meeting, a topic or a person: the
// answer is always "the slot YOU are presenting". A guest cannot ask for somebody else's session
// because the question cannot express it, which is a stronger guarantee than checking that they did
// not (OQ-074 records that a Chairman or Secretary previewing a SPECIFIC presenter's view is not
// built; they see their own slot, or the empty state).
//
// Composed from three modules through contracts, never a join (ADR-0001): the slot and the meeting
// are Meetings' own, the caller's id and access window come from ICommitteeDirectory, and the topic
// card plus its materials come from ITopicReader.

public sealed record GetMySessionQuery : IRequest<PresenterSessionDto?>, IAuthorizedRequest
{
    // Guest is the audience; Chairman and Secretary are admitted so the people who run the meeting can
    // see the surface an external presenter gets (DEC-037). Enforced HERE and not only by the route
    // guard, which the decision calls out explicitly.
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { AcmpRoles.Guest, AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed record SessionMaterialDto(Guid Id, string FileName, string ContentType, long SizeBytes);

/// <param name="AccessExpiresAt">
/// The banner's value, and the SAME stored column the API refusal and the expiry sweep read (DEC-037).
/// Null for a Chairman or Secretary previewing — their access does not expire, and the banner says so.
/// </param>
/// <param name="ItemNumber">1-based position, for the design's "Item 3 of 6".</param>
/// <param name="SlotStart">
/// Computed from the meeting's start plus the time-boxes of the items before this one, so the design's
/// "10:40–10:55" is real rather than decorative. It is a PLAN, not a promise — a meeting that overruns
/// does not move it.
/// </param>
public sealed record PresenterSessionDto(
    DateTimeOffset? AccessExpiresAt,
    string MeetingKey,
    string MeetingTitle,
    DateTimeOffset SlotStart,
    DateTimeOffset SlotEnd,
    int ItemNumber,
    int ItemCount,
    int TimeboxMinutes,
    string TopicKey,
    string TopicTitle,
    string TopicSummary,
    IReadOnlyList<SessionMaterialDto> Materials);

public sealed class GetMySessionHandler : IRequestHandler<GetMySessionQuery, PresenterSessionDto?>
{
    private readonly IMeetingsDbContext _db;
    private readonly ICommitteeDirectory _directory;
    private readonly ITopicReader _topics;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public GetMySessionHandler(IMeetingsDbContext db, ICommitteeDirectory directory, ITopicReader topics,
        ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _directory = directory;
        _topics = topics;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PresenterSessionDto?> Handle(GetMySessionQuery request, CancellationToken ct)
    {
        var me = await _directory.ResolveMemberAsync(_currentUser.UserId ?? string.Empty, ct);
        if (me is null)
            return null; // no member row yet — the SPA provisions on login, so this is a first-visit race

        // Every meeting this person is presenting at. Cancelled meetings are excluded: a slot on a
        // meeting that will not happen is not a session, and showing one would tell a guest to prepare.
        var candidates = await _db.Agendas.AsNoTracking()
            .SelectMany(a => a.Items.Where(i => i.PresenterUserId == me.PublicId)
                .Select(i => new { a.MeetingId, i.TopicId, i.Order, i.TimeboxMinutes }))
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return null;

        var meetingIds = candidates.Select(c => c.MeetingId).ToArray();
        var meetings = await _db.Meetings.AsNoTracking()
            .Where(m => meetingIds.Contains(m.PublicId) && m.Status != Domain.Enums.MeetingStatus.Cancelled)
            .Select(m => new { m.PublicId, m.Key, m.Title, m.ScheduledStart, m.ScheduledEnd })
            .ToListAsync(ct);

        if (meetings.Count == 0)
            return null;

        // WHICH slot, when there is more than one: the soonest meeting that has not ended yet, and
        // otherwise the most recent one. The fallback matters — the access window outlives the meeting
        // by a day (ADR-0040 decision 2), and during that day the presenter can still want their own
        // material. Picking "the next one" alone would blank the page the moment the meeting ended.
        var now = _clock.UtcNow;
        var meeting = meetings.Where(m => m.ScheduledEnd >= now).OrderBy(m => m.ScheduledStart).FirstOrDefault()
                      ?? meetings.OrderByDescending(m => m.ScheduledEnd).First();

        var slot = candidates.First(c => c.MeetingId == meeting.PublicId);

        // WHICH slot is this handler's job; RENDERING it is PresenterSessionComposer's, shared with the
        // Chairman/Secretary preview (FR-165, DEC-086 d1). The split exists so a preview cannot drift
        // from the thing it previews — two renderers would agree only until the next change to either.
        // The caller's OWN window is passed in: for a committee member it is null, and the banner says so.
        return await PresenterSessionComposer.ComposeAsync(
            _db, _topics, meeting.PublicId, slot.TopicId, me.AccessExpiresAt, ct);
    }
}

// The download half. Same caller-scoping: the client names an ATTACHMENT, never a topic, and the
// topic is re-derived from the caller's own slot — so a guest cannot reach another topic's file by
// guessing an id. Returns null when the attachment is not on their slot's topic (fail closed).
public sealed record GetMyMaterialUrlQuery(Guid AttachmentId) : IRequest<string?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { AcmpRoles.Guest, AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class GetMyMaterialUrlHandler : IRequestHandler<GetMyMaterialUrlQuery, string?>
{
    private readonly ITopicReader _topics;
    private readonly IMeetingsDbContext _db;
    private readonly ICommitteeDirectory _directory;
    private readonly ICurrentUser _currentUser;

    public GetMyMaterialUrlHandler(ITopicReader topics, IMeetingsDbContext db,
        ICommitteeDirectory directory, ICurrentUser currentUser)
    {
        _topics = topics;
        _db = db;
        _directory = directory;
        _currentUser = currentUser;
    }

    public async Task<string?> Handle(GetMyMaterialUrlQuery request, CancellationToken ct)
    {
        var me = await _directory.ResolveMemberAsync(_currentUser.UserId ?? string.Empty, ct);
        if (me is null)
            return null;

        // EVERY topic this person presents, not just the one /session is currently showing: the page
        // may have been open across a meeting boundary, and a link that worked a minute ago should not
        // start returning a mismatch. Still strictly their own slots.
        var topicIds = await _db.Agendas.AsNoTracking()
            .SelectMany(a => a.Items.Where(i => i.PresenterUserId == me.PublicId).Select(i => i.TopicId))
            .ToListAsync(ct);

        foreach (var topicId in topicIds)
        {
            var url = await _topics.GetMaterialUrlAsync(topicId, request.AttachmentId, ct);
            if (url is not null)
                return url;
        }

        return null;
    }
}
