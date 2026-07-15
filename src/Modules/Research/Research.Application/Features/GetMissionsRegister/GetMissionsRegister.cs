using Acmp.Modules.Research.Application.Abstractions;
using Acmp.Modules.Research.Application.Contracts;
using Acmp.Modules.Research.Application.Internal;
using Acmp.Modules.Research.Domain;
using Acmp.Modules.Research.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Research.Application.Features.GetMissionsRegister;

// The research-mission register as a filtered/sorted/paged view, newest first by default. Readable by any
// authenticated committee member (read-all). Status filter runs in SQL; the bilingual text search runs in
// memory after materialising — right-sized for one low-traffic committee (mirrors the ADR register).
public sealed record GetMissionsRegisterQuery(
    IReadOnlyList<ResearchMissionStatus>? Statuses = null,
    string? Search = null,
    string SortBy = "created",
    string SortDir = "desc",
    int Page = 1,
    int PageSize = 25)
    : IRequest<PagedResult<ResearchMissionSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetMissionsRegisterHandler : IRequestHandler<GetMissionsRegisterQuery, PagedResult<ResearchMissionSummaryDto>>
{
    private readonly IResearchDbContext _db;

    public GetMissionsRegisterHandler(IResearchDbContext db) => _db = db;

    public async Task<PagedResult<ResearchMissionSummaryDto>> Handle(GetMissionsRegisterQuery request, CancellationToken ct)
    {
        var query = _db.Missions.AsNoTracking()
            .Include(m => m.Findings)
            .Include(m => m.Recommendations)
            .AsQueryable();

        if (request.Statuses is { Count: > 0 })
            query = query.Where(m => request.Statuses.Contains(m.Status));

        var missions = await query.ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            missions = missions.Where(m =>
                m.Title.En.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                m.Title.Ar.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                m.Key.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var sorted = Sort(missions, request.SortBy, request.SortDir);
        var total = sorted.Count;
        var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
        var page = request.Page <= 0 ? 1 : request.Page;

        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ResearchMapping.ToSummary)
            .ToList();

        return new PagedResult<ResearchMissionSummaryDto>(items, total, page, pageSize);
    }

    private static List<ResearchMission> Sort(List<ResearchMission> missions, string by, string dir)
    {
        var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        IEnumerable<ResearchMission> ordered = by.ToLowerInvariant() switch
        {
            "status" => missions.OrderBy(m => m.Status),
            "key" => missions.OrderBy(m => m.Key, StringComparer.Ordinal),
            "title" => missions.OrderBy(m => m.Title.En, StringComparer.OrdinalIgnoreCase),
            // "Updated" sorts by last-modified, falling back to creation for never-edited missions.
            "updated" => missions.OrderBy(m => m.UpdatedAt ?? m.CreatedAt),
            _ => missions.OrderBy(m => m.CreatedAt),
        };
        return (desc ? ordered.Reverse() : ordered).ToList();
    }
}
