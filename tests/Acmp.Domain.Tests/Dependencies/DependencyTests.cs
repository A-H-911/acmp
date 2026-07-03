using Acmp.Modules.Dependencies.Domain;
using Acmp.Modules.Dependencies.Domain.Enums;
using FluentAssertions;

namespace Acmp.Domain.Tests.Dependencies;

// The Dependency aggregate: create guards (empty endpoints, self-loop, undefined kind, trimming) and the
// Open → Resolved / Open → Removed transitions (each only legal from Open). Pure domain — no EF, no MediatR.
public class DependencyTests
{
    private static Dependency Create(
        DependencyEndpointType fromType = DependencyEndpointType.Topic, Guid? fromId = null,
        DependencyEndpointType toType = DependencyEndpointType.Action, Guid? toId = null,
        DependencyKind kind = DependencyKind.BlockedBy, string? note = "  handle first  ") =>
        Dependency.Create("DPN-2026-001", fromType, fromId ?? Guid.NewGuid(), "  TOP-2026-001  ", "  Gateway  ",
            toType, toId ?? Guid.NewGuid(), "  ACT-2026-009  ", "  Rotate keys  ", kind, note);

    [Fact]
    public void Create_opens_the_dependency_trims_snapshots_and_note()
    {
        var d = Create();
        d.Status.Should().Be(DependencyStatus.Open);
        d.Key.Should().Be("DPN-2026-001");
        d.FromKey.Should().Be("TOP-2026-001");
        d.FromTitle.Should().Be("Gateway");
        d.ToKey.Should().Be("ACT-2026-009");
        d.ToTitle.Should().Be("Rotate keys");
        d.Kind.Should().Be(DependencyKind.BlockedBy);
        d.Note.Should().Be("handle first");
    }

    [Fact]
    public void Create_nulls_a_blank_note()
    {
        Create(note: "   ").Note.Should().BeNull();
        Create(note: null).Note.Should().BeNull();
    }

    [Fact]
    public void Create_rejects_empty_endpoints()
    {
        FluentActions.Invoking(() => Create(fromId: Guid.Empty)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Create(toId: Guid.Empty)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_rejects_a_self_loop()
    {
        var id = Guid.NewGuid();
        FluentActions.Invoking(() => Create(fromType: DependencyEndpointType.Topic, fromId: id,
                toType: DependencyEndpointType.Topic, toId: id))
            .Should().Throw<InvalidOperationException>().WithMessage("*itself*");
    }

    [Fact] // Same id but different endpoint type is NOT a self-loop.
    public void Create_allows_same_id_across_different_endpoint_types()
    {
        var id = Guid.NewGuid();
        FluentActions.Invoking(() => Create(fromType: DependencyEndpointType.Topic, fromId: id,
                toType: DependencyEndpointType.Action, toId: id))
            .Should().NotThrow();
    }

    [Fact]
    public void Create_rejects_an_undefined_kind()
    {
        FluentActions.Invoking(() => Create(kind: (DependencyKind)0)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Create(kind: (DependencyKind)99)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Resolve_moves_open_to_resolved()
    {
        var d = Create();
        d.Resolve();
        d.Status.Should().Be(DependencyStatus.Resolved);
    }

    [Fact]
    public void Resolve_is_rejected_when_not_open()
    {
        var d = Create();
        d.Resolve();
        FluentActions.Invoking(() => d.Resolve()).Should().Throw<InvalidOperationException>().WithMessage("*open*");

        var removed = Create();
        removed.Remove();
        FluentActions.Invoking(() => removed.Resolve()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Remove_moves_open_to_removed()
    {
        var d = Create();
        d.Remove();
        d.Status.Should().Be(DependencyStatus.Removed);
    }

    [Fact]
    public void Remove_is_rejected_when_not_open()
    {
        var d = Create();
        d.Remove();
        FluentActions.Invoking(() => d.Remove()).Should().Throw<InvalidOperationException>().WithMessage("*open*");

        var resolved = Create();
        resolved.Resolve();
        FluentActions.Invoking(() => resolved.Remove()).Should().Throw<InvalidOperationException>();
    }
}
