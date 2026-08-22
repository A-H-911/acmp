namespace Acmp.Shared.Application.Pagination;

/// <summary>
/// The upper bound on a caller-supplied page size (DEF-104).
///
/// ⚠ THE GUARD LIVES HERE RATHER THAN IN EACH READ, and that is DEF-059's rule applied again: an
/// invariant asserted at one entry point and absent at the others is the shape this codebase keeps
/// paying for. Eleven register reads guarded only the LOWER bound —
/// <c>pageSize = request.PageSize &lt;= 0 ? 25 : request.PageSize</c> — so an authenticated caller could
/// ask for any page size and the query would attempt to materialize it. Two reads already clamped
/// (GetNotifications, and search via MaxTakePerType), so the codebase knew the answer in two places and
/// had not applied it in eleven. One shared helper means the twelfth read cannot forget.
///
/// WHY 500, and it is not arbitrary. It is the largest page size the application itself ever asks for
/// (the kanban's KANBAN_PAGE_SIZE, the reports and dashboard ALL, and the wiki list), and ADR-0022
/// records that <c>pageSize=500</c> was verified to cover every register at this deployment's size —
/// CON-001 caps the committee at 20 users and no register runs to a second page. So the cap bounds an
/// unbounded materialization WITHOUT narrowing anything the product legitimately does today. A smaller
/// cap would have been a behaviour change dressed as a fix.
///
/// ⚠ Deliberately NOT a rejection. Clamping keeps an over-large request working (it simply returns the
/// capped page) rather than turning a mistyped query string into a 400 for a caller who is allowed to
/// read the data anyway. GetNotifications already chose clamping; this matches it.
/// </summary>
public static class PageSize
{
    /// <summary>Largest page a caller may request. See the type remarks for why this value.</summary>
    public const int Max = 500;

    /// <summary>Page size used when the caller supplies none or a non-positive one.</summary>
    public const int Default = 25;

    /// <summary>
    /// Clamp a caller-supplied page size into [1, <see cref="Max"/>], substituting
    /// <paramref name="fallback"/> when the caller supplied nothing meaningful.
    /// </summary>
    public static int Clamp(int requested, int fallback = Default) =>
        requested <= 0 ? Math.Clamp(fallback, 1, Max) : Math.Min(requested, Max);

    /// <summary>Clamp a nullable caller-supplied limit; null or non-positive yields <paramref name="fallback"/>.</summary>
    public static int Clamp(int? requested, int fallback = Default) =>
        Clamp(requested ?? 0, fallback);
}
