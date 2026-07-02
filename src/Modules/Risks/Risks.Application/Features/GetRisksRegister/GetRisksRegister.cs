using Acmp.Modules.Risks.Application.Abstractions;
using Acmp.Modules.Risks.Application.Contracts;
using Acmp.Modules.Risks.Application.Internal;
using Acmp.Modules.Risks.Domain;
using Acmp.Modules.Risks.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Risks.Application.Features.GetRisksRegister;

// The Risk register as a filtered/sorted/paged view (the design's isRisks table, sorted by exposure by
// default = the "Open risks by exposure" saved view). Readable by any authenticated committee member
// (read-all, README §C). DB-translatable predicates (status, owner) run in SQL; the derived exposure filter,
// text search, and exposure/severity sort run in memory after materialising — right-sized for one low-traffic
// committee (docs/12: severity/exposure is a computed overlay, not a stored column).
public sealed record GetRisksRegisterQuery(
    IReadOnlyList<RiskStatus>? Statuses = null,
    string? OwnerUserId = null,
    IReadOnlyList<RiskExposure>? Exposures = null,
    string? Search = null,
    string SortBy = "exposure",
    string SortDir = "desc",
    int Page = 1,
    int PageSize = 25)
    : IRequest<PagedResult<RiskSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetRisksRegisterHandler : IRequestHandler<GetRisksRegisterQuery, PagedResult<RiskSummaryDto>>
{
    private readonly IRisksDbContext _db;

    public GetRisksRegisterHandler(IRisksDbContext db) => _db = db;

    public async Task<PagedResult<RiskSummaryDto>> Handle(GetRisksRegisterQuery request, CancellationToken ct)
    {
        var query = _db.Risks.AsNoTracking().AsQueryable();

        if (request.Statuses is { Count: > 0 })
            query = query.Where(r => request.Statuses.Contains(r.Status));
        if (!string.IsNullOrWhiteSpace(request.OwnerUserId))
            query = query.Where(r => r.OwnerUserId == request.OwnerUserId);

        var risks = await query.ToListAsync(ct);

        if (request.Exposures is { Count: > 0 })
            risks = risks.Where(r => request.Exposures.Contains(r.Exposure())).ToList();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            risks = risks.Where(r =>
                r.Title.En.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                r.Title.Ar.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                r.Key.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var sorted = Sort(risks, request.SortBy, request.SortDir);
        var total = sorted.Count;
        var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
        var page = request.Page <= 0 ? 1 : request.Page;

        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(RiskMapping.ToSummary)
            .ToList();

        return new PagedResult<RiskSummaryDto>(items, total, page, pageSize);
    }

    // Default sort = exposure (severity score) so the highest-exposure risks lead the register.
    private static List<Risk> Sort(List<Risk> risks, string by, string dir)
    {
        var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        IEnumerable<Risk> ordered = by.ToLowerInvariant() switch
        {
            "status" => risks.OrderBy(r => r.Status),
            "created" => risks.OrderBy(r => r.CreatedAt),
            "key" => risks.OrderBy(r => r.Key, StringComparer.Ordinal),
            _ => risks.OrderBy(r => r.Severity()),
        };
        return (desc ? ordered.Reverse() : ordered).ToList();
    }
}
