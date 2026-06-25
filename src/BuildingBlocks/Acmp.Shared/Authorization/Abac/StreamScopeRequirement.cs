using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Acmp.Shared.Authorization.Abac;

// docs/10 §E.1 stream scope for WRITE actions. Read is committee-wide by settled decision
// (README §C, OQ-AUTH-001 = read-visible/write-scoped) so this constrains mutation only.
public sealed class StreamScopeRequirement : IAuthorizationRequirement
{
}

// Committee-wide roles bypass stream scope; stream-bounded roles must intersect the resource's
// affected streams. Used by P5+ write paths against real aggregates; unit-tested in P4.
public sealed class StreamScopeHandler : AuthorizationHandler<StreamScopeRequirement, IStreamScopedResource>
{
    private static readonly string[] CommitteeWide =
        { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Auditor, AcmpRoles.Administrator };

    private readonly IUserStreamProvider _streams;

    public StreamScopeHandler(IUserStreamProvider streams) => _streams = streams;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, StreamScopeRequirement requirement, IStreamScopedResource resource)
    {
        if (CommitteeWide.Any(context.User.IsInRole))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? context.User.FindFirst("sub")?.Value;
        if (userId is null)
            return;

        if (resource.AffectedStreams.Count == 0)
        {
            context.Succeed(requirement);
            return;
        }

        var assigned = await _streams.GetAssignedStreamsAsync(userId);
        if (resource.AffectedStreams.Intersect(assigned, StringComparer.OrdinalIgnoreCase).Any())
            context.Succeed(requirement);
    }
}
