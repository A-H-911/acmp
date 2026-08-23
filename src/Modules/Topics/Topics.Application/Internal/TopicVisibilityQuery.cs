using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Authorization.Abac;

namespace Acmp.Modules.Topics.Application.Internal;

// C-AUTHZ-04 / FR-163 — the READ half of the confidentiality control.
//
// ⚠ WHY A QUERY PREDICATE AND NOT AN AUTHORIZATION HANDLER. An IAuthorizationRequirement is only ever
// evaluated where IAuthorizationService is called, and no topic read path calls it — reads are a bare
// RequireAuthorization() group plus an empty AllowedRoles. GuestSurfaceMiddleware's own header records
// that gap as the reason it had to exist. ConfidentialityRequirement covers the resource-authorized
// write paths; this covers everything that returns topics. Two mechanisms, one control: change one and
// you must change the other.
public static class TopicVisibilityQuery
{
    /// <summary>
    /// Narrows a topic query to what the principal may see. Composes BEFORE paging or Take.
    /// </summary>
    /// <remarks>
    /// ⚠ MUST BE APPLIED BEFORE <c>Take</c>/<c>Skip</c>. Filtering after paging silently shrinks pages
    /// and misreports totals — the page would claim 25 results and show fewer, which reads as a bug in
    /// the list rather than as a control doing its job.
    /// <para>
    /// The predicate is SQL-translatable on purpose: <c>IsRestricted</c> is a plain bit column (unlike
    /// streams/systems/tags, which are JSON and must be filtered in memory), and the grant set is a
    /// materialised array so <c>Contains</c> becomes an IN clause.
    /// </para>
    /// </remarks>
    public static IQueryable<Topic> VisibleTo(this IQueryable<Topic> query, TopicVisibilityScope scope) =>
        scope.SeesAllRestricted
            ? query
            : query.Where(t => !t.IsRestricted || scope.GrantedTopicIds.Contains(t.PublicId));
}

// Resolves the caller's reach once per request. Scoped, so the grant round-trip happens at most once
// even when a handler filters several queries.
public sealed class TopicVisibility : ITopicVisibility
{
    // ⚠ THE SAME POSITIVE LIST AS ConfidentialityHandler, AND IT MUST STAY THE SAME (DEC-063 d1).
    // If the two ever diverge, a role sees a Restricted topic in the backlog and is refused on the
    // detail, or the reverse — the shape that makes a control look broken rather than strict.
    private static readonly string[] CommitteeWideReaders =
        { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Auditor };

    private readonly ICurrentUser _user;
    private readonly ITopicCapabilityResolver _capabilities;

    public TopicVisibility(ICurrentUser user, ITopicCapabilityResolver capabilities)
    {
        _user = user;
        _capabilities = capabilities;
    }

    public async Task<TopicVisibilityScope> ResolveAsync(CancellationToken ct = default)
    {
        if (CommitteeWideReaders.Any(_user.IsInRole))
            return new TopicVisibilityScope(true, Array.Empty<Guid>());

        // No subject means no grants can be resolved, so the caller sees unrestricted topics only.
        // Fail closed: a control whose whole job is to withhold must not open up on a missing claim.
        var userId = _user.UserId;
        if (string.IsNullOrEmpty(userId))
            return new TopicVisibilityScope(false, Array.Empty<Guid>());

        var granted = await _capabilities.GetGrantedTopicIdsAsync(userId, ct);
        return new TopicVisibilityScope(false, granted.ToArray());
    }
}
