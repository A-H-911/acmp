using Acmp.Modules.Topics.Application.Features.AcceptTopic;
using Acmp.Modules.Topics.Application.Features.AddTopicComment;
using Acmp.Modules.Topics.Application.Features.AttachFileToTopic;
using Acmp.Modules.Topics.Application.Features.ConvertResearchToTopic;
using Acmp.Modules.Topics.Application.Features.DeferTopic;
using Acmp.Modules.Topics.Application.Features.GetBacklog;
using Acmp.Modules.Topics.Application.Features.GetTopicDetail;
using Acmp.Modules.Topics.Application.Features.PrepareTopic;
using Acmp.Modules.Topics.Application.Features.PrioritizeTopic;
using Acmp.Modules.Topics.Application.Features.RejectTopic;
using Acmp.Modules.Topics.Application.Features.SubmitTopic;
using Acmp.Modules.Topics.Application.Features.UpdateTopic;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Authorization;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md). The group requires authentication (401 without a token,
// AC-008); RBAC endpoints add the docs/domain/permission-role-matrix.md policy (403 for the wrong role); ABAC endpoints (prepare) only
// authenticate here — the handler runs the per-resource owner check (AC-009/034).
public static class TopicEndpoints
{
    public static IEndpointRouteBuilder MapTopicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/topics").WithTags("Topics").RequireAuthorization();

        // Backlog — any authenticated user (committee-wide read, AC-059 analogue).
        group.MapGet("/", async (ISender sender, CancellationToken ct,
            TopicStatus[]? status = null, TopicType? type = null, string? stream = null,
            TopicUrgency? urgency = null, Guid? ownerId = null, string? search = null,
            bool includeClosed = false, string sortBy = "age", string sortDir = "desc",
            int page = 1, int pageSize = 25) =>
            Results.Ok(await sender.Send(new GetBacklogQuery(
                status is { Length: > 0 } ? status : null, type, stream, urgency, ownerId, search,
                includeClosed, sortBy, sortDir, page, pageSize), ct)));

        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var topic = await sender.Send(new GetTopicDetailQuery(key), ct);
            return topic is null ? Results.NotFound() : Results.Ok(topic);
        });

        // W1: submit a topic for triage.
        group.MapPost("/", async (SubmitTopicCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/topics/{result.Key}", result);
        }).RequireAuthorization(Policies.TopicSubmit);

        // W16 / FR-113: convert a research mission (or one of its recommendations) into a new execution topic,
        // linked back by an Informs traceability edge. 409 if the source is ineligible or already converted.
        group.MapPost("/from-research", async (ConvertResearchToTopicCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/topics/{result.Key}", result);
        }).RequireAuthorization(Policies.TopicSubmit);

        // W2/W20: triage actions.
        group.MapPost("/{id:guid}/accept", async (Guid id, AcceptTopicBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new AcceptTopicCommand(id, body.OwnerId, body.OwnerName), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.TopicTriage);

        group.MapPost("/{id:guid}/reject", async (Guid id, ReasonBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RejectTopicCommand(id, body.Reason), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.TopicTriage);

        group.MapPost("/{id:guid}/defer", async (Guid id, DeferTopicBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeferTopicCommand(id, body.Reason, body.RevisitOn), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.TopicTriage);

        // W4: mark prepared — ABAC (Owner/Secretary) enforced in the handler.
        group.MapPost("/{id:guid}/prepare", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new PrepareTopicCommand(id), ct);
            return Results.NoContent();
        });

        // W3: backlog prioritization.
        group.MapPut("/{id:guid}/priority", async (Guid id, PriorityBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new PrioritizeTopicCommand(id, body.Priority), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.BacklogPrioritize);

        // Edit (AC-034) — phase-aware authorization in the handler.
        group.MapPut("/{id:guid}", async (Guid id, UpdateTopicBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new UpdateTopicCommand(id, body.Title, body.Description, body.Justification,
                body.Urgency, body.Streams, body.Systems, body.Tags), ct);
            return Results.NoContent();
        });

        // BL-033: discussion comment — any authenticated member.
        group.MapPost("/{id:guid}/comments", async (Guid id, ReasonBody body, ISender sender, CancellationToken ct) =>
        {
            var commentId = await sender.Send(new AddTopicCommentCommand(id, body.Reason), ct);
            return Results.Created($"/api/topics/{id}/comments/{commentId}", new { id = commentId });
        });

        // AC-049/050: attach a file (multipart). Size/MIME validated in the handler.
        group.MapPost("/{id:guid}/attachments", async (Guid id, IFormFile file, ISender sender, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var dto = await sender.Send(new AttachFileToTopicCommand(id, file.FileName,
                file.ContentType, file.Length, stream), ct);
            return Results.Created($"/api/topics/{id}/attachments/{dto.Id}", dto);
        }).DisableAntiforgery()
          .RequireRateLimiting(Acmp.Api.Infrastructure.RateLimitPolicies.Upload);

        return app;
    }

    public sealed record AcceptTopicBody(Guid OwnerId, string OwnerName);
    public sealed record ReasonBody(string Reason);
    public sealed record DeferTopicBody(string Reason, DateTimeOffset? RevisitOn);
    public sealed record PriorityBody(int Priority);
    public sealed record UpdateTopicBody(string Title, string Description, string Justification,
        TopicUrgency Urgency, IReadOnlyList<string> Streams, IReadOnlyList<string> Systems, IReadOnlyList<string> Tags);
}
