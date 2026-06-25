using Acmp.Modules.Membership.Application.Features.GetMembers;
using Acmp.Modules.Membership.Application.Features.InviteMember;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer: no business logic, delegates to MediatR (CLAUDE.md coding standards).
public static class MembershipEndpoints
{
    public static IEndpointRouteBuilder MapMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/members").WithTags("Membership");

        group.MapGet("/", async (ISender sender, CancellationToken ct, bool includeInactive = false) =>
            Results.Ok(await sender.Send(new GetMembersQuery(includeInactive), ct)));

        group.MapPost("/", async (ISender sender, InviteMemberCommand command, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return Results.Created("/api/members/" + result.PublicId, result);
        });

        return app;
    }
}
