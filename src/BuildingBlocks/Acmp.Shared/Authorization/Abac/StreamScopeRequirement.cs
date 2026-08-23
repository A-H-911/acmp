using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Acmp.Shared.Authorization.Abac;

// docs/domain/permission-role-matrix.md §E.1 stream scope for WRITE actions. Read is committee-wide by settled decision
// (README §C, OQ-AUTH-001 = read-visible/write-scoped) so this constrains mutation only.
/// <summary>
/// A principal's stream assignment: the stream codes they hold, and whether any of them is the
/// wildcard that makes them unrestricted (ADR-0043 clause 3).
/// </summary>
/// <remarks>
/// ⚠ ONE VALUE RATHER THAN A SECOND PORT METHOD, deliberately. The wildcard flag lives in the same
/// row as the code — <c>UserStreamProvider</c> already joins member_streams to streams — so asking
/// for it separately would be a second database round-trip per authorization for a column the first
/// query had in hand, and a second thing every future implementer must remember to honour.
/// <para>
/// ⚠ <c>IsUnrestricted</c> IS A FLAG FROM A COLUMN, NEVER A CODE COMPARISON. ADR-0043 clause (3)
/// forbids matching on the code: a magic string in an authorization control breaks silently when the
/// stream is renamed or re-coded, and would hand universal access to any future stream that collided
/// with it. The database enforces at most one wildcard row through a filtered unique index.
/// </para>
/// </remarks>
public sealed record AssignedStreams(IReadOnlyCollection<string> Codes, bool IsUnrestricted);

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

        var assigned = await _streams.GetAssignedStreamsAsync(userId);

        // ADR-0043 clause (3): "unrestricted" is a member holding the wildcard STREAM, read from its
        // IsWildcard column rather than matched by code (DW-026). The step-5 backfill assigned exactly
        // that to every member who held nothing, so without this line the backfill would have the
        // opposite of its intended effect — a wildcard holder's "all-streams" simply fails to
        // intersect any topic, and the people the backfill was meant to protect would be refused.
        if (assigned.IsUnrestricted)
        {
            context.Succeed(requirement);
            return;
        }

        if (resource.AffectedStreams.Intersect(assigned.Codes, StringComparer.OrdinalIgnoreCase).Any())
            context.Succeed(requirement);
    }
}
