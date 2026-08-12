using Acmp.Modules.Meetings.Application.Features.GetMySession;
using MediatR;

namespace Acmp.Api.Endpoints;

// FR-159 / AC-092 / DEC-037 — /session, the guest presenter's surface and the ONLY committee content
// a Guest can reach (the deny-by-default gate in GuestSurfaceMiddleware allowlists exactly this group).
//
// NO ROUTE PARAMETERS ANYWHERE, deliberately. Both endpoints answer for the CALLER, so a guest cannot
// name somebody else's meeting, topic or attachment — the question has no room to say it. The role
// restriction (Guest + Chairman/Secretary for preview) is on the queries themselves, so it holds at
// the application boundary and not only here, which DEC-037 requires in as many words.
public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/session").WithTags("Session").RequireAuthorization();

        // 204 rather than 404 when the caller has no slot: "you are not presenting" is a normal state
        // for a Chairman or Secretary opening the preview, not a missing resource.
        group.MapGet("/me", async (ISender sender, CancellationToken ct) =>
        {
            var session = await sender.Send(new GetMySessionQuery(), ct);
            return session is null ? Results.NoContent() : Results.Ok(session);
        });

        // A short-lived pre-signed URL, handed to the browser rather than streaming bytes through the
        // API (ADR-0014 / NFR-027). 404 when the attachment is not on the caller's own slot — the same
        // answer as an attachment that does not exist, so the response cannot be used to probe for one.
        group.MapGet("/materials/{attachmentId:guid}", async (Guid attachmentId, ISender sender, CancellationToken ct) =>
        {
            var url = await sender.Send(new GetMyMaterialUrlQuery(attachmentId), ct);
            return url is null ? Results.NotFound() : Results.Ok(new MaterialUrlResponse(url));
        });

        return app;
    }

    public sealed record MaterialUrlResponse(string Url);
}
