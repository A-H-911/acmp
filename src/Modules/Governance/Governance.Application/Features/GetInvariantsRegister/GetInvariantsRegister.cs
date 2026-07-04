using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Modules.Governance.Domain;
using Acmp.Modules.Governance.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.GetInvariantsRegister;

// The Invariant register as a filtered/sorted/paged view (the design's Lists & Registers invariants tab),
// newest first by default. Readable by any authenticated committee member (read-all, README §C). Status filter
// runs in SQL; the bilingual text search runs in memory after materialising — right-sized for one low-traffic
// committee. Full-text search is deferred to the Search phase; this is the substring filter.
public sealed record GetInvariantsRegisterQuery(
    IReadOnlyList<InvariantStatus>? Statuses = null,
    string? Search = null,
    string SortBy = "created",
    string SortDir = "desc",
    int Page = 1,
    int PageSize = 25)
    : IRequest<PagedResult<InvariantSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetInvariantsRegisterHandler : IRequestHandler<GetInvariantsRegisterQuery, PagedResult<InvariantSummaryDto>>
{
    private readonly IGovernanceDbContext _db;

    public GetInvariantsRegisterHandler(IGovernanceDbContext db) => _db = db;

    public async Task<PagedResult<InvariantSummaryDto>> Handle(GetInvariantsRegisterQuery request, CancellationToken ct)
    {
        var query = _db.Invariants.AsNoTracking().AsQueryable();

        if (request.Statuses is { Count: > 0 })
            query = query.Where(a => request.Statuses.Contains(a.Status));

        var invariants = await query.ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            invariants = invariants.Where(a =>
                a.Statement.En.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Statement.Ar.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Key.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var sorted = Sort(invariants, request.SortBy, request.SortDir);
        var total = sorted.Count;
        var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
        var page = request.Page <= 0 ? 1 : request.Page;

        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(InvariantMapping.ToSummary)
            .ToList();

        return new PagedResult<InvariantSummaryDto>(items, total, page, pageSize);
    }

    // Default sort = created (newest first) so the latest invariants lead the register.
    private static List<Invariant> Sort(List<Invariant> invariants, string by, string dir)
    {
        var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        IEnumerable<Invariant> ordered = by.ToLowerInvariant() switch
        {
            "status" => invariants.OrderBy(a => a.Status),
            "key" => invariants.OrderBy(a => a.Key, StringComparer.Ordinal),
            "category" => invariants.OrderBy(a => a.Category),
            "statement" => invariants.OrderBy(a => a.Statement.En, StringComparer.OrdinalIgnoreCase),
            _ => invariants.OrderBy(a => a.CreatedAt),
        };
        return (desc ? ordered.Reverse() : ordered).ToList();
    }
}
