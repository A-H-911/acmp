using Acmp.Modules.Dependencies.Application.Abstractions;
using Acmp.Modules.Dependencies.Application.Contracts;
using Acmp.Modules.Dependencies.Application.Internal;
using Acmp.Modules.Dependencies.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Dependencies.Application.Features.GetDependenciesForArtifact;

// The dependency panel for one artifact — its outbound edges (this artifact is the From end) and inbound
// edges (this artifact is the To end). Readable by any authenticated committee member (read-all). Removed
// edges are excluded. Keyed by (type, PublicId): the stable identity the SPA already holds from the
// artifact's detail payload.
public sealed record GetDependenciesForArtifactQuery(DependencyEndpointType Type, Guid Id)
    : IRequest<ArtifactDependenciesDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetDependenciesForArtifactHandler
    : IRequestHandler<GetDependenciesForArtifactQuery, ArtifactDependenciesDto>
{
    private readonly IDependenciesDbContext _db;

    public GetDependenciesForArtifactHandler(IDependenciesDbContext db) => _db = db;

    public async Task<ArtifactDependenciesDto> Handle(GetDependenciesForArtifactQuery request, CancellationToken ct)
    {
        var outbound = await _db.Dependencies.AsNoTracking()
            .Where(d => d.Status != DependencyStatus.Removed && d.FromType == request.Type && d.FromId == request.Id)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);

        var inbound = await _db.Dependencies.AsNoTracking()
            .Where(d => d.Status != DependencyStatus.Removed && d.ToType == request.Type && d.ToId == request.Id)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);

        return new ArtifactDependenciesDto(
            outbound.Select(DependencyMapping.ToOutboundEdge).ToList(),
            inbound.Select(DependencyMapping.ToInboundEdge).ToList());
    }
}
