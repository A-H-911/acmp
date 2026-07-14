using Acmp.Modules.Knowledge.Application.Abstractions;
using Acmp.Modules.Knowledge.Application.Contracts;
using Acmp.Modules.Knowledge.Application.Internal;
using Acmp.Modules.Knowledge.Domain;
using Acmp.Modules.Knowledge.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Knowledge.Application.Features.GetDocumentsRegister;

// The document register as a filtered/sorted/paged view, newest first by default. Readable by any authenticated
// committee member (read-all). Status + category filters run in SQL; the bilingual text search runs in memory
// after materialising — right-sized for one low-traffic committee (mirrors the research register).
public sealed record GetDocumentsRegisterQuery(
    IReadOnlyList<DocumentStatus>? Statuses = null,
    string? Category = null,
    string? Search = null,
    string SortBy = "created",
    string SortDir = "desc",
    int Page = 1,
    int PageSize = 25)
    : IRequest<PagedResult<DocumentSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetDocumentsRegisterHandler : IRequestHandler<GetDocumentsRegisterQuery, PagedResult<DocumentSummaryDto>>
{
    private readonly IKnowledgeDbContext _db;

    public GetDocumentsRegisterHandler(IKnowledgeDbContext db) => _db = db;

    public async Task<PagedResult<DocumentSummaryDto>> Handle(GetDocumentsRegisterQuery request, CancellationToken ct)
    {
        var query = _db.Documents.AsNoTracking().AsQueryable();

        if (request.Statuses is { Count: > 0 })
            query = query.Where(d => request.Statuses.Contains(d.Status));

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(d => d.Category == request.Category);

        var documents = await query.ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            documents = documents.Where(d =>
                d.Title.En.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                d.Title.Ar.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                d.Key.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var sorted = Sort(documents, request.SortBy, request.SortDir);
        var total = sorted.Count;
        var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
        var page = request.Page <= 0 ? 1 : request.Page;

        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(KnowledgeMapping.ToSummary)
            .ToList();

        return new PagedResult<DocumentSummaryDto>(items, total, page, pageSize);
    }

    private static List<Document> Sort(List<Document> documents, string by, string dir)
    {
        var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        IEnumerable<Document> ordered = by.ToLowerInvariant() switch
        {
            "status" => documents.OrderBy(d => d.Status),
            "key" => documents.OrderBy(d => d.Key, StringComparer.Ordinal),
            "title" => documents.OrderBy(d => d.Title.En, StringComparer.OrdinalIgnoreCase),
            _ => documents.OrderBy(d => d.CreatedAt),
        };
        return (desc ? ordered.Reverse() : ordered).ToList();
    }
}
