using Acmp.Modules.Knowledge.Domain;
using Acmp.Modules.Knowledge.Domain.Enums;
using Acmp.Modules.Knowledge.Domain.Events;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Domain.Tests.Knowledge;

// Unit tests for the Template aggregate (P15d-2, FR-119) — the Active→Deprecated lifecycle, Edit bumping a plain
// Version counter (no snapshot history, unlike Document), and Deprecated-terminal immutability. No EF: invariants
// are exercised directly.
public class TemplateTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static LocalizedString L(string s = "x") => LocalizedString.Create(s, s);

    private static Template Active() =>
        Template.Create("TPL-2026-001", L("Topic intake"), TemplateTargetType.Topic, "# {{title}}", Now);

    // ── Create ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Create_makes_an_active_v1_and_raises_the_event()
    {
        var t = Active();

        t.Key.Should().Be("TPL-2026-001");
        t.Status.Should().Be(TemplateStatus.Active);
        t.TargetType.Should().Be(TemplateTargetType.Topic);
        t.Name.En.Should().Be("Topic intake");
        t.Body.Should().Be("# {{title}}");
        t.Version.Should().Be(1);

        t.DomainEvents.OfType<TemplateCreatedEvent>().Should().ContainSingle()
            .Which.TargetType.Should().Be(TemplateTargetType.Topic);
    }

    [Fact]
    public void Create_trims_the_body_and_key()
    {
        Template.Create("  TPL-2026-001 ", L(), TemplateTargetType.Adr, "  body  ", Now)
            .Body.Should().Be("body");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Create_requires_name_and_body(bool nullName, bool blankBody)
    {
        var act = () => Template.Create("TPL-2026-001",
            nullName ? null! : L(), TemplateTargetType.MinutesOfMeeting, blankBody ? "  " : "body", Now);
        act.Should().Throw<InvalidOperationException>();
    }

    // ── Edit ─────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Edit_bumps_version_and_revises_name_and_body_without_a_snapshot()
    {
        var t = Active();
        t.Edit(L("Topic intake v2"), "# {{title}} v2", Now.AddDays(1));

        t.Version.Should().Be(2);
        t.Name.En.Should().Be("Topic intake v2");
        t.Body.Should().Be("# {{title}} v2");
        t.TargetType.Should().Be(TemplateTargetType.Topic); // TargetType is immutable
        t.DomainEvents.OfType<TemplateEditedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Edit_requires_name_and_body()
    {
        var t = Active();
        t.Invoking(x => x.Edit(null!, "body", Now)).Should().Throw<InvalidOperationException>();
        t.Invoking(x => x.Edit(L(), "  ", Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Edit_is_rejected_once_deprecated()
    {
        var t = Active();
        t.Deprecate(Now);
        t.Invoking(x => x.Edit(L(), "body", Now)).Should().Throw<InvalidOperationException>();
    }

    // ── Deprecate ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Deprecate_retires_an_active_template_and_is_terminal()
    {
        var t = Active();
        t.Deprecate(Now);

        t.Status.Should().Be(TemplateStatus.Deprecated);
        t.DomainEvents.OfType<TemplateDeprecatedEvent>().Should().ContainSingle();
        // Deprecated is terminal — no re-deprecate.
        t.Invoking(x => x.Deprecate(Now)).Should().Throw<InvalidOperationException>();
    }
}
