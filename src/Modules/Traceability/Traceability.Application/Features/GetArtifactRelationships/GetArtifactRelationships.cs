using Acmp.Modules.Traceability.Application.Abstractions;
using Acmp.Modules.Traceability.Application.Contracts;
using Acmp.Modules.Traceability.Application.Internal;
using Acmp.Modules.Traceability.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Traceability.Application.Features.GetArtifactRelationships;

// AC-062: the traceability panel for one artifact — its active outgoing and incoming typed edges, one hop
// (docs/30 §6.1). Transitive impact analysis (subgraph / BFS) is a later slice (P10f). Readable by any
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

    public GetArtifactRelationshipsHandler(ITraceabilityDbContext db) => _db = db;

    public async Task<ArtifactRelationshipsDto> Handle(GetArtifactRelationshipsQuery request, CancellationToken ct)
    {
        var outgoing = await _db.Relationships.AsNoTracking()
            .Where(r => r.IsActive && r.SourceType == request.Type && r.SourceId == request.Id)
            .OrderBy(r => r.CreatedAt)
            .Select(r => RelationshipMapping.ToEdge(r, RelationshipDirection.Outgoing))
            .ToListAsync(ct);

        var incoming = await _db.Relationships.AsNoTracking()
            .Where(r => r.IsActive && r.TargetType == request.Type && r.TargetId == request.Id)
            .OrderBy(r => r.CreatedAt)
            .Select(r => RelationshipMapping.ToEdge(r, RelationshipDirection.Incoming))
            .ToListAsync(ct);

        return new ArtifactRelationshipsDto(outgoing, incoming);
    }
}
