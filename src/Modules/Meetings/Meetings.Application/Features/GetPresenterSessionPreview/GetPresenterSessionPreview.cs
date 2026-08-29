using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Features.GetMySession;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Topics;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.GetPresenterSessionPreview;

// FR-165 / DEC-086 — the Chairman/Secretary preview of a CHOSEN presenter's session view.
//
// WHY THIS IS A SEPARATE QUERY AND NOT A PARAMETER ON GetMySessionQuery (DEC-086 d1). The /session read
// path's strongest property is an absence: it takes no parameter naming a meeting, a topic or a person,
// so a guest cannot ask for somebody else's session because the QUESTION CANNOT EXPRESS IT. That is a
// structural guarantee, not a check that can be got wrong. OQ-074 was resolved (DEC-048 d4) to a chosen
// presenter's slot, which requires exactly the parameter whose absence is that guarantee — so the
// targeting lives HERE, in a query a guest is never admitted to, and GetMySessionQuery is untouched.
//
// THE REFUSAL IS THE FEATURE (DW-028's own words) AND IT HAS THREE INDEPENDENT LAYERS, deliberately
// given DISTINGUISHABLE signatures so each can be forced and proven SEPARATELY rather than shading into
// one control tested three times:
//   1. the SPA route guard, which admits only Chairman and Secretary;
//   2. GuestSurfaceMiddleware, because /api/session-preview is NOT under the /api/session allowlist —
//      a guest-only principal is refused at the path with an X-Acmp-Auth-Reason of guest_scope, before
//      any handler runs and without a database;
//   3. AllowedRoles below, which refuses every other role at the application boundary and emits an
//      Authorization.Forbidden audit row on the way out.
// Layer 2 only ever sees guests; layer 3 is the only one that refuses a Member or an Auditor. Neither
// is redundant, which is why both are asserted.
public sealed record GetPresenterSessionPreviewQuery(Guid MeetingId, Guid TopicId)
    : IRequest<PresenterSessionDto?>, IAuthorizedRequest
{
    // ⚠ GUEST IS ABSENT FROM THIS LIST ON PURPOSE, and its absence is load-bearing rather than an
    // oversight. GetMySessionQuery admits Guest, Chairman and Secretary; this one admits the two roles
    // that RUN the meeting and nobody else. permission-role-matrix E.3 is why: a guest is bounded by a
    // TIME WINDOW and not by scope, so a targeting parameter must never become the way one reads
    // somebody else's slot.
    //
    // ⚠⚠ AND THE TEST FOR THIS CANNOT LIVE AT THE HTTP BOUNDARY, which is worth knowing before moving it.
    // An earlier draft of this comment called adding Guest here "the single mutation" that would open that
    // door. That was written rather than measured, and it is FALSE: GuestSurfaceMiddleware refuses a
    // guest-only principal at the PATH, so the mutation could be applied and every API test would stay
    // green. The guest population is exactly the one layer 2 always intercepts, so layer 3's guest
    // exclusion is invisible to any test that goes through the pipeline. It is asserted against
    // AuthorizationBehavior directly instead — PresenterPreviewAuthorizationTests.
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class GetPresenterSessionPreviewHandler
    : IRequestHandler<GetPresenterSessionPreviewQuery, PresenterSessionDto?>
{
    private readonly IMeetingsDbContext _db;
    private readonly ICommitteeDirectory _directory;
    private readonly ITopicReader _topics;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditSink _audit;

    public GetPresenterSessionPreviewHandler(IMeetingsDbContext db, ICommitteeDirectory directory,
        ITopicReader topics, ICurrentUser currentUser, IAuditSink audit)
    {
        _db = db;
        _directory = directory;
        _topics = topics;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<PresenterSessionDto?> Handle(GetPresenterSessionPreviewQuery request, CancellationToken ct)
    {
        // WHOSE view is being previewed is decided by the AGENDA, never by the caller. The request names
        // a slot; the slot names its presenter. So there is no parameter through which a caller can ask
        // to be shown as somebody else, and the targeting cannot be widened by choosing a different id.
        var presenterUserId = await _db.Agendas.AsNoTracking()
            .Where(a => a.MeetingId == request.MeetingId)
            .SelectMany(a => a.Items.Where(i => i.TopicId == request.TopicId).Select(i => i.PresenterUserId))
            .FirstOrDefaultAsync(ct);

        // No presenter assigned yet is a normal state, not an error: the Secretary is looking at a slot
        // they have not staffed. There is no person whose view could be rendered, so there is nothing to
        // preview and nothing to audit.
        if (presenterUserId is null || presenterUserId == Guid.Empty)
            return null;

        // The TARGET's window, not the caller's — the difference that makes the preview worth having.
        // A Chairman's own access never expires, so a banner rendered from the caller's row would show
        // something no presenter will ever see. Null here means the agenda names a PublicId with no
        // member row behind it, which is not a person to preview: fail closed.
        var presenter = await _directory.ResolveMemberByPublicIdAsync(presenterUserId.Value, ct);
        if (presenter is null)
            return null;

        var session = await PresenterSessionComposer.ComposeAsync(
            _db, _topics, request.MeetingId, request.TopicId, presenter.AccessExpiresAt, ct);

        if (session is null)
            return null;

        // DEC-086 d3 — A SUCCESSFUL PREVIEW IS AUDITED, not only a refused one, and this is only the
        // SECOND place in the product where a successful READ is recorded (the first being the audit-log
        // export, which control C-AUDIT-08 forced to be server-side for the same reason). One principal
        // looking through another person's scoped view is a governance act; DEF-056 was a refusal nobody
        // audits, and an access nobody audits is its mirror image.
        //
        // ⚠ EMITTED ONLY WHEN CONTENT IS ACTUALLY DISCLOSED. The empty-state returns above leave no audit
        // row, because nothing was read — the criterion says so in its own text rather than leaving the
        // boundary to be inferred from the code.
        await _audit.EmitAsync("Session.PresenterPreviewed", _currentUser.UserId, new
        {
            meetingId = request.MeetingId,
            topicId = request.TopicId,
            presenterUserId = presenterUserId.Value,
        }, ct);

        return session;
    }
}
