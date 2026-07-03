using Acmp.Modules.Dependencies.Application.Abstractions;
using Acmp.Modules.Dependencies.Application.Contracts;
using Acmp.Modules.Dependencies.Application.Internal;
using Acmp.Modules.Dependencies.Domain;
using Acmp.Modules.Dependencies.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Dependencies.Application.Features.GetDependenciesRegister;

// The dependency register as a filtered/sorted/paged view. Readable by any authenticated committee member
// (read-all). Removed edges are excluded by default (they are the soft-deleted state) unless the Status
// filter explicitly asks for Removed. All predicates + the sort are DB-translatable (no derived-in-memory
// column), right-sized for one low-traffic committee.
public sealed record GetDependenciesRegisterQuery(
    DependencyKind? Kind = null,
    DependencyStatus? Status = null,
    bool BlockedOnly = false,
    string SortBy = "key",
    string SortDir = "asc",
    int Page = 1,
    int PageSize = 25)
    : IRequest<PagedResult<DependencySummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetDependenciesRegisterHandler
    : IRequestHandler<GetDependenciesRegisterQuery, PagedResult<DependencySummaryDto>>
{
    private readonly IDependenciesDbContext _db;

    public GetDependenciesRegisterHandler(IDependenciesDbContext db) => _db = db;

    public async Task<PagedResult<DependencySummaryDto>> Handle(GetDependenciesRegisterQuery request, CancellationToken ct)
    {
        var query = _db.Dependencies.AsNoTracking().AsQueryable();

        // Removed is excluded by default; an explicit Status filter (incl. Removed) overrides that.
        query = request.Status is { } status
            ? query.Where(d => d.Status == status)
            : query.Where(d => d.Status != DependencyStatus.Removed);

        if (request.Kind is { } kind)
            query = query.Where(d => d.Kind == kind);

        if (request.BlockedOnly)
            query = query.Where(d =>
                (d.Kind == DependencyKind.BlockedBy || d.Kind == DependencyKind.Blocks)
                && d.Status == DependencyStatus.Open);

        var ordered = Sort(query, request.SortBy, request.SortDir);

        var total = await ordered.CountAsync(ct);
        var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
        var page = request.Page <= 0 ? 1 : request.Page;

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(DependencyMapping.ToSummary).ToList();
        return new PagedResult<DependencySummaryDto>(items, total, page, pageSize);
    }

    private static IQueryable<Dependency> Sort(IQueryable<Dependency> query, string by, string dir)
    {
        var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        return by.ToLowerInvariant() switch
        {
            "status" => desc ? query.OrderByDescending(d => d.Status) : query.OrderBy(d => d.Status),
            _ => desc ? query.OrderByDescending(d => d.Key) : query.OrderBy(d => d.Key),
        };
    }
}
