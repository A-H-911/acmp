using Acmp.Modules.Knowledge.Application.Features.CreateDocument;
using Acmp.Modules.Knowledge.Application.Features.CreateTemplate;
using Acmp.Modules.Knowledge.Application.Features.DeprecateTemplate;
using Acmp.Modules.Knowledge.Application.Features.DocumentLifecycle;
using Acmp.Modules.Knowledge.Application.Features.EditDocument;
using Acmp.Modules.Knowledge.Application.Features.EditTemplate;
using Acmp.Modules.Knowledge.Application.Features.GetDocumentByKey;
using Acmp.Modules.Knowledge.Application.Features.GetDocumentsRegister;
using Acmp.Modules.Knowledge.Application.Features.GetTemplateByKey;
using Acmp.Modules.Knowledge.Application.Features.GetTemplatesRegister;
using Acmp.Modules.Knowledge.Domain.Enums;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md). The group requires authentication (401 without a token,
// AC-008); each mutating route adds the docs/domain/permission-role-matrix.md Document.Manage policy (403 for
// the wrong role). The MediatR AuthorizationBehavior re-checks roles at the application boundary (defence in
// depth, guardrail 4). Reads are committee-wide; every mutation is Document.Manage.
//
// ABAC note (P15d): Document.Manage's allow-if-owner (Member/Reviewer) resolves ONLY via a topic-capability
// relationship, and a Document is NOT topic-scoped — so, exactly like the ADR endpoints, the AiO has no
// relationship to resolve and Chairman/Secretary are the effective writers; a bare Member/Reviewer create is a
// 403. OwnerUserId on the document is attribution, not an enforced endpoint gate.
public static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/knowledge/documents").WithTags("Knowledge").RequireAuthorization();

        // Register — any authenticated committee member (read-all), newest first by default.
        group.MapGet("/", async (ISender sender, CancellationToken ct,
            DocumentStatus[]? status = null, string? category = null, string? search = null,
            string sortBy = "created", string sortDir = "desc", int page = 1, int pageSize = 25) =>
            Results.Ok(await sender.Send(new GetDocumentsRegisterQuery(
                status is { Length: > 0 } ? status : null, category, search, sortBy, sortDir, page, pageSize), ct)));

        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var document = await sender.Send(new GetDocumentByKeyQuery(key), ct);
            return document is null ? Results.NotFound() : Results.Ok(document);
        });

        // FR-116: author a new document (Draft).
        group.MapPost("/", async (CreateDocumentBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateDocumentCommand(body.Title, body.Category, body.Body, body.Tags), ct);
            return Results.Created($"/api/knowledge/documents/{result.Key}", result);
        }).RequireAuthorization(Policies.DocumentManage);

        // FR-117: revise content (Draft or Published) — bumps Version + appends a snapshot.
        group.MapPut("/{id:guid}", async (Guid id, EditDocumentBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new EditDocumentCommand(id, body.Title, body.Category, body.Body), ct)))
            .RequireAuthorization(Policies.DocumentManage);

        // FR-116 lifecycle.
        group.MapPost("/{id:guid}/publish", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new PublishDocumentCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.DocumentManage);

        group.MapPost("/{id:guid}/archive", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ArchiveDocumentCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.DocumentManage);

        MapTemplateEndpoints(app);
        return app;
    }

    // FR-119: reusable artifact templates (TPL-). Same auth shape as documents — reads are committee-wide, every
    // mutation is Template.Manage (Chairman/Secretary/Administrator; no allow-if-owner).
    private static void MapTemplateEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/knowledge/templates").WithTags("Knowledge").RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct,
            TemplateStatus[]? status = null, TemplateTargetType? targetType = null, string? search = null,
            string sortBy = "created", string sortDir = "desc", int page = 1, int pageSize = 25) =>
            Results.Ok(await sender.Send(new GetTemplatesRegisterQuery(
                status is { Length: > 0 } ? status : null, targetType, search, sortBy, sortDir, page, pageSize), ct)));

        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var template = await sender.Send(new GetTemplateByKeyQuery(key), ct);
            return template is null ? Results.NotFound() : Results.Ok(template);
        });

        // FR-119: author a new template (Active).
        group.MapPost("/", async (CreateTemplateBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateTemplateCommand(body.Name, body.TargetType, body.Body), ct);
            return Results.Created($"/api/knowledge/templates/{result.Key}", result);
        }).RequireAuthorization(Policies.TemplateManage);

        // FR-119: revise Name + Body (bumps Version; TargetType is immutable).
        group.MapPut("/{id:guid}", async (Guid id, EditTemplateBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new EditTemplateCommand(id, body.Name, body.Body), ct)))
            .RequireAuthorization(Policies.TemplateManage);

        // FR-119: soft delete (Active → Deprecated; terminal).
        group.MapPost("/{id:guid}/deprecate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeprecateTemplateCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.TemplateManage);
    }

    public sealed record CreateDocumentBody(LocalizedString Title, string Category, LocalizedString Body, IReadOnlyList<string>? Tags);

    public sealed record EditDocumentBody(LocalizedString Title, string Category, LocalizedString Body);

    public sealed record CreateTemplateBody(LocalizedString Name, TemplateTargetType TargetType, string Body);

    public sealed record EditTemplateBody(LocalizedString Name, string Body);
}
