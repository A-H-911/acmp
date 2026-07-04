using Acmp.Modules.Governance.Domain;
using Acmp.Modules.Governance.Domain.Enums;
using Acmp.Modules.Governance.Domain.Events;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Domain.Tests.Governance;

// The Adr aggregate state machine (docs/12 §8, W17/W21): draft → propose → approve; propose → request-changes
// → draft; approved → supersede / deprecate. The decisive rule (FR-101): once Approved the content is frozen —
// a correction is a new ADR. Pure domain — no EF, no MediatR.
public class AdrTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly LocalizedString Title = LocalizedString.Create("Adopt Keycloak", "اعتماد Keycloak");
    private static readonly LocalizedString Context = LocalizedString.Create("We need OIDC", "نحتاج OIDC");
    private static readonly LocalizedString Decision = LocalizedString.Create("Use Keycloak", "استخدام Keycloak");
    private static readonly LocalizedString Reason = LocalizedString.Create("Superseded by newer IdP", "استبدل بهوية أحدث");

    private static IEnumerable<AdrOptionInput> Options() => new[]
    {
        new AdrOptionInput(LocalizedString.Create("Keycloak", "Keycloak"), LocalizedString.Create("Chosen", "مختار"), true),
        new AdrOptionInput(LocalizedString.Create("Auth0", "Auth0"), null, false),
    };

    private static Adr Draft() => Adr.Draft("ADR-2026-001", Title, Context, null, Decision, null, null,
        Options(), "kc-author", "Author", sourceDecisionId: null, Now);

    private static Adr Proposed() { var a = Draft(); a.Propose(Now); return a; }
    private static Adr Approved() { var a = Proposed(); a.Approve("kc-chair", "Chair", Now); return a; }

    [Fact]
    public void Draft_creates_a_draft_sets_the_key_maps_options_and_raises_an_event()
    {
        var a = Draft();
        a.Status.Should().Be(AdrStatus.Draft);
        a.Key.Should().Be("ADR-2026-001");
        a.AuthorName.Should().Be("Author");
        a.Options.Should().HaveCount(2);
        a.Options.First().IsChosen.Should().BeTrue();
        a.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AdrDraftedEvent>();
    }

    [Fact]
    public void Draft_guards_required_fields()
    {
        FluentActions.Invoking(() => Adr.Draft("k", null!, Context, null, Decision, null, null, Options(), "a", "A", null, Now))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Adr.Draft("k", Title, null!, null, Decision, null, null, Options(), "a", "A", null, Now))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Adr.Draft("k", Title, Context, null, null!, null, null, Options(), "a", "A", null, Now))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Adr.Draft("k", Title, Context, null, Decision, null, null, Options(), "", "A", null, Now))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateDraft_replaces_content_and_options_only_while_draft()
    {
        var a = Draft();
        a.UpdateDraft(Title, Context, LocalizedString.Create("driver", "دافع"), Decision, null, null,
            new[] { new AdrOptionInput(LocalizedString.Create("Only", "فقط"), null, true) });
        a.Options.Should().ContainSingle();
        a.DecisionDrivers!.En.Should().Be("driver");

        var approved = Approved();
        FluentActions.Invoking(() => approved.UpdateDraft(Title, Context, null, Decision, null, null, Options()))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateDraft_guards_required_fields()
    {
        var a = Draft();
        FluentActions.Invoking(() => a.UpdateDraft(null!, Context, null, Decision, null, null, Options())).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => a.UpdateDraft(Title, null!, null, Decision, null, null, Options())).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => a.UpdateDraft(Title, Context, null, null!, null, null, Options())).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Propose_moves_draft_to_proposed_and_only_from_draft()
    {
        var a = Draft();
        a.Propose(Now);
        a.Status.Should().Be(AdrStatus.Proposed);
        a.DomainEvents.OfType<AdrProposedEvent>().Should().ContainSingle();
        FluentActions.Invoking(() => a.Propose(Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RequestChanges_returns_proposed_to_draft_and_only_from_proposed()
    {
        var a = Proposed();
        a.RequestChanges(Now);
        a.Status.Should().Be(AdrStatus.Draft);
        a.DomainEvents.OfType<AdrChangesRequestedEvent>().Should().ContainSingle();
        FluentActions.Invoking(() => a.RequestChanges(Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_records_attribution_and_only_from_proposed()
    {
        var a = Proposed();
        a.Approve("kc-chair", "Chair", Now);
        a.Status.Should().Be(AdrStatus.Approved);
        a.ApprovedByName.Should().Be("Chair");
        a.ApprovedAt.Should().Be(Now);
        a.DomainEvents.OfType<AdrApprovedEvent>().Should().ContainSingle();
        FluentActions.Invoking(() => a.Approve("x", "X", Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Proposed().Approve("", "X", Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Supersede_freezes_the_prior_with_a_backlink_and_only_from_approved()
    {
        var a = Approved();
        var successor = Guid.NewGuid();
        a.Supersede(successor, Reason, Now);
        a.Status.Should().Be(AdrStatus.Superseded);
        a.SupersededByAdrId.Should().Be(successor);
        a.SupersessionReason.Should().Be(Reason);
        a.DomainEvents.OfType<AdrSupersededEvent>().Should().ContainSingle();

        FluentActions.Invoking(() => Draft().Supersede(successor, Reason, Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Approved().Supersede(Guid.Empty, Reason, Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Approved().Supersede(successor, null!, Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkSupersedes_sets_the_forward_link_and_guards_empty()
    {
        var a = Approved();
        var prior = Guid.NewGuid();
        a.MarkSupersedes(prior);
        a.SupersedesAdrId.Should().Be(prior);
        FluentActions.Invoking(() => a.MarkSupersedes(Guid.Empty)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Deprecate_retires_an_approved_adr_with_a_reason_and_only_from_approved()
    {
        var a = Approved();
        a.Deprecate(Reason, Now);
        a.Status.Should().Be(AdrStatus.Deprecated);
        a.DeprecationReason.Should().Be(Reason);
        a.DomainEvents.OfType<AdrDeprecatedEvent>().Should().ContainSingle();
        FluentActions.Invoking(() => Draft().Deprecate(Reason, Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Approved().Deprecate(null!, Now)).Should().Throw<InvalidOperationException>();
    }
}
