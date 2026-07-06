using Acmp.Modules.Governance.Application.Features.ApproveInvariant;
using Acmp.Modules.Governance.Application.Features.ChangeInvariantStatus;
using Acmp.Modules.Governance.Application.Features.CreateInvariant;
using Acmp.Modules.Governance.Application.Features.GetInvariantByKey;
using Acmp.Modules.Governance.Application.Features.GetInvariantsRegister;
using Acmp.Modules.Governance.Application.Features.ProposeInvariant;
using Acmp.Modules.Governance.Application.Features.SupersedeInvariant;
using Acmp.Modules.Governance.Application.Features.UpdateInvariantDraft;
using Acmp.Modules.Governance.Domain.Enums;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md). The group requires authentication (401 without a token,
// AC-008); each mutating route adds its docs/domain/permission-role-matrix.md policy (403 for the wrong role). The MediatR
// AuthorizationBehavior re-checks roles at the application boundary (defence in depth, guardrail 4). Reads
// are committee-wide; create/edit/propose/request-changes are Invariant.Create; approve is Invariant.Approve;
// retire/supersede are Invariant.Approve (there is no separate supersede policy for invariants, docs/domain/permission-role-matrix.md §22).
public static class InvariantsEndpoints
{
    public static IEndpointRouteBuilder MapInvariantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invariants").WithTags("Invariants").RequireAuthorization();

        // Register — any authenticated committee member (read-all), default newest first.
        group.MapGet("/", async (ISender sender, CancellationToken ct,
            InvariantStatus[]? status = null, string? search = null,
            string sortBy = "created", string sortDir = "desc", int page = 1, int pageSize = 25) =>
            Results.Ok(await sender.Send(new GetInvariantsRegisterQuery(
                status is { Length: > 0 } ? status : null, search, sortBy, sortDir, page, pageSize), ct)));

        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var inv = await sender.Send(new GetInvariantByKeyQuery(key), ct);
            return inv is null ? Results.NotFound() : Results.Ok(inv);
        });

        // W18: author a new invariant (Draft).
        group.MapPost("/", async (CreateInvariantBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateInvariantCommand(body.Category, body.Scope, body.Statement,
                body.Rationale, body.ExceptionsPolicy, body.OwnerUserId, body.OwnerName), ct);
            return Results.Created($"/api/invariants/{result.Key}", result);
        }).RequireAuthorization(Policies.InvariantCreate);

        // Revise a Draft.
        group.MapPut("/{id:guid}/draft", async (Guid id, UpdateInvariantDraftBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new UpdateInvariantDraftCommand(id, body.Category, body.Scope, body.Statement,
                body.Rationale, body.ExceptionsPolicy, body.OwnerUserId, body.OwnerName), ct)))
            .RequireAuthorization(Policies.InvariantCreate);

        // W18: submit for approval (Draft → Proposed) — notifies reviewers.
        group.MapPost("/{id:guid}/propose", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ProposeInvariantCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.InvariantCreate);

        // Request changes (Proposed → Draft).
        group.MapPost("/{id:guid}/request-changes", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RequestInvariantChangesCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.InvariantCreate);

        // W18: approve/activate (Proposed → Active) — notifies the committee.
        group.MapPost("/{id:guid}/approve", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ApproveInvariantCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.InvariantApprove);

        // W21: retire (Active → Retired) — notifies the committee.
        group.MapPost("/{id:guid}/retire", async (Guid id, RetireInvariantBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RetireInvariantCommand(id, body.Reason), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.InvariantApprove);

        // W21: supersede (author a new invariant, activate it, mark the prior Superseded) — notifies the committee.
        group.MapPost("/{id:guid}/supersede", async (Guid id, SupersedeInvariantBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SupersedeInvariantCommand(id, body.Category, body.Scope, body.Statement,
                body.Rationale, body.ExceptionsPolicy, body.OwnerUserId, body.OwnerName, body.Reason), ct);
            return Results.Created($"/api/invariants/{result.Key}", result);
        }).RequireAuthorization(Policies.InvariantApprove);

        return app;
    }

    public sealed record CreateInvariantBody(
        InvariantCategory Category, InvariantScope Scope, LocalizedString Statement, LocalizedString Rationale,
        LocalizedString? ExceptionsPolicy, string OwnerUserId, string OwnerName);

    public sealed record UpdateInvariantDraftBody(
        InvariantCategory Category, InvariantScope Scope, LocalizedString Statement, LocalizedString Rationale,
        LocalizedString? ExceptionsPolicy, string OwnerUserId, string OwnerName);

    public sealed record RetireInvariantBody(LocalizedString Reason);

    public sealed record SupersedeInvariantBody(
        InvariantCategory Category, InvariantScope Scope, LocalizedString Statement, LocalizedString Rationale,
        LocalizedString? ExceptionsPolicy, string OwnerUserId, string OwnerName, LocalizedString Reason);
}
