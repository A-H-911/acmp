using Acmp.Modules.Traceability.Application.Abstractions;
using Acmp.Modules.Traceability.Application.Contracts;
using Acmp.Modules.Traceability.Application.Internal;
using Acmp.Modules.Traceability.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Topics;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Traceability.Application.Features.GetArtifactRelationships;

// AC-062: the traceability panel for one artifact — its active outgoing and incoming typed edges, one hop
// (docs/domain/search-and-traceability.md §6.1). Transitive impact analysis (subgraph / BFS) is a later slice (P10f). Readable by any
// authenticated committee member (read-all). Keyed by (type, PublicId): the stable identity the SPA already
// holds from the artifact's detail payload.
public sealed record GetArtifactRelationshipsQuery(ArtifactType Type, Guid Id)
    : IRequest<ArtifactRelationshipsDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetArtifactRelationshipsHandler
    : IRequestHandler<GetArtifactRelationshipsQuery, ArtifactRelationshipsDto>
{
    private readonly ITraceabilityDbContext _db;
    private readonly ITopicConfidentiality _confidentiality;

    public GetArtifactRelationshipsHandler(ITraceabilityDbContext db, ITopicConfidentiality confidentiality)
    {
        _db = db;
        _confidentiality = confidentiality;
    }

    public async Task<ArtifactRelationshipsDto> Handle(GetArtifactRelationshipsQuery request, CancellationToken ct)
    {
        // FR-163 / AC-114 — Relationship froze both endpoints' key+title at create time (ADR-0019), so an
        // edge touching a Restricted topic carries that topic's title into a read-all panel. Resolved ONCE
        // for both directions; this read also backs GetImpactGraph, so redacting here covers the graph too.
        var hidden = await _confidentiality.GetHiddenTopicIdsAsync(ct);

        // ⚠ THE EDGE IS DROPPED, NOT MASKED — the opposite of the agenda-item choice, for a structural
        // reason. A relationship row IS a pointer: blanking its far endpoint's identity leaves a node with
        // no id, and ImpactGraphComposer walks OtherId to expand the next hop, so a masked edge would enter
        // the BFS as a real node keyed on an empty Guid. An agenda item still means something with its topic
        // withheld (order, time-box, "item 3 of 6"); an edge to nowhere does not.
        //
        // ⚠ FILTERED ON BOTH ENDPOINTS, NOT JUST THE FAR ONE, AND THAT IS WHAT MAKES THE FOCUS SAFE. Asking
        // for a hidden topic's own panel matches nothing, so it answers exactly as a nonexistent id does —
        // no separate focus guard here, in GetImpactGraph, or in the Dependencies reads.
        IQueryable<Domain.Relationship> Visible(IQueryable<Domain.Relationship> q) =>
            hidden.Length == 0
                ? q
                : q.Where(r =>
                    !(r.SourceType == ArtifactType.Topic && hidden.Contains(r.SourceId)) &&
                    !(r.TargetType == ArtifactType.Topic && hidden.Contains(r.TargetId)));

        var outgoing = await Visible(_db.Relationships.AsNoTracking()
                .Where(r => r.IsActive && r.SourceType == request.Type && r.SourceId == request.Id))
            .OrderBy(r => r.CreatedAt)
            .Select(r => RelationshipMapping.ToEdge(r, RelationshipDirection.Outgoing))
            .ToListAsync(ct);

        var incoming = await Visible(_db.Relationships.AsNoTracking()
                .Where(r => r.IsActive && r.TargetType == request.Type && r.TargetId == request.Id))
            .OrderBy(r => r.CreatedAt)
            .Select(r => RelationshipMapping.ToEdge(r, RelationshipDirection.Incoming))
            .ToListAsync(ct);

        return new ArtifactRelationshipsDto(outgoing, incoming);
    }
}
