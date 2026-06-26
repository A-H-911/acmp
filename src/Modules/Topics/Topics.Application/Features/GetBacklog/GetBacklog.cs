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

// The Backlog as a filtered/sorted/paged view over Topics (W3). Readable by any authenticated user
// (committee-wide read, README §C). Stream/text filters and sort run in memory after the DB-translatable
// predicates — right-sized for a single low-traffic committee (≤ a few hundred topics).
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
    int PageSize = 25)
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

    public GetBacklogHandler(ITopicsDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PagedResult<TopicSummaryDto>> Handle(GetBacklogQuery request, CancellationToken ct)
    {
        // History drives "time in current status" for the SLA aging badge (AC-057).
        var query = _db.Topics.AsNoTracking().Include(t => t.History).AsQueryable();

        if (request.Statuses is { Count: > 0 })
            query = query.Where(t => request.Statuses.Contains(t.Status));
        else if (!request.IncludeClosed)
            query = query.Where(t => !Terminal.Contains(t.Status));

        if (request.Type is { } type) query = query.Where(t => t.Type == type);
        if (request.Urgency is { } urg) query = query.Where(t => t.Urgency == urg);
        if (request.OwnerId is { } owner) query = query.Where(t => t.OwnerId == owner);

        var topics = await query.ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.Stream))
            topics = topics.Where(t => t.AffectedStreams.Contains(request.Stream, StringComparer.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            topics = topics.Where(t =>
                t.Title.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                t.Key.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var now = _clock.UtcNow;
        var sorted = Sort(topics, request.SortBy, request.SortDir, now);
        var total = sorted.Count;
        var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
        var page = request.Page <= 0 ? 1 : request.Page;

        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => Map(t, now))
            .ToList();

        return new PagedResult<TopicSummaryDto>(items, total, page, pageSize);
    }

    private static List<Topic> Sort(List<Topic> topics, string by, string dir, DateTimeOffset now)
    {
        var desc = !string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
        IEnumerable<Topic> ordered = by.ToLowerInvariant() switch
        {
            "priority" => topics.OrderBy(t => t.Priority),
            "title" => topics.OrderBy(t => t.Title),
            "status" => topics.OrderBy(t => t.Status),
            "urgency" => topics.OrderBy(t => t.Urgency),
            _ => topics.OrderBy(t => t.CreatedAt), // "age": oldest first ascending
        };
        return (desc ? ordered.Reverse() : ordered).ToList();
    }

    private static TopicSummaryDto Map(Topic t, DateTimeOffset now) => new(
        t.PublicId, t.Key, t.Title, t.Type.ToString(), t.Status.ToString(), t.Urgency.ToString(),
        t.Scope.ToString(), t.AffectedStreams.ToList(), t.OwnerId, t.OwnerName, t.Priority,
        TopicAging.AgeDays(t.CreatedAt, now), TopicAging.IsBreaching(t, now), t.CreatedAt);
}
