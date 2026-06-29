using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.Enums;
using Acmp.Shared.Domain.Events;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Domain.Tests.Shared;

// Covers the uncovered lines in BaseEntity and LocalizedString:
//   BaseEntity  — ClearDomainEvents(), Raise()/DomainEvents round-trip, PublicId default
//   LocalizedString — Create() guard throws, For(Language.Ar), record equality
public class SharedKernelTests
{
    // ── Local minimal IDomainEvent so we have no coupling to any module ──────
    private sealed record TestEvent(DateTimeOffset OccurredOn) : IDomainEvent;

    // Minimal concrete BaseEntity; the abstract class cannot be instantiated directly.
    private sealed class ConcreteEntity : BaseEntity { }

    // ── BaseEntity ────────────────────────────────────────────────────────────

    [Fact]
    public void BaseEntity_has_a_non_empty_default_PublicId()
    {
        // Arrange / Act
        var entity = new ConcreteEntity();

        // Assert
        entity.PublicId.Should().NotBeEmpty();
    }

    [Fact]
    public void Raise_adds_event_to_DomainEvents_and_they_are_retrievable()
    {
        // Arrange
        var entity = new ConcreteEntity();
        var ev = new TestEvent(DateTimeOffset.UtcNow);

        // Act
        entity.Raise(ev);

        // Assert
        entity.DomainEvents.Should().ContainSingle()
            .Which.Should().BeSameAs(ev);
    }

    [Fact]
    public void ClearDomainEvents_empties_the_collection()
    {
        // Arrange
        var entity = new ConcreteEntity();
        entity.Raise(new TestEvent(DateTimeOffset.UtcNow));
        entity.Raise(new TestEvent(DateTimeOffset.UtcNow.AddSeconds(1)));
        entity.DomainEvents.Should().HaveCount(2);

        // Act  ← this branch was 0% before this test
        entity.ClearDomainEvents();

        // Assert
        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Raise_multiple_events_preserves_insertion_order()
    {
        // Arrange
        var entity = new ConcreteEntity();
        var first = new TestEvent(DateTimeOffset.UtcNow);
        var second = new TestEvent(DateTimeOffset.UtcNow.AddSeconds(1));

        // Act
        entity.Raise(first);
        entity.Raise(second);

        // Assert
        entity.DomainEvents.Should().HaveCount(2);
        entity.DomainEvents.First().Should().BeSameAs(first);
        entity.DomainEvents.Last().Should().BeSameAs(second);
    }

    // ── LocalizedString ───────────────────────────────────────────────────────

    [Fact]
    public void LocalizedString_Create_returns_trimmed_values_for_valid_input()
    {
        // Act
        var ls = LocalizedString.Create(" Hello ", " مرحبا ");

        // Assert
        ls.En.Should().Be("Hello");
        ls.Ar.Should().Be("مرحبا");
    }

    [Fact]
    public void LocalizedString_For_Language_En_returns_En_value()
    {
        // Arrange
        var ls = LocalizedString.Create("English", "عربي");

        // Act / Assert
        ls.For(Language.En).Should().Be("English");
    }

    [Fact]
    public void LocalizedString_For_Language_Ar_returns_Ar_value()
    {
        // Arrange  ← Language.Ar branch was not covered
        var ls = LocalizedString.Create("English", "عربي");

        // Act / Assert
        ls.For(Language.Ar).Should().Be("عربي");
    }

    [Fact]
    public void LocalizedString_Create_throws_when_En_is_null_or_whitespace()
    {
        // Act / Assert  ← first guard throw was uncovered
        Action act = () => LocalizedString.Create("   ", "عربي");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("en");
    }

    [Fact]
    public void LocalizedString_Create_throws_when_Ar_is_null_or_whitespace()
    {
        // Act / Assert  ← second guard throw was uncovered
        Action act = () => LocalizedString.Create("Hello", "   ");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("ar");
    }

    [Fact]
    public void LocalizedString_record_equality_is_value_based()
    {
        // Arrange
        var a = new LocalizedString("Hello", "مرحبا");
        var b = new LocalizedString("Hello", "مرحبا");
        var c = new LocalizedString("Hi", "مرحبا");

        // Assert
        a.Should().Be(b);
        a.Should().NotBe(c);
    }
}
