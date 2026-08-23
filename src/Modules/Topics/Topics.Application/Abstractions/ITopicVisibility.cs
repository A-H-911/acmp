using Acmp.Modules.Topics.Domain;

namespace Acmp.Modules.Topics.Application.Abstractions;

/// <summary>
/// One principal's confidentiality reach, resolved ONCE per request (FR-163 / C-AUTHZ-04, DEC-063 d1).
/// </summary>
/// <param name="SeesAllRestricted">
/// The principal holds a committee-wide reader role (Chairman, Secretary, Auditor).
/// </param>
/// <param name="GrantedTopicIds">
/// Every topic the principal holds an active capability grant on — Owner, Assignee or Presenter.
/// A <c>Guid[]</c> rather than an interface so EF translates <c>Contains</c> into an IN clause.
/// </param>
public sealed record TopicVisibilityScope(bool SeesAllRestricted, Guid[] GrantedTopicIds)
{
    /// <summary>In-memory counterpart of the query predicate, for single-topic decisions.</summary>
    /// <remarks>
    /// ⚠ MUST STAY EQUIVALENT TO <see cref="TopicVisibilityQuery.VisibleTo"/>. The two exist because
    /// one runs in SQL over a page and the other decides one already-loaded topic; if they ever
    /// disagree, a topic is filtered out of the list but readable by key, or the reverse. That is why
    /// both are asserted against the same table of cases rather than each against its own.
    /// </remarks>
    public bool CanSee(Topic topic) =>
        !topic.IsRestricted || SeesAllRestricted || GrantedTopicIds.Contains(topic.PublicId);
}

/// <summary>
/// Resolves the caller's <see cref="TopicVisibilityScope"/>.
/// </summary>
/// <remarks>
/// ⚠ RESOLVE ONCE PER REQUEST, NEVER PER ROW. The grant lookup is a database round-trip with no
/// caching, so calling this inside a loop over a backlog page is an N+1 over every page the committee
/// ever loads. Handlers resolve it before composing their query and pass the scope down.
/// </remarks>
public interface ITopicVisibility
{
    Task<TopicVisibilityScope> ResolveAsync(CancellationToken ct = default);
}
