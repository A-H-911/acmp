using Acmp.Modules.Knowledge.Application.Abstractions;
using Acmp.Modules.Knowledge.Application.Contracts;
using Acmp.Modules.Knowledge.Application.Internal;
using Acmp.Modules.Knowledge.Domain;
using Acmp.Modules.Knowledge.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Knowledge.Application.Features.GetTemplatesRegister;

// The template register as a filtered/sorted/paged view, newest first by default. Readable by any authenticated
// committee member (read-all). The TargetType filter is the seam P15h reads to offer "templates for this artifact
// type" at creation time; Status + TargetType filter in SQL, the bilingual name search runs in memory after
// materialising — right-sized for one low-traffic committee (mirrors the document register).
public sealed record GetTemplatesRegisterQuery(
    IReadOnlyList<TemplateStatus>? Statuses = null,
    TemplateTargetType? TargetType = null,
    string? Search = null,
    string SortBy = "created",
    string SortDir = "desc",
    int Page = 1,
    int PageSize = 25)
    : IRequest<PagedResult<TemplateSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetTemplatesRegisterHandler : IRequestHandler<GetTemplatesRegisterQuery, PagedResult<TemplateSummaryDto>>
{
    private readonly IKnowledgeDbContext _db;

    public GetTemplatesRegisterHandler(IKnowledgeDbContext db) => _db = db;

    public async Task<PagedResult<TemplateSummaryDto>> Handle(GetTemplatesRegisterQuery request, CancellationToken ct)
    {
        var query = _db.Templates.AsNoTracking().AsQueryable();

        if (request.Statuses is { Count: > 0 })
            query = query.Where(t => request.Statuses.Contains(t.Status));

        if (request.TargetType is { } targetType)
            query = query.Where(t => t.TargetType == targetType);

        var templates = await query.ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            templates = templates.Where(t =>
                t.Name.En.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                t.Name.Ar.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                t.Key.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var sorted = Sort(templates, request.SortBy, request.SortDir);
        var total = sorted.Count;
        var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
        var page = request.Page <= 0 ? 1 : request.Page;

        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(KnowledgeMapping.ToSummary)
            .ToList();

        return new PagedResult<TemplateSummaryDto>(items, total, page, pageSize);
    }

    private static List<Template> Sort(List<Template> templates, string by, string dir)
    {
        var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        IEnumerable<Template> ordered = by.ToLowerInvariant() switch
        {
            "status" => templates.OrderBy(t => t.Status),
            "key" => templates.OrderBy(t => t.Key, StringComparer.Ordinal),
            "name" => templates.OrderBy(t => t.Name.En, StringComparer.OrdinalIgnoreCase),
            "target" => templates.OrderBy(t => t.TargetType),
            _ => templates.OrderBy(t => t.CreatedAt),
        };
        return (desc ? ordered.Reverse() : ordered).ToList();
    }
}
