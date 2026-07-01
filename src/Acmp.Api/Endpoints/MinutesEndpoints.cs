using Acmp.Modules.Meetings.Application.Features.ApproveMinutes;
using Acmp.Modules.Meetings.Application.Features.DraftMinutes;
using Acmp.Modules.Meetings.Application.Features.GetMinutesByKey;
using Acmp.Modules.Meetings.Application.Features.GetMinutesForMeeting;
using Acmp.Modules.Meetings.Application.Features.PublishMinutes;
using Acmp.Modules.Meetings.Application.Features.RequestMinutesChanges;
using Acmp.Modules.Meetings.Application.Features.ReviseMinutes;
using Acmp.Modules.Meetings.Application.Features.SubmitMinutesForReview;
using Acmp.Modules.Meetings.Application.Features.SupersedeMinutes;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md) for MinutesOfMeeting (W10). The group requires
// authentication (401 without a token); each mutating route adds its docs/10 policy (403 for the wrong
// role). Reads are committee-wide; draft/revise/submit = Minutes.Capture; request-changes/approve/publish/
// supersede = Minutes.Approve. Domain guards surface as 409 (Conflict) via the global handler; a stale
// RowVersion write is 409 too.
public static class MinutesEndpoints
{
    public static IEndpointRouteBuilder MapMinutesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/minutes").WithTags("Minutes").RequireAuthorization();

        // Reads — any authenticated committee member. List = version history for a meeting (a Guid);
        // detail = a key (optionally a specific version, else the current/head version).
        group.MapGet("/", async (Guid meeting, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetMinutesForMeetingQuery(meeting), ct)));

        group.MapGet("/{key}", async (string key, int? version, ISender sender, CancellationToken ct) =>
        {
            var minutes = await sender.Send(new GetMinutesByKeyQuery(key, version), ct);
            return minutes is null ? Results.NotFound() : Results.Ok(minutes);
        });

        // W10: start a Draft MoM for a meeting.
        group.MapPost("/", async (DraftMinutesBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DraftMinutesCommand(body.MeetingId, body.Summary), ct);
            return Results.Created($"/api/minutes/{result.Key}", result);
        }).RequireAuthorization(Policies.MinutesCapture);

        // W10: revise the draft body (Draft-only).
        group.MapPut("/{id:guid}", async (Guid id, ReviseMinutesBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ReviseMinutesCommand(id, body.Summary), ct)))
            .RequireAuthorization(Policies.MinutesCapture);

        // W10: submit for review (Draft → InReview).
        group.MapPost("/{id:guid}/submit", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SubmitMinutesForReviewCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.MinutesCapture);

        // W10 (AC-037): request changes (InReview → Draft) — Minutes.Approve.
        group.MapPost("/{id:guid}/request-changes", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RequestMinutesChangesCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.MinutesApprove);

        // W10 (AC-014): approve (InReview → Approved; soft SoD-2) — Minutes.Approve.
        group.MapPost("/{id:guid}/approve", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ApproveMinutesCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.MinutesApprove);

        // W10 (AC-038): publish (Approved → Published) + notify all members — Minutes.Approve.
        group.MapPost("/{id:guid}/publish", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new PublishMinutesCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.MinutesApprove);

        // W10 (AC-036): supersede an approved/published MoM with a corrected new version (returns the successor).
        group.MapPost("/{id:guid}/supersede", async (Guid id, SupersedeMinutesBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SupersedeMinutesCommand(id, body.Summary, body.Reason), ct);
            return Results.Created($"/api/minutes/{result.Key}?version={result.Version}", result);
        }).RequireAuthorization(Policies.MinutesApprove);

        return app;
    }

    public sealed record DraftMinutesBody(Guid MeetingId, LocalizedString Summary);

    public sealed record ReviseMinutesBody(LocalizedString Summary);

    public sealed record SupersedeMinutesBody(LocalizedString Summary, LocalizedString Reason);
}
