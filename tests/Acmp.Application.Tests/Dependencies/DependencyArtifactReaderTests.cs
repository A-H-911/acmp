using Acmp.Modules.Dependencies.Application.Contracts;
using Acmp.Modules.Dependencies.Application.Features.GetDependenciesForArtifact;
using Acmp.Modules.Dependencies.Infrastructure.Directory;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Acmp.Application.Tests.Dependencies;

// The Acmp.Shared IDependencyArtifactReader port (P10f): reuses the existing GetDependenciesForArtifact read
// and maps the module read model to the primitive shared DTO. An endpoint-type name that is not a
// DependencyEndpointType returns empty without hitting the read (defence in depth for the graph composer).
public class DependencyArtifactReaderTests
{
    private static DependencyEdgeDto Edge(string otherType, Guid otherId, string kind, bool blocker) =>
        new(Guid.NewGuid(), "DPN-2026-003", otherType, otherId, "TOP-9", "Far", kind, "Open", blocker);

    [Fact] // A valid endpoint type is sent to the existing read and mapped field-for-field to the shared DTO.
    public async Task Maps_the_existing_read_to_the_shared_dto()
    {
        var otherId = Guid.NewGuid();
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetDependenciesForArtifactQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ArtifactDependenciesDto(
                new[] { Edge("Action", otherId, "Blocks", blocker: true) },
                Array.Empty<DependencyEdgeDto>())));

        var result = await new DependencyArtifactReader(sender).GetForArtifactAsync("Topic", Guid.NewGuid());

        result.Outbound.Should().ContainSingle();
        result.Outbound[0].OtherType.Should().Be("Action");
        result.Outbound[0].OtherId.Should().Be(otherId);
        result.Outbound[0].Kind.Should().Be("Blocks");
        result.Outbound[0].IsBlocker.Should().BeTrue();
        result.Inbound.Should().BeEmpty();
        await sender.Received(1).Send(Arg.Is<GetDependenciesForArtifactQuery>(q => q.Type == Acmp.Modules.Dependencies.Domain.Enums.DependencyEndpointType.Topic), Arg.Any<CancellationToken>());
    }

    [Fact] // A non-DependencyEndpointType name (e.g. an ArtifactType-only "Adr") returns empty, no read issued.
    public async Task Unknown_endpoint_type_returns_empty_without_reading()
    {
        var sender = Substitute.For<ISender>();

        var result = await new DependencyArtifactReader(sender).GetForArtifactAsync("Adr", Guid.NewGuid());

        result.Outbound.Should().BeEmpty();
        result.Inbound.Should().BeEmpty();
        await sender.DidNotReceive().Send(Arg.Any<GetDependenciesForArtifactQuery>(), Arg.Any<CancellationToken>());
    }
}
