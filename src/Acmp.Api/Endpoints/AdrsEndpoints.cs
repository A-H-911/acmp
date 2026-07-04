using Acmp.Modules.Governance.Application.Features.ApproveAdr;
using Acmp.Modules.Governance.Application.Features.ChangeAdrStatus;
using Acmp.Modules.Governance.Application.Features.CreateAdr;
using Acmp.Modules.Governance.Application.Features.GetAdrByKey;
using Acmp.Modules.Governance.Application.Features.GetAdrsRegister;
using Acmp.Modules.Governance.Application.Features.ProposeAdr;
using Acmp.Modules.Governance.Application.Features.SupersedeAdr;
using Acmp.Modules.Governance.Application.Features.UpdateAdrDraft;
using Acmp.Modules.Governance.Domain.Enums;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md). The group requires authentication (401 without a token,
// AC-008); each mutating route adds its docs/10 policy (403 for the wrong role). The MediatR
// AuthorizationBehavior re-checks roles at the application boundary (defence in depth, guardrail 4). Reads
// are committee-wide; create/edit/propose/request-changes are Adr.Create; approve is Adr.Approve;
// deprecate/supersede are Adr.Supersede.
public static class AdrsEndpoints
{
    public static IEndpointRouteBuilder MapAdrEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/adrs").WithTags("Adrs").RequireAuthorization();

        // Register — any authenticated committee member (read-all), default newest first.
        group.MapGet("/", async (ISender sender, CancellationToken ct,
            AdrStatus[]? status = null, string? search = null,
            string sortBy = "created", string sortDir = "desc", int page = 1, int pageSize = 25) =>
            Results.Ok(await sender.Send(new GetAdrsRegisterQuery(
                status is { Length: > 0 } ? status : null, search, sortBy, sortDir, page, pageSize), ct)));

        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var adr = await sender.Send(new GetAdrByKeyQuery(key), ct);
            return adr is null ? Results.NotFound() : Results.Ok(adr);
        });

        // W17: author a new ADR (Draft).
        group.MapPost("/", async (CreateAdrBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateAdrCommand(body.Title, body.Context, body.DecisionDrivers,
                body.DecisionText, body.ConsequencesPositive, body.ConsequencesNegative, body.Options), ct);
            return Results.Created($"/api/adrs/{result.Key}", result);
        }).RequireAuthorization(Policies.AdrCreate);

        // Revise a Draft.
        group.MapPut("/{id:guid}/draft", async (Guid id, UpdateAdrDraftBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new UpdateAdrDraftCommand(id, body.Title, body.Context, body.DecisionDrivers,
                body.DecisionText, body.ConsequencesPositive, body.ConsequencesNegative, body.Options), ct)))
            .RequireAuthorization(Policies.AdrCreate);

        // W17: submit for approval (Draft → Proposed) — notifies reviewers.
        group.MapPost("/{id:guid}/propose", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ProposeAdrCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.AdrCreate);

        // Request changes (Proposed → Draft).
        group.MapPost("/{id:guid}/request-changes", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RequestAdrChangesCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.AdrCreate);

        // W17: approve (Proposed → Approved) — notifies the committee.
        group.MapPost("/{id:guid}/approve", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ApproveAdrCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.AdrApprove);

        // W21: deprecate (Approved → Deprecated) — notifies the committee.
        group.MapPost("/{id:guid}/deprecate", async (Guid id, DeprecateAdrBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeprecateAdrCommand(id, body.Reason), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.AdrSupersede);

        // W21: supersede (author a new ADR, approve it, mark the prior Superseded) — notifies the committee.
        group.MapPost("/{id:guid}/supersede", async (Guid id, SupersedeAdrBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SupersedeAdrCommand(id, body.Title, body.Context, body.DecisionDrivers,
                body.DecisionText, body.ConsequencesPositive, body.ConsequencesNegative, body.Options, body.Reason), ct);
            return Results.Created($"/api/adrs/{result.Key}", result);
        }).RequireAuthorization(Policies.AdrSupersede);

        return app;
    }

    public sealed record CreateAdrBody(
        LocalizedString Title, LocalizedString Context, LocalizedString? DecisionDrivers,
        LocalizedString DecisionText, LocalizedString? ConsequencesPositive, LocalizedString? ConsequencesNegative,
        IReadOnlyList<AdrOptionRequest>? Options);

    public sealed record UpdateAdrDraftBody(
        LocalizedString Title, LocalizedString Context, LocalizedString? DecisionDrivers,
        LocalizedString DecisionText, LocalizedString? ConsequencesPositive, LocalizedString? ConsequencesNegative,
        IReadOnlyList<AdrOptionRequest>? Options);

    public sealed record DeprecateAdrBody(LocalizedString Reason);

    public sealed record SupersedeAdrBody(
        LocalizedString Title, LocalizedString Context, LocalizedString? DecisionDrivers,
        LocalizedString DecisionText, LocalizedString? ConsequencesPositive, LocalizedString? ConsequencesNegative,
        IReadOnlyList<AdrOptionRequest>? Options, LocalizedString Reason);
}
