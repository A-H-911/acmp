using Acmp.Modules.Risks.Application.Features.AcceptRisk;
using Acmp.Modules.Risks.Application.Features.ChangeRiskStatus;
using Acmp.Modules.Risks.Application.Features.GetRiskByKey;
using Acmp.Modules.Risks.Application.Features.GetRisksRegister;
using Acmp.Modules.Risks.Application.Features.ManageMitigations;
using Acmp.Modules.Risks.Application.Features.RaiseRisk;
using Acmp.Modules.Risks.Domain.Enums;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md). The group requires authentication (401 without a token,
// AC-008); each mutating route adds its docs/domain/permission-role-matrix.md policy (403 for the wrong role). The MediatR
// AuthorizationBehavior re-checks roles at the application boundary (defence in depth, guardrail 4). Reads
// are committee-wide; raise/mitigate/close/escalate are Risk.Manage; accept is the narrower Risk.Accept.
public static class RisksEndpoints
{
    public static IEndpointRouteBuilder MapRiskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/risks").WithTags("Risks").RequireAuthorization();

        // Register — any authenticated committee member (read-all), default sorted by exposure.
        group.MapGet("/", async (ISender sender, CancellationToken ct,
            RiskStatus[]? status = null, string? owner = null, RiskExposure[]? exposure = null, string? search = null,
            string sortBy = "exposure", string sortDir = "desc", int page = 1, int pageSize = 25) =>
            Results.Ok(await sender.Send(new GetRisksRegisterQuery(
                status is { Length: > 0 } ? status : null, owner,
                exposure is { Length: > 0 } ? exposure : null, search, sortBy, sortDir, page, pageSize), ct)));

        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var risk = await sender.Send(new GetRiskByKeyQuery(key), ct);
            return risk is null ? Results.NotFound() : Results.Ok(risk);
        });

        // W15: raise a risk.
        group.MapPost("/", async (RaiseRiskBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RaiseRiskCommand(
                body.Title, body.Description, body.Likelihood, body.Impact, body.OwnerUserId, body.OwnerName,
                body.SubjectType, body.SubjectId, body.SubjectKey, body.InitialMitigation), ct);
            return Results.Created($"/api/risks/{result.Key}", result);
        }).RequireAuthorization(Policies.RiskManage);

        // W15: plan a mitigation.
        group.MapPost("/{id:guid}/mitigations", async (Guid id, AddMitigationBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new AddMitigationCommand(id, body.Description, body.Type, body.OwnerUserId, body.LinkedActionId, body.DueDate), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.RiskManage);

        // W15: advance a mitigation's status.
        group.MapPost("/{id:guid}/mitigations/{mitigationId:guid}/status",
            async (Guid id, Guid mitigationId, MitigationStatusBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SetMitigationStatusCommand(id, mitigationId, body.Status), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.RiskManage);

        // W15: begin mitigating (Open/Escalated → Mitigating).
        group.MapPost("/{id:guid}/begin-mitigation", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new BeginMitigationCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.RiskManage);

        // W15: close (Mitigating/Escalated → Closed).
        group.MapPost("/{id:guid}/close", async (Guid id, CloseRiskBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CloseRiskCommand(id, body.ClosureNote), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.RiskManage);

        // W15: escalate (Open/Mitigating → Escalated) — notifies Secretary + Chairman.
        group.MapPost("/{id:guid}/escalate", async (Guid id, EscalateRiskBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new EscalateRiskCommand(id, body.Reason, body.Target), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.RiskManage);

        // W15: accept (Open/Mitigating → Accepted) — Chairman/Secretary only (Risk.Accept).
        group.MapPost("/{id:guid}/accept", async (Guid id, AcceptRiskBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new AcceptRiskCommand(id, body.Rationale, body.Authority), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.RiskAccept);

        return app;
    }

    public sealed record RaiseRiskBody(
        LocalizedString Title, LocalizedString? Description, RiskLevel Likelihood, RiskLevel Impact,
        string OwnerUserId, string OwnerName, RiskSubjectType SubjectType, Guid SubjectId, string? SubjectKey,
        LocalizedString? InitialMitigation);

    public sealed record AddMitigationBody(
        LocalizedString Description, MitigationType Type, string? OwnerUserId, Guid? LinkedActionId, DateTimeOffset? DueDate);

    public sealed record MitigationStatusBody(MitigationStatus Status);

    public sealed record CloseRiskBody(LocalizedString? ClosureNote);

    public sealed record EscalateRiskBody(LocalizedString Reason, string Target);

    public sealed record AcceptRiskBody(LocalizedString Rationale, string Authority);
}
