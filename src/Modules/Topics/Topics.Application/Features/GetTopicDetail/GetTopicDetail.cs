using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Contracts;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.GetTopicDetail;

// Topic detail by display key (TOP-YYYY-###) — overview fields, status history, comments, attachments.
// Cross-module relationships (decisions/ADRs/actions/risks/meetings) populate as those modules land.
public sealed record GetTopicDetailQuery(string Key) : IRequest<TopicDetailDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetTopicDetailHandler : IRequestHandler<GetTopicDetailQuery, TopicDetailDto?>
{
    private readonly ITopicsDbContext _db;
    private readonly IClock _clock;
    private readonly ITopicVisibility _visibility;
    private readonly IAnomalyDetector _anomaly;

    public GetTopicDetailHandler(ITopicsDbContext db, IClock clock, ITopicVisibility visibility,
        IAnomalyDetector anomaly)
    {
        _db = db;
        _clock = clock;
        _visibility = visibility;
        _anomaly = anomaly;
    }

    public async Task<TopicDetailDto?> Handle(GetTopicDetailQuery request, CancellationToken ct)
    {
        var t = await _db.Topics.AsNoTracking()
            .Include(x => x.History)
            .Include(x => x.Comments)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Key == request.Key, ct);

        if (t is null) return null;

        // C-AUTHZ-04 / FR-163. THE DIRECT-BY-KEY PATH IS THE IDOR PATH: filtering the LIST while
        // leaving this open would hide a Restricted topic from the backlog and hand it to anyone who
        // guesses or is sent the key. This handler carried NO authorization at all before FR-163 —
        // AllowedRoles is empty and nothing called IResourceAuthorizer — so the check is added here
        // rather than assumed to exist upstream.
        //
        // ⚠ RETURNS null, WHICH THE ENDPOINT RENDERS AS 404 — NOT 403. A 403 confirms that a topic
        // with this key exists, which is precisely the fact the classification is protecting; the
        // refusal must be indistinguishable from "no such topic".
        if (!(await _visibility.ResolveAsync(ct)).CanSee(t)) return null;

        // C-INS-01 signal 2 (NFR-065): a SUCCESSFUL read of a Restricted topic is recorded here, and the
        // detector decides whether this principal's rate is atypical (DEC-099 d1).
        //
        // ⚠⚠ IT SITS AFTER THE VISIBILITY CHECK ON PURPOSE. Above this line the caller may not see the
        // topic and the refusal is already indistinguishable from "no such topic"; recording an access
        // there would log reads that never happened and would leak, into the audit log, the existence of
        // topics the classification exists to hide. Below it, the access is real.
        if (t.IsRestricted) await _anomaly.ObserveRestrictedTopicAccessAsync(t.PublicId, ct);

        var now = _clock.UtcNow;
        return new TopicDetailDto(
            t.PublicId, t.Key, t.Title, t.Description, t.Justification,
            t.Type.ToString(), t.Status.ToString(), t.Urgency.ToString(), t.Scope.ToString(), t.Source.ToString(),
            t.AffectedStreams.ToList(), t.Systems.ToList(), t.Tags.ToList(),
            t.OwnerId, t.OwnerName, t.SubmittedByName, t.Priority,
            TopicAging.AgeDays(t.CreatedAt, now), TopicAging.IsBreaching(t, now), t.CreatedAt, t.RevisitOn,
            t.History.OrderBy(h => h.OccurredAt)
                .Select(h => new TopicHistoryDto(h.FromStatus.ToString(), h.ToStatus.ToString(), h.Reason, h.ActorName, h.OccurredAt)).ToList(),
            t.Comments.OrderBy(c => c.PostedAt)
                .Select(c => new TopicCommentDto(c.PublicId, c.Body, c.AuthorName, c.PostedAt)).ToList(),
            t.Attachments.OrderBy(a => a.UploadedAt)
                .Select(a => new TopicAttachmentDto(a.PublicId, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByName, a.UploadedAt)).ToList(),
            t.IsRestricted);
    }
}
