using Acmp.Modules.Membership.Application.Features.AssignRoles;
using Acmp.Modules.Membership.Application.Features.AssignStreams;
using Acmp.Modules.Membership.Application.Features.CreateDelegation;
using Acmp.Modules.Membership.Application.Features.DeactivateMember;
using Acmp.Modules.Membership.Application.Features.GetMembers;
using Acmp.Modules.Membership.Application.Features.GetStreams;
using Acmp.Modules.Membership.Application.Features.InviteUser;
using Acmp.Modules.Membership.Application.Features.ProvisionCurrentUser;
using Acmp.Modules.Membership.Application.Features.ReconcileIdentityAccounts;
using Acmp.Shared.Authorization;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer: no business logic, delegates to MediatR (CLAUDE.md coding standards).
// The group requires authentication (401 without a token, AC-008); admin-only operations add the
// docs/domain/permission-role-matrix.md policy that returns 403 for an authenticated-but-unauthorized role.
public static class MembershipEndpoints
{
    // Body shape for the roles endpoint; the member is the route id, so it is not repeated here.
    // ConfirmedPrivileged carries guard 2's explicit acknowledgement from the UI to the server —
    // the server refuses without it, so the confirmation is a real gate rather than a dialog.
    public sealed record AssignRolesRequest(IReadOnlyCollection<string> Roles, bool ConfirmedPrivileged = false);

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

        // FR-156 / AC-088 — invite a user (Administrator or Secretary; DEC-038).
        // The temporary password is in THIS response and nowhere else: it is not persisted, not
        // logged, and no later query can return it. "No email in v1" is a hard constraint, so the
        // inviter passing it on is the delivery channel.
        group.MapPost("/invite", async (InviteUserCommand command, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(command, ct)));

        // FR-157 / AC-089 / AC-090 — assign roles. Guards (no self-change, confirmed privileged
        // grant, never zero Administrators) are enforced in the handler, server-side, so hiding the
        // control in the UI is never what stops a refused change.
        group.MapPut("/{publicId:guid}/roles", async (
            Guid publicId, AssignRolesRequest body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new AssignRolesCommand(publicId, body.Roles, body.ConfirmedPrivileged), ct);
            return Results.NoContent();
        });

        // Membership administration — Administrator only (docs/domain/permission-role-matrix.md row 27, Admin.Users / SoD-5).
        group.MapPost("/{publicId:guid}/deactivate", async (Guid publicId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeactivateMemberCommand(publicId), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.AdminUsers);

        // DEC-046 / DEF-065 — create a member row, holding the wildcard stream, for every identity
        // account that has none. Administrator only, and Administrator is not stream-bounded, so this
        // stays reachable in the very deploy that starts refusing unassigned members.
        //
        // POST because it creates rows, and it is idempotent in the way that matters: a second run
        // finds those rows already provisioned and creates nothing.
        group.MapPost("/reconcile", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ReconcileIdentityAccountsCommand(), ct)))
            .RequireAuthorization(Policies.AdminUsers);

        group.MapPut("/{publicId:guid}/streams", async (Guid publicId, IReadOnlyList<Guid> streamPublicIds, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new AssignStreamsCommand(publicId, streamPublicIds), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.AdminUsers);

        // Delegate authority for a bounded window — Chairman/Secretary (docs/domain/permission-role-matrix.md row 28, Auth.Delegate).
        group.MapPost("/delegations", async (CreateDelegationCommand command, ISender sender, CancellationToken ct) =>
        {
            var publicId = await sender.Send(command, ct);
            return Results.Created($"/api/members/delegations/{publicId}", new { publicId });
        }).RequireAuthorization(Policies.AuthDelegate);

        return app;
    }
}
