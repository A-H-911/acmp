using Acmp.Modules.Actions.Application.Abstractions;
using Acmp.Modules.Actions.Application.Contracts;
using Acmp.Modules.Actions.Application.Internal;
using Acmp.Modules.Actions.Domain;
using Acmp.Modules.Actions.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Actions.Application.Features.GetActionsRegister;

// The Actions register as a filtered/sorted/paged view (the design's isActions table). Readable by any
// authenticated committee member (read-all, README §C). DB-translatable predicates (status, owner) run in
// SQL; the derived "overdue" filter, text search, and sort run in memory after materialising — right-sized
// for a single low-traffic committee (docs/domain/entity-lifecycles.md: overdue is a computed overlay, not a stored column).
public sealed record GetActionsRegisterQuery(
    IReadOnlyList<ActionStatus>? Statuses = null,
    string? OwnerUserId = null,
    bool OverdueOnly = false,
    string? Search = null,
    string SortBy = "due",
    string SortDir = "asc",
    int Page = 1,
    int PageSize = 25)
    : IRequest<PagedResult<ActionSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetActionsRegisterHandler : IRequestHandler<GetActionsRegisterQuery, PagedResult<ActionSummaryDto>>
{
    private readonly IActionsDbContext _db;
    private readonly IClock _clock;

    public GetActionsRegisterHandler(IActionsDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PagedResult<ActionSummaryDto>> Handle(GetActionsRegisterQuery request, CancellationToken ct)
    {
        var query = _db.Actions.AsNoTracking().AsQueryable();

        if (request.Statuses is { Count: > 0 })
            query = query.Where(a => request.Statuses.Contains(a.Status));
        if (!string.IsNullOrWhiteSpace(request.OwnerUserId))
            query = query.Where(a => a.OwnerUserId == request.OwnerUserId);

        var actions = await query.ToListAsync(ct);
        var now = _clock.UtcNow;

        if (request.OverdueOnly)
            actions = actions.Where(a => a.IsOverdue(now)).ToList();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            actions = actions.Where(a =>
                a.Title.En.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Title.Ar.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Key.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var sorted = Sort(actions, request.SortBy, request.SortDir);
        var total = sorted.Count;
        var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
        var page = request.Page <= 0 ? 1 : request.Page;

        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => ActionMapping.ToSummary(a, now))
            .ToList();

        return new PagedResult<ActionSummaryDto>(items, total, page, pageSize);
    }

    // Due-date sort puts undated actions last regardless of direction (they have no deadline to rank).
    private static List<ActionItem> Sort(List<ActionItem> actions, string by, string dir)
    {
        var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        IEnumerable<ActionItem> ordered = by.ToLowerInvariant() switch
        {
            "status" => actions.OrderBy(a => a.Status),
            "priority" => actions.OrderBy(a => a.Priority),
            "progress" => actions.OrderBy(a => a.ProgressPct),
            "created" => actions.OrderBy(a => a.CreatedAt),
            _ => actions.OrderBy(a => a.DueDate.HasValue ? 0 : 1)          // dated first
                        .ThenBy(a => a.DueDate ?? DateTimeOffset.MaxValue), // then by due date
        };
        return (desc ? ordered.Reverse() : ordered).ToList();
    }
}
