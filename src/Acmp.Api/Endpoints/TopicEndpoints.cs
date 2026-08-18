using Acmp.Modules.Topics.Application.Features.AcceptTopic;
using Acmp.Modules.Topics.Application.Features.AddTopicComment;
using Acmp.Modules.Topics.Application.Features.AttachFileToTopic;
using Acmp.Modules.Topics.Application.Features.CloseTopic;
using Acmp.Modules.Topics.Application.Features.ConvertResearchToTopic;
using Acmp.Modules.Topics.Application.Features.ConvertTopic;
using Acmp.Modules.Topics.Application.Features.DeferTopic;
using Acmp.Modules.Topics.Application.Features.GetBacklog;
using Acmp.Modules.Topics.Application.Features.GetTopicDetail;
using Acmp.Modules.Topics.Application.Features.MoveTopicPriority;
using Acmp.Modules.Topics.Application.Features.PrepareTopic;
using Acmp.Modules.Topics.Application.Features.PrioritizeTopic;
using Acmp.Modules.Topics.Application.Features.ReactivateTopic;
using Acmp.Modules.Topics.Application.Features.RejectTopic;
using Acmp.Modules.Topics.Application.Features.SetTopicConfidentiality;
using Acmp.Modules.Topics.Application.Features.ReopenTopic;
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

        // FR-161 / AC-110 — the way BACK from Deferred. Before this the revisit date recorded above
        // was displayed on the topic detail and could never be acted on.
        group.MapPost("/{id:guid}/reactivate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ReactivateTopicCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.TopicTriage);

        // FR-160 / AC-109 — the terminal transition. Without it Decided was a permanent resting state.
        group.MapPost("/{id:guid}/close", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CloseTopicCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.TopicTriage);

        // FR-045 / AC-112 — approved in the original plan, traced to WBS-5.7, never built until now.
        // Reuses ReasonBody: the justification is mandatory, as it is for reject and defer (FR-044).
        group.MapPost("/{id:guid}/reopen", async (Guid id, ReasonBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ReopenTopicCommand(id, body.Reason), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.TopicTriage);

        // FR-030 / AC-113 (SC-018): convert a Decided topic to a different type. Returns 201 with the
        // SUCCESSOR's key — the response body describes the new artifact, not the retired one, which is why
        // this is Created rather than NoContent like the other lifecycle transitions.
        group.MapPost("/{id:guid}/convert", async (Guid id, ConvertBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ConvertTopicCommand(id, body.TargetType, body.Reason), ct);
            return Results.Created($"/api/topics/{result.Key}", result);
        }).RequireAuthorization(Policies.TopicTriage);

        // FR-163 / C-AUTHZ-04: classify or declassify. Chairman + Secretary only (DEC-063 d2), gated
        // by AllowedRoles on the command AND the endpoint policy — the same two roles TopicTriage
        // carries. ⚠ Deliberately NOT Policies.TopicEdit: that policy now carries
        // ConfidentialityRequirement, so routing declassification through it would make lifting a
        // classification depend on being able to see the topic — circular for the case that matters.
        group.MapPut("/{id:guid}/confidentiality", async (Guid id, ConfidentialityBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SetTopicConfidentialityCommand(id, body.Restricted), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.TopicTriage);

        // W4: mark prepared — ABAC (Owner/Secretary) enforced in the handler.
        group.MapPost("/{id:guid}/prepare", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new PrepareTopicCommand(id), ct);
            return Results.NoContent();
        });

        // W3: backlog prioritization (absolute set — drag-and-drop / direct edit).
        group.MapPut("/{id:guid}/priority", async (Guid id, PriorityBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new PrioritizeTopicCommand(id, body.Priority), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.BacklogPrioritize);

        // AC-043 / FR-034: keyboard move-up/down reorder within the topic's kanban column (a single ±1 delta).
        group.MapPost("/{id:guid}/priority/move", async (Guid id, MoveBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new MoveTopicPriorityCommand(id, body.Delta), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.BacklogPrioritize);

        // Edit (AC-034) — phase-aware authorization in the handler.
        group.MapPut("/{id:guid}", async (Guid id, UpdateTopicBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new UpdateTopicCommand(id, body.Title, body.Description, body.Justification,
                body.Urgency, body.Streams, body.Systems, body.Tags, body.Scope), ct);
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
    // FR-030: ReasonBody is not reused here — conversion needs the TARGET TYPE as well, and the reason is
    // about why the type is changing rather than why a transition was refused.
    public sealed record ConvertBody(TopicType TargetType, string Reason);
    // PUT, not POST: setting a classification is idempotent and the body carries the DESIRED state
    // rather than an action, so a repeated call is a no-op instead of a second event.
    public sealed record ConfidentialityBody(bool Restricted);
    public sealed record DeferTopicBody(string Reason, DateTimeOffset? RevisitOn);
    public sealed record PriorityBody(int Priority);
    public sealed record MoveBody(int Delta);
    // Scope is nullable and OMITTING IT MEANS "leave it alone", which is what makes this safe to add
    // to an existing body: a caller that does not know about scope cannot silently reset an elevated
    // topic back to a derived value (DEF-058).
    public sealed record UpdateTopicBody(string Title, string Description, string Justification,
        TopicUrgency Urgency, IReadOnlyList<string> Streams, IReadOnlyList<string> Systems, IReadOnlyList<string> Tags,
        TopicScope? Scope = null);
}
