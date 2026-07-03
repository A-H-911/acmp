using Acmp.Modules.Traceability.Application.Features.CreateRelationship;
using Acmp.Modules.Traceability.Application.Features.DeactivateRelationship;
using Acmp.Modules.Traceability.Application.Features.GetArtifactRelationships;
using Acmp.Modules.Traceability.Domain.Enums;
using Acmp.Shared.Authorization;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md). The group requires authentication (401 without a token). The
// panel read is committee-wide; creating/deactivating a typed edge is Traceability.Link (Chairman/Secretary).
// The MediatR AuthorizationBehavior re-checks roles at the application boundary (defence in depth, guardrail 4).
public static class TraceabilityEndpoints
{
    public static IEndpointRouteBuilder MapTraceabilityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/traceability").WithTags("Traceability").RequireAuthorization();

        // AC-062: the traceability panel for one artifact — active outgoing + incoming edges (one hop).
        group.MapGet("/{type}/{id:guid}", async (ArtifactType type, Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetArtifactRelationshipsQuery(type, id), ct)));

        // AC-063: create an explicit typed edge between two artifacts.
        group.MapPost("/", async (CreateRelationshipBody body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateRelationshipCommand(
                body.SourceType, body.SourceId, body.SourceKey, body.SourceTitle,
                body.TargetType, body.TargetId, body.TargetKey, body.TargetTitle,
                body.RelType, body.Notes), ct);
            return Results.Created($"/api/traceability/{body.SourceType}/{body.SourceId}", new { id });
        }).RequireAuthorization(Policies.TraceabilityLink);

        // Soft-delete an edge created in error (audited, never hard-deleted).
        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeactivateRelationshipCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.TraceabilityLink);

        return app;
    }

    public sealed record CreateRelationshipBody(
        ArtifactType SourceType, Guid SourceId, string SourceKey, string SourceTitle,
        ArtifactType TargetType, Guid TargetId, string TargetKey, string TargetTitle,
        RelationshipType RelType, string? Notes);
}
