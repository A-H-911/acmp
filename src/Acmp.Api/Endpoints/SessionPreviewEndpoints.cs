using Acmp.Modules.Meetings.Application.Features.GetPresenterSessionPreview;
using MediatR;

namespace Acmp.Api.Endpoints;

// FR-165 / DEC-086 d1 — the Chairman/Secretary preview of a chosen presenter's /session view.
//
// ⚠⚠ THE PATH IS A SEPARATE GROUP ON PURPOSE, AND THE REASON IS ONE LINE OF MIDDLEWARE. GuestSurfaceMiddleware
// allowlists "/api/session" by SEGMENT, so anything under that group is reachable by a guest-only principal
// and the only refusal left would be a role check inside the handler. "/api/session-preview" does not match
// that prefix — StartsWithSegments requires the next character to be '/' or the end of the path, and here it
// is '-' — so a guest is refused at the PATH, before any handler runs and without touching a database.
// SessionPreviewApiTests forces that refusal rather than reasoning about it, because this whole paragraph is
// an argument about string matching and an argument is not evidence.
//
// THE OTHER GROUP'S "NO ROUTE PARAMETERS ANYWHERE" COMMENT STAYS LITERALLY TRUE, which is the second reason
// not to nest this under it. That sentence is the guest surface's security argument written down; a preview
// endpoint sitting inside the same group would falsify it silently, and a stale comment about a sibling's
// state is exactly the drift WBS-24.3 found and nothing compiles.
public static class SessionPreviewEndpoints
{
    public static IEndpointRouteBuilder MapSessionPreviewEndpoints(this IEndpointRouteBuilder app)
    {
        // 204 rather than 404 when there is nothing to preview — a slot with no presenter, a cancelled
        // meeting, an agenda item that no longer exists. All three are what the PRESENTER would see, and
        // reproducing their empty state is the point (FR-165): a preview that showed more than its subject
        // would get is not a preview.
        app.MapGet("/api/session-preview", async (Guid meetingId, Guid topicId, ISender sender, CancellationToken ct) =>
        {
            var session = await sender.Send(new GetPresenterSessionPreviewQuery(meetingId, topicId), ct);
            return session is null ? Results.NoContent() : Results.Ok(session);
        })
        .WithTags("Session")
        .RequireAuthorization();

        return app;
    }
}
