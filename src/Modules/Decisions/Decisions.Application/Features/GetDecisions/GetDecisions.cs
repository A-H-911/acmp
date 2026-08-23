using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.GetDecisions;

// Committee-wide decisions register (across every topic), newest-issued first. Feeds the committee
// dashboard's "last N issued decisions" (AC-064) and the Reports decision-history view. Optional
// status filter (DecisionStatus name; an unparseable value is ignored — the caller sees all) and an
// optional limit (absent / <=0 = the whole register). Read-all: any authenticated committee member.
// A register is not a cross-module read — Decisions reads only its own tables (ADR-0001).
public sealed record GetDecisionsQuery(string? Status, int? Limit)
    : IRequest<IReadOnlyList<DecisionSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetDecisionsHandler : IRequestHandler<GetDecisionsQuery, IReadOnlyList<DecisionSummaryDto>>
{
    private readonly IDecisionsDbContext _db;

    public GetDecisionsHandler(IDecisionsDbContext db) => _db = db;

    public async Task<IReadOnlyList<DecisionSummaryDto>> Handle(GetDecisionsQuery request, CancellationToken ct)
    {
        var query = _db.Decisions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<DecisionStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(d => d.Status == status);
        }

        // Issued decisions sort by their issue time; a still-Draft record (no IssuedAt) falls back to
        // CreatedAt so the register stays deterministic even when filtered to all statuses.
        query = query
            .OrderByDescending(d => d.IssuedAt ?? d.CreatedAt)
            .ThenByDescending(d => d.Id);

        // DEF-104: cap the caller-supplied limit. ⚠ A NULL limit deliberately still means "no Take":
        // this read is not caller-amplifiable — with no limit it reads the register once, which is what
        // the reports page needs — so introducing a cap where none existed would be a silent truncation
        // (DEF-103's shape) rather than a fix. Only the value the CALLER supplies is bounded here.
        if (request.Limit is int n and > 0)
        {
            query = query.Take(PageSize.Clamp(n));
        }

        var decisions = await query.ToListAsync(ct);
        return decisions.Select(DecisionMapping.ToSummary).ToList();
    }
}
