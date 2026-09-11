using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Contracts;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.GetBacklog;

/*
 * The Backlog as a filtered/sorted/paged view over Topics (W3). Readable by any authenticated user
 * (committee-wide read, README §C).
 *
 * ⚠⚠ THIS COMMENT USED TO SAY "Stream/text filters and sort run in memory after the DB-translatable
 * predicates — right-sized for a single low-traffic committee (≤ a few hundred topics)". THAT
 * RIGHT-SIZING WAS DELIBERATE AND ITS STATED BOUND WAS EXCEEDED BY THE REQUIREMENTS THEMSELVES:
 * NFR-009 specifies 2 500 topics over a five-year life and NFR-002 bounds this endpoint at 10 000 —
 * eight to forty times "a few hundred". Measured at 10 000 the endpoint returned P95 1 362.7 ms
 * against a 1 000 ms budget (DEF-155), because it built 10 000 entity graphs to return 25 of them.
 *
 * ⭐ NOW: the search, the sort, the count and the page all run in SQL, and History is Included only
 * for the rows actually returned. P95 57.4 ms at 10 000. ONLY the stream filter still runs in
 * memory — AffectedStreams is projected from a JSON column and matched OrdinalIgnoreCase, so no
 * provider can push it down, and that path deliberately keeps the original shape.
 *
 * ⚠ The right-sizing was not wrong to make; it was wrong to leave unrevisited once the requirements
 * named a scale an order of magnitude past its stated bound. The bound was written down, which is
 * why this was findable at all.
 */
public sealed record GetBacklogQuery(
    IReadOnlyList<TopicStatus>? Statuses = null,
    TopicType? Type = null,
    string? Stream = null,
    TopicUrgency? Urgency = null,
    Guid? OwnerId = null,
    string? Search = null,
    bool IncludeClosed = false,
    string SortBy = "age",
    string SortDir = "desc",
    int Page = 1,
    int PageSize = 25,
    // WBS-40.2 / DEC-171 u3: the submitter channel as a backlog facet. Appended LAST with a default so no
    // existing positional caller changes meaning; a persisted enum column, so it filters in SQL.
    TopicSource? Source = null)
    : IRequest<PagedResult<TopicSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetBacklogHandler : IRequestHandler<GetBacklogQuery, PagedResult<TopicSummaryDto>>
{
    private static readonly TopicStatus[] Terminal =
        { TopicStatus.Closed, TopicStatus.Converted, TopicStatus.Rejected };

    private readonly ITopicsDbContext _db;
    private readonly IClock _clock;
    private readonly ITopicVisibility _visibility;

    public GetBacklogHandler(ITopicsDbContext db, IClock clock, ITopicVisibility visibility)
    {
        _db = db;
        _clock = clock;
        _visibility = visibility;
    }

    public async Task<PagedResult<TopicSummaryDto>> Handle(GetBacklogQuery request, CancellationToken ct)
    {
        var query = _db.Topics.AsNoTracking().AsQueryable();

        // C-AUTHZ-04 / FR-163. THIS ONE LINE COVERS FIVE PRODUCT SURFACES: the backlog list, the
        // kanban, the agenda pool (GET /topics?status=Prepared), the role dashboards and the reports
        // all read through this handler. Applied FIRST, before every filter and long before paging, so
        // a Restricted topic can never be counted into a total the caller may not see.
        query = query.VisibleTo(await _visibility.ResolveAsync(ct));

        if (request.Statuses is { Count: > 0 })
            query = query.Where(t => request.Statuses.Contains(t.Status));
        else if (!request.IncludeClosed)
            query = query.Where(t => !Terminal.Contains(t.Status));

        if (request.Type is { } type) query = query.Where(t => t.Type == type);
        if (request.Urgency is { } urg) query = query.Where(t => t.Urgency == urg);
        if (request.Source is { } src) query = query.Where(t => t.Source == src);
        if (request.OwnerId is { } owner) query = query.Where(t => t.OwnerId == owner);

        var now = _clock.UtcNow;
        var pageSize = PageSize.Clamp(request.PageSize);   // DEF-104: cap the caller-supplied page
        var page = request.Page <= 0 ? 1 : request.Page;

        /*
         * DEF-155. Search translates to SQL; the earlier in-memory pass did not, and it was one of the
         * reasons every row had to be materialised.
         *
         * ⛔⛔ `ToLower()` ON BOTH SIDES IS LOAD-BEARING — A BARE `Contains(s)` MEANS TWO DIFFERENT
         * THINGS ON THE TWO PROVIDERS THIS CODE RUNS ON. On the InMemory provider it is real
         * `string.Contains`: ORDINAL and CASE-SENSITIVE. On SQL Server it becomes `LIKE`, resolved by
         * the database COLLATION, which is case-INsensitive by default. So the same expression narrows
         * the result set in the test suite and widens it in production.
         *
         * `GetBacklogCoverageTests.Search_by_title_is_case_insensitive_and_returns_matching_topics`
         * asserts the case-insensitive behaviour, so it is specified rather than incidental — and that
         * test is what caught this. Lowercasing both operands translates to SQL `LOWER(...)` and
         * evaluates as plain .NET on InMemory, so the two providers agree by construction instead of
         * by a collation nobody sets.
         *
         * ⚠ It also forgoes an index, which costs nothing here: a leading-wildcard `LIKE` could not
         * have used one anyway.
         */
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(s) || t.Key.ToLower().Contains(s));
        }

        /*
         * ⛔ THE ONE FILTER THAT CANNOT TRANSLATE, AND THEREFORE THE ONE PATH THAT STILL MATERIALISES.
         * AffectedStreams is projected from a JSON column and the match is OrdinalIgnoreCase, so no
         * provider can push it down. That path keeps the original shape deliberately — it is correct,
         * it is comparatively rare, and pretending otherwise would trade a latency problem for a
         * wrong-answer one.
         */
        if (!string.IsNullOrWhiteSpace(request.Stream))
            return await PageInMemoryAsync(query, request, now, page, pageSize, ct);

        /*
         * ⭐ DEF-155'S FIX. The count and the page are two SQL statements over the SAME filtered query,
         * so the database returns 25 rows instead of every visible topic. Measured before: P95 1 362.7 ms
         * at 10 000 topics against a 1 000 ms budget, because the handler built 10 000 entity graphs to
         * return 25 of them.
         *
         * ⚠ THE COUNT SITS INSIDE THE VISIBILITY FILTER, WHICH IS THE WHOLE POINT OF THAT `VisibleTo`
         * CALL ABOVE — counting first and filtering later would leak the EXISTENCE of Restricted topics
         * through the total even while their contents stayed hidden (C-AUTHZ-04 / FR-163).
         *
         * ⚠ AND `Include` COMES AFTER `Skip`/`Take` ON PURPOSE: History is needed only by Map, for the
         * rows actually returned. Including it before paging is what made the old query load every
         * topic's whole history to display one page.
         */
        var total = await query.CountAsync(ct);

        var items = await SortQuery(query, request.SortBy, request.SortDir)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(t => t.History)   // AC-057: History drives "time in current status" on the aging badge
            .ToListAsync(ct);

        return new PagedResult<TopicSummaryDto>(
            items.Select(t => Map(t, now)).ToList(), total, page, pageSize);
    }

    // The stream-filtered path: materialise, then filter, sort and page in memory exactly as before.
    private async Task<PagedResult<TopicSummaryDto>> PageInMemoryAsync(
        IQueryable<Topic> query, GetBacklogQuery request, DateTimeOffset now, int page, int pageSize, CancellationToken ct)
    {
        var topics = await query.Include(t => t.History).ToListAsync(ct);

        topics = topics
            .Where(t => t.AffectedStreams.Contains(request.Stream!, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var sorted = Sort(topics, request.SortBy, request.SortDir, now);

        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => Map(t, now))
            .ToList();

        return new PagedResult<TopicSummaryDto>(items, sorted.Count, page, pageSize);
    }

    /*
     * The SQL twin of Sort. ⚠⚠ EVERY BRANCH CARRIES A TOTAL ORDER, AND THE TIEBREAK IS NOT COSMETIC:
     * LINQ-to-Objects `OrderBy` is STABLE, so the in-memory version had a deterministic order among
     * equal titles for free. SQL guarantees NOTHING among ties, and a non-deterministic order under
     * OFFSET/FETCH lets a row appear on two pages or on none. `ThenBy(Key)` is what makes paging
     * sound; `priority` already carried its own total order for AC-043.
     *
     * Descending reverses every key, which is what `Enumerable.Reverse()` did to the whole sequence.
     */
    private static IQueryable<Topic> SortQuery(IQueryable<Topic> q, string by, string dir)
    {
        var desc = !string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);

        return by.ToLowerInvariant() switch
        {
            "priority" => desc
                ? q.OrderByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAt).ThenByDescending(t => t.Key)
                : q.OrderBy(t => t.Priority).ThenBy(t => t.CreatedAt).ThenBy(t => t.Key),
            "title" => desc
                ? q.OrderByDescending(t => t.Title).ThenByDescending(t => t.Key)
                : q.OrderBy(t => t.Title).ThenBy(t => t.Key),
            "status" => desc
                ? q.OrderByDescending(t => t.Status).ThenByDescending(t => t.Key)
                : q.OrderBy(t => t.Status).ThenBy(t => t.Key),
            "urgency" => desc
                ? q.OrderByDescending(t => t.Urgency).ThenByDescending(t => t.Key)
                : q.OrderBy(t => t.Urgency).ThenBy(t => t.Key),
            _ => desc
                ? q.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Key)
                : q.OrderBy(t => t.CreatedAt).ThenBy(t => t.Key),
        };
    }

    private static List<Topic> Sort(List<Topic> topics, string by, string dir, DateTimeOffset now)
    {
        var desc = !string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
        IEnumerable<Topic> ordered = by.ToLowerInvariant() switch
        {
            // AC-043: same deterministic tiebreak MoveTopicPriorityHandler renumbers by, so the displayed order
            // matches persistence even while priorities are non-contiguous (default 0).
            "priority" => topics.OrderBy(t => t.Priority).ThenBy(t => t.CreatedAt).ThenBy(t => t.Key),
            "title" => topics.OrderBy(t => t.Title),
            "status" => topics.OrderBy(t => t.Status),
            "urgency" => topics.OrderBy(t => t.Urgency),
            _ => topics.OrderBy(t => t.CreatedAt), // "age": oldest first ascending
        };
        return (desc ? ordered.Reverse() : ordered).ToList();
    }

    private static TopicSummaryDto Map(Topic t, DateTimeOffset now) => new(
        t.PublicId, t.Key, t.Title, t.Type.ToString(), t.Status.ToString(), t.Urgency.ToString(),
        t.Scope.ToString(), t.AffectedStreams.ToList(), t.OwnerId, t.OwnerName, t.Priority, t.TimesDeferred,
        TopicAging.AgeDays(t.CreatedAt, now), TopicAging.IsBreaching(t, now), t.CreatedAt, t.IsRestricted);
}
