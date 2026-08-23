namespace Acmp.Shared.Contracts.Topics;

// Cross-module read seam (ADR-0001, SL-030 / FR-163 / C-AUTHZ-04): a module that froze a topic's key+title
// snapshot into its own schema asks Topics which of those topics the CURRENT caller may not see, so it can
// redact at PROJECTION time — without reading Topics' tables and without re-implementing the rule. Implemented
// in Topics.Infrastructure over the Topics store (mirrors ITopicStreamReader). Speaks primitives only; the
// Topics enums, the role list and the grant model never leak.
//
// ⚠ WHY THE ANSWER IS "ALL OF THEM" AND NOT "WHICH OF THESE ids". The first consumer, the dependency
// register, PAGES and reports a total: it must narrow the query BEFORE CountAsync/Skip/Take, which is
// impossible if the answer has to be asked about ids that only exist once the page has loaded. Filtering
// after paging is the failure TopicVisibilityQuery.VisibleTo's own remarks warn about — the page claims 25
// results and shows fewer. A whole-set answer composes into the WHERE clause instead.
//
// ponytail: one query, no cache, whole set. The ceiling is the number of RESTRICTED topics — a deliberately
// narrow carve-out on a ≤20-user single-committee deployment (C4), not the topic table. If restriction ever
// becomes the common case, this becomes a per-request cached HashSet before it becomes anything cleverer.
public interface ITopicConfidentiality
{
    /// <summary>
    /// Every topic id the current caller must NOT see, for redacting snapshots copied out of Topics.
    /// Empty for a committee-wide reader (Chairman, Secretary, Auditor) — see DEC-063 d1.
    /// </summary>
    /// <remarks>
    /// ⚠ THE ANSWER IS ABOUT THE CALLER, NOT ABOUT THE DATA. It is only meaningful inside the request that
    /// asked for it: never cache it across requests, and never store it.
    /// <para>
    /// A returned <c>Guid[]</c> rather than a set so consumers can compose <c>Contains</c> straight into a
    /// SQL-translatable predicate, the same materialisation TopicVisibilityScope.GrantedTopicIds uses.
    /// </para>
    /// </remarks>
    Task<Guid[]> GetHiddenTopicIdsAsync(CancellationToken ct = default);
}
