using Acmp.Modules.Actions.Application.Features.ChangeActionStatus;
using Acmp.Modules.Actions.Application.Features.CreateAction;
using Acmp.Modules.Actions.Application.Features.GetActionByKey;
using Acmp.Modules.Actions.Application.Features.GetActionsRegister;
using Acmp.Modules.Actions.Application.Features.VerifyAction;
using Acmp.Modules.Actions.Domain.Enums;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md). The group requires authentication (401 without a token,
// AC-008); each mutating route adds its docs/10 policy (403 for the wrong role). The MediatR
// AuthorizationBehavior re-checks roles at the application boundary (defence in depth, guardrail 4). Reads
// are committee-wide; create + the lifecycle transitions are Action.Create; verify is Action.Verify (with
// the SoD-1 guard inside the handler).
public static class ActionsEndpoints
{
    public static IEndpointRouteBuilder MapActionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/actions").WithTags("Actions").RequireAuthorization();

        // Register — any authenticated committee member (read-all).
        group.MapGet("/", async (ISender sender, CancellationToken ct,
            ActionStatus[]? status = null, string? owner = null, bool overdue = false, string? search = null,
            string sortBy = "due", string sortDir = "asc", int page = 1, int pageSize = 25) =>
            Results.Ok(await sender.Send(new GetActionsRegisterQuery(
                status is { Length: > 0 } ? status : null, owner, overdue, search, sortBy, sortDir, page, pageSize), ct)));

        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var action = await sender.Send(new GetActionByKeyQuery(key), ct);
            return action is null ? Results.NotFound() : Results.Ok(action);
        });

        // W13: create a follow-up action.
        group.MapPost("/", async (CreateActionBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateActionCommand(
                body.Title, body.Description, body.Priority, body.OwnerUserId, body.OwnerName, body.DueDate,
                body.SourceType, body.SourceId, body.SourceKey, body.MeetingKey), ct);
            return Results.Created($"/api/actions/{result.Key}", result);
        }).RequireAuthorization(Policies.ActionCreate);

        // W14: lifecycle transitions (Action.Create = create/edit, docs/10 row 14).
        group.MapPost("/{id:guid}/start", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new StartActionCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ActionCreate);

        group.MapPost("/{id:guid}/block", async (Guid id, ReasonBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new BlockActionCommand(id, body.Reason), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ActionCreate);

        group.MapPost("/{id:guid}/unblock", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new UnblockActionCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ActionCreate);

        group.MapPost("/{id:guid}/progress", async (Guid id, ProgressBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new UpdateActionProgressCommand(id, body.ProgressPct), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ActionCreate);

        group.MapPost("/{id:guid}/complete", async (Guid id, CompleteActionBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CompleteActionCommand(id, body.CompletionNote), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ActionCreate);

        group.MapPost("/{id:guid}/cancel", async (Guid id, ReasonBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CancelActionCommand(id, body.Reason), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ActionCreate);

        // W14: verify completion (Action.Verify; SoD-1 verifier ≠ owner/completer enforced in the handler).
        group.MapPost("/{id:guid}/verify", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new VerifyActionCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.ActionVerify);

        return app;
    }

    public sealed record CreateActionBody(
        LocalizedString Title, LocalizedString? Description, ActionPriority Priority,
        string OwnerUserId, string OwnerName, DateTimeOffset? DueDate,
        ActionSourceType SourceType, Guid SourceId, string? SourceKey, string? MeetingKey);

    public sealed record ReasonBody(LocalizedString Reason);

    public sealed record ProgressBody(int ProgressPct);

    public sealed record CompleteActionBody(LocalizedString? CompletionNote);
}
