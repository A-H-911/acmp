using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Modules.Governance.Domain;
using Acmp.Modules.Governance.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.GetAdrsRegister;

// The ADR register as a filtered/sorted/paged view (the design's Lists & Registers adrs tab), newest first
// by default. Readable by any authenticated committee member (read-all, README §C). Status filter runs in
// SQL; the bilingual text search runs in memory after materialising — right-sized for one low-traffic
// committee. Full-text search (FR-102) is deferred to the Search phase; this is the substring filter.
public sealed record GetAdrsRegisterQuery(
    IReadOnlyList<AdrStatus>? Statuses = null,
    string? Search = null,
    string SortBy = "created",
    string SortDir = "desc",
    int Page = 1,
    int PageSize = 25)
    : IRequest<PagedResult<AdrSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetAdrsRegisterHandler : IRequestHandler<GetAdrsRegisterQuery, PagedResult<AdrSummaryDto>>
{
    private readonly IGovernanceDbContext _db;

    public GetAdrsRegisterHandler(IGovernanceDbContext db) => _db = db;

    public async Task<PagedResult<AdrSummaryDto>> Handle(GetAdrsRegisterQuery request, CancellationToken ct)
    {
        var query = _db.Adrs.AsNoTracking().AsQueryable();

        if (request.Statuses is { Count: > 0 })
            query = query.Where(a => request.Statuses.Contains(a.Status));

        var adrs = await query.ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            adrs = adrs.Where(a =>
                a.Title.En.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Title.Ar.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                a.Key.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var sorted = Sort(adrs, request.SortBy, request.SortDir);
        var total = sorted.Count;
        var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
        var page = request.Page <= 0 ? 1 : request.Page;

        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(AdrMapping.ToSummary)
            .ToList();

        return new PagedResult<AdrSummaryDto>(items, total, page, pageSize);
    }

    // Default sort = created (newest first) so the latest ADRs lead the register.
    private static List<Adr> Sort(List<Adr> adrs, string by, string dir)
    {
        var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        IEnumerable<Adr> ordered = by.ToLowerInvariant() switch
        {
            "status" => adrs.OrderBy(a => a.Status),
            "key" => adrs.OrderBy(a => a.Key, StringComparer.Ordinal),
            "title" => adrs.OrderBy(a => a.Title.En, StringComparer.OrdinalIgnoreCase),
            _ => adrs.OrderBy(a => a.CreatedAt),
        };
        return (desc ? ordered.Reverse() : ordered).ToList();
    }
}
