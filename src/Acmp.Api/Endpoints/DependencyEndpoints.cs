using Acmp.Modules.Dependencies.Application.Features.CreateDependency;
using Acmp.Modules.Dependencies.Application.Features.GetDependenciesForArtifact;
using Acmp.Modules.Dependencies.Application.Features.GetDependenciesRegister;
using Acmp.Modules.Dependencies.Application.Features.GetDependencyByKey;
using Acmp.Modules.Dependencies.Application.Features.RemoveDependency;
using Acmp.Modules.Dependencies.Application.Features.ResolveDependency;
using Acmp.Modules.Dependencies.Domain.Enums;
using Acmp.Shared.Authorization;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md). The group requires authentication (401 without a token).
// Reads are committee-wide; creating/resolving/removing a dependency is Dependency.Create (Chairman/
// Secretary). The MediatR AuthorizationBehavior re-checks roles at the application boundary (defence in
// depth, guardrail 4). No try/catch — GlobalExceptionHandler maps domain exceptions (404/409/400).
public static class DependencyEndpoints
{
    public static IEndpointRouteBuilder MapDependencyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dependencies").WithTags("Dependencies").RequireAuthorization();

        // Register — any authenticated committee member (read-all); Removed excluded unless asked for.
        group.MapGet("/", async (ISender sender, CancellationToken ct,
            DependencyKind? kind = null, DependencyStatus? status = null, bool blockedOnly = false,
            string sortBy = "key", string sortDir = "asc", int page = 1, int pageSize = 25) =>
            Results.Ok(await sender.Send(new GetDependenciesRegisterQuery(
                kind, status, blockedOnly, sortBy, sortDir, page, pageSize), ct)));

        // Detail by display key (DPN-YYYY-###).
        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var dependency = await sender.Send(new GetDependencyByKeyQuery(key), ct);
            return dependency is null ? Results.NotFound() : Results.Ok(dependency);
        });

        // The dependency panel for one artifact — outbound + inbound edges.
        group.MapGet("/artifact/{type}/{id:guid}", async (DependencyEndpointType type, Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetDependenciesForArtifactQuery(type, id), ct)));

        // Create an explicit typed dependency between two artifacts.
        group.MapPost("/", async (CreateDependencyBody body, ISender sender, CancellationToken ct) =>
        {
            var key = await sender.Send(new CreateDependencyCommand(
                body.FromType, body.FromId, body.FromKey, body.FromTitle,
                body.ToType, body.ToId, body.ToKey, body.ToTitle,
                body.Kind, body.Note), ct);
            return Results.Created($"/api/dependencies/{key}", new { key });
        }).RequireAuthorization(Policies.DependencyCreate);

        // Mark a dependency satisfied (Open → Resolved).
        group.MapPost("/{id:guid}/resolve", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ResolveDependencyCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.DependencyCreate);

        // Retract a dependency created in error (Open → Removed, soft-delete).
        group.MapPost("/{id:guid}/remove", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RemoveDependencyCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.DependencyCreate);

        return app;
    }

    public sealed record CreateDependencyBody(
        DependencyEndpointType FromType, Guid FromId, string FromKey, string FromTitle,
        DependencyEndpointType ToType, Guid ToId, string ToKey, string ToTitle,
        DependencyKind Kind, string? Note);
}
