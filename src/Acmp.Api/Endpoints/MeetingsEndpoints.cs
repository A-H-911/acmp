using Acmp.Modules.Meetings.Application.Features.AgendaBuilder;
using Acmp.Modules.Meetings.Application.Features.CancelMeeting;
using Acmp.Modules.Meetings.Application.Features.ConductMeeting;
using Acmp.Modules.Meetings.Application.Features.GetMeetingDetail;
using Acmp.Modules.Meetings.Application.Features.GetMeetings;
using Acmp.Modules.Meetings.Application.Features.PublishAgenda;
using Acmp.Modules.Meetings.Application.Features.ScheduleMeeting;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Shared.Authorization;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md). The group requires authentication (401 without a token,
// AC-008); each mutating route adds its docs/10 policy (403 for the wrong role). The MediatR
// AuthorizationBehavior re-checks roles at the application boundary (defence in depth, guardrail 4).
public static class MeetingsEndpoints
{
    public static IEndpointRouteBuilder MapMeetingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/meetings").WithTags("Meetings").RequireAuthorization();

        // Reads — any authenticated committee member (committee-wide read).
        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetMeetingsQuery(), ct)));

        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var meeting = await sender.Send(new GetMeetingDetailQuery(key), ct);
            return meeting is null ? Results.NotFound() : Results.Ok(meeting);
        });

        // W5: schedule a meeting (creates the meeting + a draft agenda).
        group.MapPost("/", async (ScheduleMeetingCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/meetings/{result.Key}", result);
        }).RequireAuthorization(Policies.MeetingSchedule);

        group.MapPost("/{id:guid}/cancel", async (Guid id, ReasonBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CancelMeetingCommand(id, body.Reason), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.MeetingSchedule);

        // W6: agenda building.
        group.MapPost("/{id:guid}/agenda/items", async (Guid id, AddAgendaItemBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new AddAgendaItemCommand(id, body.TopicId, body.TopicKey, body.TopicTitle,
                body.Urgent, body.TimeboxMinutes, body.PresenterUserId, body.PresenterName), ct)))
            .RequireAuthorization(Policies.AgendaPublish);

        group.MapDelete("/{id:guid}/agenda/items/{topicId:guid}", async (Guid id, Guid topicId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new RemoveAgendaItemCommand(id, topicId), ct)))
            .RequireAuthorization(Policies.AgendaPublish);

        group.MapPost("/{id:guid}/agenda/items/{topicId:guid}/move", async (Guid id, Guid topicId, MoveBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new MoveAgendaItemCommand(id, topicId, body.Delta), ct)))
            .RequireAuthorization(Policies.AgendaPublish);

        group.MapPost("/{id:guid}/agenda/items/{topicId:guid}/timebox", async (Guid id, Guid topicId, TimeboxBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new SetAgendaItemTimeboxCommand(id, topicId, body.Minutes), ct)))
            .RequireAuthorization(Policies.AgendaPublish);

        group.MapPost("/{id:guid}/agenda/items/{topicId:guid}/presenter", async (Guid id, Guid topicId, PresenterBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new AssignPresenterCommand(id, topicId, body.PresenterUserId, body.PresenterName), ct)))
            .RequireAuthorization(Policies.AgendaPublish);

        // W6: publish & notify (flips each topic to Scheduled; notifies committee members — P6b).
        group.MapPost("/{id:guid}/agenda/publish", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new PublishAgendaCommand(id), ct)))
            .RequireAuthorization(Policies.AgendaPublish);

        // W7: start / end the live meeting.
        group.MapPost("/{id:guid}/start", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new StartMeetingCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.MeetingSchedule);

        group.MapPost("/{id:guid}/end", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new EndMeetingCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.MeetingSchedule);

        // W8: record attendance / apologies.
        group.MapPost("/{id:guid}/attendance", async (Guid id, AttendanceBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new MarkAttendanceCommand(id, body.UserId, body.Name, body.Role, body.Status, body.IsVotingEligible), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.AttendanceRecord);

        // W9: capture the discussion note for an agenda topic.
        group.MapPost("/{id:guid}/discussion", async (Guid id, DiscussionBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CaptureDiscussionCommand(id, body.TopicId, body.Body), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.MinutesCapture);

        // W7: record per-item actual time + outcome.
        group.MapPost("/{id:guid}/agenda/items/{topicId:guid}/actual-time", async (Guid id, Guid topicId, ActualTimeBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RecordActualTimeCommand(id, topicId, body.ActualMinutes, body.Outcome), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.MeetingSchedule);

        return app;
    }

    public sealed record ReasonBody(string Reason);
    public sealed record AddAgendaItemBody(Guid TopicId, string TopicKey, string TopicTitle, bool Urgent,
        int TimeboxMinutes, Guid? PresenterUserId, string? PresenterName);
    public sealed record MoveBody(int Delta);
    public sealed record TimeboxBody(int Minutes);
    public sealed record PresenterBody(Guid PresenterUserId, string PresenterName);
    public sealed record AttendanceBody(Guid UserId, string Name, AttendanceRole Role, AttendanceStatus Status, bool IsVotingEligible);
    public sealed record DiscussionBody(Guid TopicId, string Body);
    public sealed record ActualTimeBody(int ActualMinutes, AgendaItemOutcome? Outcome);
}
