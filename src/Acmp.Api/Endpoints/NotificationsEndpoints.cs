using Acmp.Modules.Notifications.Application.Features.GetNotifications;
using Acmp.Modules.Notifications.Application.Features.MarkRead;
using MediatR;

namespace Acmp.Api.Endpoints;

// The signed-in user's in-app notification center (ADR-0005, AC-051/053). No policy beyond authentication:
// every route is implicitly scoped to ICurrentUser in the handlers, so a user only ever reads/mutates
// their own items (guardrail 4 — the scope is the authorization). A mark-read miss returns 404 (the
// GlobalExceptionHandler maps KeyNotFoundException) — a stranger's id is indistinguishable from a missing one.
public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetNotificationsQuery(), ct)));

        group.MapPost("/{id:guid}/read", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new MarkNotificationReadCommand(id), ct);
            return Results.NoContent();
        });

        return app;
    }
}
