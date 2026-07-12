using Acmp.Modules.Research.Application.Features.CreateMission;
using Acmp.Modules.Research.Application.Features.GetMissionByKey;
using Acmp.Modules.Research.Application.Features.GetMissionsRegister;
using Acmp.Modules.Research.Application.Features.ManageFindings;
using Acmp.Modules.Research.Application.Features.ManageRecommendations;
using Acmp.Modules.Research.Application.Features.MissionLifecycle;
using Acmp.Modules.Research.Application.Features.UpdateMissionDraft;
using Acmp.Modules.Research.Domain.Enums;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md). The group requires authentication (401 without a token,
// AC-008); each mutating route adds the docs/domain/permission-role-matrix.md Research.Manage policy (403 for
// the wrong role). The MediatR AuthorizationBehavior re-checks roles at the application boundary (defence in
// depth, guardrail 4). Reads are committee-wide; every mutation is Research.Manage.
//
// ABAC note (P15a): Research.Manage's allow-if-owner (Member/Reviewer) resolves ONLY via a topic-capability
// relationship, and a ResearchMission is NOT topic-scoped — so, exactly like the ADR endpoints, the AiO has no
// relationship to resolve and Chairman/Secretary are the effective writers; a bare Member/Reviewer create is a
// 403. OwnerUserId on the mission is attribution, not an enforced endpoint gate.
public static class ResearchEndpoints
{
    public static IEndpointRouteBuilder MapResearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/research").WithTags("Research").RequireAuthorization();

        // Register — any authenticated committee member (read-all), newest first by default.
        group.MapGet("/", async (ISender sender, CancellationToken ct,
            ResearchMissionStatus[]? status = null, string? search = null,
            string sortBy = "created", string sortDir = "desc", int page = 1, int pageSize = 25) =>
            Results.Ok(await sender.Send(new GetMissionsRegisterQuery(
                status is { Length: > 0 } ? status : null, search, sortBy, sortDir, page, pageSize), ct)));

        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var mission = await sender.Send(new GetMissionByKeyQuery(key), ct);
            return mission is null ? Results.NotFound() : Results.Ok(mission);
        });

        // FR-111: author a new mission (Proposed).
        group.MapPost("/", async (CreateMissionBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateMissionCommand(
                body.Title, body.Question, body.KeystonePackageRef, body.SourceTopicId), ct);
            return Results.Created($"/api/research/{result.Key}", result);
        }).RequireAuthorization(Policies.ResearchManage);

        // Revise a Proposed mission's fields.
        group.MapPut("/{id:guid}/draft", async (Guid id, UpdateMissionDraftBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new UpdateMissionDraftCommand(
                id, body.Title, body.Question, body.KeystonePackageRef, body.SourceTopicId), ct)))
            .RequireAuthorization(Policies.ResearchManage);

        // FR-111 lifecycle.
        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ActivateMissionCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ResearchManage);

        group.MapPost("/{id:guid}/complete", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CompleteMissionCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ResearchManage);

        group.MapPost("/{id:guid}/cancel", async (Guid id, ReasonBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CancelMissionCommand(id, body.Reason), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ResearchManage);

        // FR-113: findings.
        group.MapPost("/{id:guid}/findings", async (Guid id, AddFindingBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new AddFindingCommand(id, body.Summary, body.Detail, body.Confidence), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ResearchManage);

        group.MapPut("/{id:guid}/findings/{findingId:guid}", async (Guid id, Guid findingId, AddFindingBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new UpdateFindingCommand(id, findingId, body.Summary, body.Detail, body.Confidence), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ResearchManage);

        group.MapPost("/{id:guid}/findings/{findingId:guid}/verify", async (Guid id, Guid findingId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new VerifyFindingCommand(id, findingId), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ResearchManage);

        // FR-113: recommendations.
        group.MapPost("/{id:guid}/recommendations", async (Guid id, AddRecommendationBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new AddRecommendationCommand(id, body.Statement, body.Rationale, body.Priority, body.LinkedTopicId), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ResearchManage);

        group.MapPut("/{id:guid}/recommendations/{recommendationId:guid}", async (Guid id, Guid recommendationId, AddRecommendationBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new UpdateRecommendationCommand(id, recommendationId, body.Statement, body.Rationale, body.Priority, body.LinkedTopicId), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ResearchManage);

        group.MapPost("/{id:guid}/recommendations/{recommendationId:guid}/status", async (Guid id, Guid recommendationId, RecommendationStatusBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SetRecommendationStatusCommand(id, recommendationId, body.Status), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ResearchManage);

        return app;
    }

    public sealed record CreateMissionBody(LocalizedString Title, LocalizedString Question, string? KeystonePackageRef, Guid? SourceTopicId);

    public sealed record UpdateMissionDraftBody(LocalizedString Title, LocalizedString Question, string? KeystonePackageRef, Guid? SourceTopicId);

    public sealed record ReasonBody(LocalizedString Reason);

    public sealed record AddFindingBody(LocalizedString Summary, LocalizedString? Detail, Confidence Confidence);

    public sealed record AddRecommendationBody(LocalizedString Statement, LocalizedString? Rationale, RecommendationPriority Priority, Guid? LinkedTopicId);

    public sealed record RecommendationStatusBody(RecommendationStatus Status);
}
