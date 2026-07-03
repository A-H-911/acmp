using Acmp.Modules.Traceability.Application.Contracts;
using Acmp.Modules.Traceability.Application.Features.GetArtifactRelationships;
using Acmp.Modules.Traceability.Application.Internal;
using Acmp.Modules.Traceability.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Dependencies;
using Acmp.Shared.Contracts.Topics;
using MediatR;

namespace Acmp.Modules.Traceability.Application.Features.GetImpactGraph;

// FR-096: the depth-bounded impact subgraph around one artifact (P10f). Read-time composition of this module's
// typed Relationship edges (via the existing GetArtifactRelationships read — one source of truth) with the
// Dependencies module's governed edges (Acmp.Shared IDependencyArtifactReader port) and Topic streams (Topics
// ITopicStreamReader port). No cross-module table reads (ADR-0001). Readable by any authenticated committee
// member (read-all). The traversal, node ceiling, cycle guard, and depth clamp all live in the composer.
public sealed record GetImpactGraphQuery(ArtifactType Type, Guid Id, int Depth)
    : IRequest<ImpactGraphDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetImpactGraphHandler : IRequestHandler<GetImpactGraphQuery, ImpactGraphDto>
{
    private readonly ISender _sender;
    private readonly IDependencyArtifactReader _dependencies;
    private readonly ITopicStreamReader _streams;

    public GetImpactGraphHandler(ISender sender, IDependencyArtifactReader dependencies, ITopicStreamReader streams)
    {
        _sender = sender;
        _dependencies = dependencies;
        _streams = streams;
    }

    public Task<ImpactGraphDto> Handle(GetImpactGraphQuery request, CancellationToken ct) =>
        ImpactGraphComposer.BuildAsync(
            request.Type, request.Id, request.Depth,
            (type, id, c) => _sender.Send(new GetArtifactRelationshipsQuery(type, id), c),
            (typeName, id, c) => _dependencies.GetForArtifactAsync(typeName, id, c),
            (topicId, c) => _streams.GetStreamsAsync(topicId, c),
            ct);
}
