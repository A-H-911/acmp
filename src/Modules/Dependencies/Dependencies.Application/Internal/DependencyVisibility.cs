using Acmp.Modules.Dependencies.Domain;
using Acmp.Modules.Dependencies.Domain.Enums;

namespace Acmp.Modules.Dependencies.Application.Internal;

// FR-163 / C-AUTHZ-04 / AC-114 — the confidentiality egress filter for dependency edges.
//
// Dependency froze both endpoints' key+title at create time (Dependency.cs:22-28, ADR-0019), so an edge with
// a Topic endpoint carries that topic's TITLE. All three dependency reads are read-all (AllowedRoles is
// empty on the register, the per-artifact panel and the by-key detail), and the register is what the Reports
// surface AC-114 names actually loads — so without this a Restricted topic's title reaches every member
// through a module that never mentions topics.
//
// ⚠ AC-114 DOES NOT ENUMERATE THIS SURFACE — it names agenda items, agendas and minutes, traceability
// relationships and notification bodies. It is covered by the criterion's leading clause ("data already
// copied out of the topic ... is redacted at PROJECTION time") and by its own reports clause. DEF-090
// records the omission so the enumeration is not mistaken for the whole list next time.
internal static class DependencyVisibility
{
    /// <summary>
    /// Drops every edge with a Restricted-and-invisible Topic at EITHER end. Composes BEFORE paging.
    /// </summary>
    /// <remarks>
    /// ⚠ MUST BE APPLIED BEFORE <c>Skip</c>/<c>Take</c> AND BEFORE <c>CountAsync</c>. The register pages and
    /// reports a total; filtering after either one gives a page that claims 25 rows and shows fewer, which
    /// reads as a bug in the register rather than as a control doing its job. This is why the confidentiality
    /// port answers with the WHOLE hidden set rather than about a page's ids — the page does not exist yet.
    /// <para>
    /// ⚠ BOTH ENDPOINTS, NOT JUST THE FAR ONE. Filtering both is what makes asking for a hidden topic's own
    /// dependency panel answer exactly as a nonexistent id does, with no separate focus guard anywhere.
    /// </para>
    /// <para>
    /// The predicate is SQL-translatable: the endpoint type is a stored enum column and the hidden set is a
    /// materialised <c>Guid[]</c>, so <c>Contains</c> becomes an IN clause.
    /// </para>
    /// </remarks>
    public static IQueryable<Dependency> WithoutHiddenTopics(
        this IQueryable<Dependency> query, Guid[] hiddenTopicIds) =>
        hiddenTopicIds.Length == 0
            ? query
            : query.Where(d =>
                !(d.FromType == DependencyEndpointType.Topic && hiddenTopicIds.Contains(d.FromId)) &&
                !(d.ToType == DependencyEndpointType.Topic && hiddenTopicIds.Contains(d.ToId)));
}
