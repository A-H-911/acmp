using Acmp.Modules.Membership.Application.Features.AssignStreams;
using Acmp.Modules.Membership.Application.Features.CreateDelegation;
using Acmp.Modules.Membership.Application.Features.DeactivateMember;
using Acmp.Modules.Membership.Application.Features.GetMembers;
using Acmp.Modules.Membership.Application.Features.GetStreams;
using Acmp.Modules.Membership.Application.Features.ProvisionCurrentUser;
using Acmp.Shared.Authorization;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer: no business logic, delegates to MediatR (CLAUDE.md coding standards).
// The group requires authentication (401 without a token, AC-008); admin-only operations add the
// docs/10 policy that returns 403 for an authenticated-but-unauthorized role.
public static class MembershipEndpoints
{
    public static IEndpointRouteBuilder MapMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/members").WithTags("Membership").RequireAuthorization();

        // Member directory — any authenticated user, any role (AC-059).
        group.MapGet("/", async (ISender sender, CancellationToken ct, bool includeInactive = false) =>
            Results.Ok(await sender.Send(new GetMembersQuery(includeInactive), ct)));

        group.MapGet("/streams", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetStreamsQuery(), ct)));

        // JIT provisioning of the caller's profile from Keycloak claims (ADR-0004); SPA calls on login.
        group.MapPost("/me", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ProvisionCurrentUserCommand(), ct)));

        // Membership administration — Administrator only (docs/10 row 27, Admin.Users / SoD-5).
        group.MapPost("/{publicId:guid}/deactivate", async (Guid publicId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeactivateMemberCommand(publicId), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.AdminUsers);

        group.MapPut("/{publicId:guid}/streams", async (Guid publicId, IReadOnlyList<Guid> streamPublicIds, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new AssignStreamsCommand(publicId, streamPublicIds), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.AdminUsers);

        // Delegate authority for a bounded window — Chairman/Secretary (docs/10 row 28, Auth.Delegate).
        group.MapPost("/delegations", async (CreateDelegationCommand command, ISender sender, CancellationToken ct) =>
        {
            var publicId = await sender.Send(command, ct);
            return Results.Created($"/api/members/delegations/{publicId}", new { publicId });
        }).RequireAuthorization(Policies.AuthDelegate);

        return app;
    }
}
