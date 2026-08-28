using Acmp.Modules.Membership.Application.Features.AssignRoles;
using Acmp.Modules.Membership.Application.Features.AssignStreams;
using Acmp.Modules.Membership.Application.Features.CreateDelegation;
using Acmp.Modules.Membership.Application.Features.CreateStream;
using Acmp.Modules.Membership.Application.Features.DeactivateMember;
using Acmp.Modules.Membership.Application.Features.GetMembers;
using Acmp.Modules.Membership.Application.Features.GetStreams;
using Acmp.Modules.Membership.Application.Features.InviteUser;
using Acmp.Modules.Membership.Application.Features.ProvisionCurrentUser;
using Acmp.Modules.Membership.Application.Features.ReactivateMember;
using Acmp.Modules.Membership.Application.Features.ReconcileIdentityAccounts;
using Acmp.Modules.Membership.Application.Features.RenameStream;
using Acmp.Modules.Membership.Application.Features.SetVotingEligibility;
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

    /// <summary>The desired state, sent explicitly rather than toggled server-side (DEF-041).</summary>
    /// <remarks>
    /// A "flip it" endpoint would make two clicks racing each other produce whichever order the
    /// server saw last, with no way for either caller to know what they set.
    /// </remarks>
    public sealed record VotingEligibilityRequest(bool IsVotingEligible);

    /// <summary>The stream's new bilingual display name. The CODE is absent on purpose.</summary>
    /// <remarks>
    /// Topics carry stream codes and the ABAC intersect resolves on them, so re-coding a live stream
    /// would silently re-scope every topic that names the old value. Leaving Code out of the request
    /// shape means the API cannot express that change at all, rather than relying on a handler to
    /// ignore it.
    /// </remarks>
    public sealed record RenameStreamRequest(string NameEn, string NameAr);

    public static IEndpointRouteBuilder MapMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/members").WithTags("Membership").RequireAuthorization();

        // Member directory — any authenticated user, any role (AC-059).
        group.MapGet("/", async (ISender sender, CancellationToken ct, bool includeInactive = false) =>
            Results.Ok(await sender.Send(new GetMembersQuery(includeInactive), ct)));

        group.MapGet("/streams", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetStreamsQuery(), ct)));

        // NFR-010 / WBS-24.7 — the stream taxonomy becomes configuration instead of a migration.
        // SEC-178 specifies screen 85 as "Stream list, edit inline, add stream button", Administrator
        // only; these are its two write paths. Both are guarded server-side, so hiding the controls in
        // the UI is never what stops a refused change.
        //
        // ⚠ There is no DELETE, and its absence is a decision rather than an omission: topics carry
        // stream CODES and members hold stream assignments, so removing a row would orphan live scope
        // references — the immutable-history asymmetry this project has already paid for once. A
        // stream that should no longer be used is renamed, not deleted.
        group.MapPost("/streams", async (CreateStreamCommand command, ISender sender, CancellationToken ct) =>
        {
            var publicId = await sender.Send(command, ct);
            return Results.Created($"/api/members/streams/{publicId}", new { publicId });
        }).RequireAuthorization(Policies.AdminUsers);

        // PUT carries only the bilingual name; the code is not editable (see RenameStreamCommand).
        group.MapPut("/streams/{publicId:guid}", async (
            Guid publicId, RenameStreamRequest body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RenameStreamCommand(publicId, body.NameEn, body.NameAr), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.AdminUsers);

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

        // DEF-041 / DEC-046 d4 — who may vote. CHAIRMAN OR SECRETARY, and the gate is the command's
        // own AllowedRoles rather than a per-endpoint policy: AuthorizationBehavior enforces it (the
        // invite and roles endpoints above rely on the same thing), a new policy would need a new
        // permission-matrix row, and an endpoint-layer 403 is the one nobody audits (DEF-056).
        // ⚠ Administrator is EXCLUDED on purpose — SoD-5 keeps that role out of committee content.
        group.MapPut("/{publicId:guid}/voting-eligibility", async (
            Guid publicId, VotingEligibilityRequest body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SetVotingEligibilityCommand(publicId, body.IsVotingEligible), ct);
            return Results.NoContent();
        });

        // Membership administration — Administrator only (docs/domain/permission-role-matrix.md row 27, Admin.Users / SoD-5).
        group.MapPost("/{publicId:guid}/deactivate", async (Guid publicId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeactivateMemberCommand(publicId), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.AdminUsers);

        // FR-162 / AC-111 / DEF-085 — the counterpart of deactivate. Without it a disabled member was
        // locked out permanently: re-inviting throws on the duplicate email and the Keycloak user can
        // never be deleted (DEF-029).
        //
        // The body is optional, and its ABSENCE means "clear the access window" rather than "leave it
        // alone" — a returning guest is re-admitted as an ordinary member unless the caller supplies a
        // new expiry. Leaving it alone is the one behaviour that must not exist, because an expired
        // window refuses the member independently of Status (ADR-0039).
        group.MapPost("/{publicId:guid}/reactivate", async (
            Guid publicId, ReactivateMemberBody? body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ReactivateMemberCommand(publicId, body?.AccessExpiresAt), ct);
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

    /// <param name="AccessExpiresAt">
    /// The member's access window after reactivation. NULL (or no body at all) CLEARS it, which is
    /// the ordinary re-admission; supply an instant to re-window a guest for another meeting. The
    /// window is always written, because an expired one refuses the member independently of Status
    /// (ADR-0039) and leaving it stale is the Active-but-refused state AC-111 forbids.
    /// </param>
    public sealed record ReactivateMemberBody(DateTimeOffset? AccessExpiresAt);
}
