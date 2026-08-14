using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Acmp.Shared.Authorization.Abac;

// docs/domain/permission-role-matrix.md §E.1 stream scope for WRITE actions. Read is committee-wide by settled decision
// (README §C, OQ-AUTH-001 = read-visible/write-scoped) so this constrains mutation only.
public sealed class StreamScopeRequirement : IAuthorizationRequirement
{
}

// Stream-bounded roles must intersect the resource's affected streams; everyone else is unbounded.
// Used by P5+ write paths against real aggregates; unit-tested in P4.
public sealed class StreamScopeHandler : AuthorizationHandler<StreamScopeRequirement, IStreamScopedResource>
{
    // ⚠ THE SCOPED SET IS STATED POSITIVELY, AND THAT IS LOAD-BEARING (DEF-060, ADR-0043). The
    // matrix's §E.1 rule is "a Member/Reviewer/Submitter may act only on topics intersecting their
    // assigned streams" — those three roles and no others. This used to be expressed as the
    // complement of a committee-wide BYPASS list, which is not the same statement: it silently swept
    // in Guest, whom §E.3 bounds by a time window instead, and would have refused FR-159 guest
    // presenters their one write capability. A bypass list's complement is an inference; the scoped
    // set is the specification. Expressed this way, a role added later cannot fall into stream scope
    // by omission — the failure mode is "a new role is unbounded", which is visible in this list,
    // rather than "a new role is refused everything", which is visible only at runtime.
    private static readonly string[] StreamBounded =
        { AcmpRoles.Member, AcmpRoles.Reviewer, AcmpRoles.Submitter };

    private readonly IUserStreamProvider _streams;

    public StreamScopeHandler(IUserStreamProvider streams) => _streams = streams;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, StreamScopeRequirement requirement, IStreamScopedResource resource)
    {
        if (!StreamBounded.Any(context.User.IsInRole))
        {
            context.Succeed(requirement);
            return;
        }

        // ADR-0043 clause (5): a topic that affects everything does not belong to one stream's
        // members. DECLARED by the resource, never inferred from an empty stream list (DEF-059).
        if (resource.AffectsAllStreams)
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? context.User.FindFirst("sub")?.Value;
        if (userId is null)
            return;

        // ⚠ STILL MISSING, AND STEP 7 CANNOT SHIP WITHOUT IT (DW-026): the WILDCARD is not read here.
        // ADR-0043 clause (3) expresses "unrestricted" as a member holding the stream whose
        // Stream.IsWildcard column is set, and the ADR-0043 step-5 backfill has just assigned exactly
        // that to every member who held nothing. This method returns stream CODES, so a wildcard
        // holder's "all-streams" simply fails to intersect and they would be REFUSED — the opposite
        // of what the backfill was for. Honouring it needs a new signal on IUserStreamProvider
        // (never a code comparison — clause (3) forbids matching the magic string), which lands with
        // the wiring in step 7 where it is testable end to end.
        var assigned = await _streams.GetAssignedStreamsAsync(userId);
        if (resource.AffectedStreams.Intersect(assigned, StringComparer.OrdinalIgnoreCase).Any())
            context.Succeed(requirement);
    }
}
