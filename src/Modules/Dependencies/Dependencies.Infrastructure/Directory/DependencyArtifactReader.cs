using Acmp.Modules.Dependencies.Application.Features.GetDependenciesForArtifact;
using Acmp.Modules.Dependencies.Domain.Enums;
using Acmp.Shared.Contracts.Dependencies;
using MediatR;

namespace Acmp.Modules.Dependencies.Infrastructure.Directory;

// Dependencies-side implementation of the shared IDependencyArtifactReader port (ADR-0001, P10f): lets the
// Traceability impact-graph composer read an artifact's dependency edges without touching this module's
// tables. It reuses the EXISTING GetDependenciesForArtifact read (one source of truth — same Removed-excluded
// filter and IsBlocker derivation), then maps the module read model to the primitive shared DTO. An unknown
// endpoint-type name (e.g. an ArtifactType with no dependency endpoint) returns empty — never throws — so a
// far node the composer can't map just contributes no dependency edges.
public sealed class DependencyArtifactReader : IDependencyArtifactReader
{
    private static readonly DependencyGraphEdges Empty =
        new(Array.Empty<DependencyGraphEdge>(), Array.Empty<DependencyGraphEdge>());

    private readonly ISender _sender;

    public DependencyArtifactReader(ISender sender) => _sender = sender;

    public async Task<DependencyGraphEdges> GetForArtifactAsync(string typeName, Guid id, CancellationToken ct = default)
    {
        if (!Enum.TryParse<DependencyEndpointType>(typeName, out var type))
            return Empty;

        var dto = await _sender.Send(new GetDependenciesForArtifactQuery(type, id), ct);

        return new DependencyGraphEdges(
            dto.Outbound.Select(e => new DependencyGraphEdge(
                e.Id, e.Key, e.OtherType, e.OtherId, e.OtherKey, e.OtherTitle, e.Kind, e.Status, e.IsBlocker)).ToList(),
            dto.Inbound.Select(e => new DependencyGraphEdge(
                e.Id, e.Key, e.OtherType, e.OtherId, e.OtherKey, e.OtherTitle, e.Kind, e.Status, e.IsBlocker)).ToList());
    }
}
