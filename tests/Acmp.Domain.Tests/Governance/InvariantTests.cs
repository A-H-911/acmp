using Acmp.Modules.Governance.Domain;
using Acmp.Modules.Governance.Domain.Enums;
using Acmp.Modules.Governance.Domain.Events;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Domain.Tests.Governance;

// The Invariant aggregate state machine (docs/12 §9, W18/W21): draft → propose → activate; propose →
// request-changes → draft; active → supersede / retire. Once Active the statement is frozen — a correction is
// a new invariant. Pure domain — no EF, no MediatR.
public class InvariantTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly LocalizedString Statement = LocalizedString.Create("No cross-module DB access", "لا وصول لقاعدة وحدة أخرى");
    private static readonly LocalizedString Rationale = LocalizedString.Create("Preserves boundaries", "يحافظ على الحدود");
    private static readonly LocalizedString Reason = LocalizedString.Create("Superseded by a stronger rule", "استبدل بقاعدة أقوى");

    private static Invariant Draft() => Invariant.Draft("AIV-2026-001", InvariantCategory.Security,
        InvariantScope.Platform, Statement, Rationale, null, "kc-owner", "Owner", Now);

    private static Invariant Proposed() { var i = Draft(); i.Propose(Now); return i; }
    private static Invariant Active() { var i = Proposed(); i.Activate("kc-chair", "Chair", Now); return i; }

    [Fact]
    public void Draft_creates_a_draft_sets_the_key_and_raises_an_event()
    {
        var i = Draft();
        i.Status.Should().Be(InvariantStatus.Draft);
        i.Key.Should().Be("AIV-2026-001");
        i.Category.Should().Be(InvariantCategory.Security);
        i.Scope.Should().Be(InvariantScope.Platform);
        i.OwnerName.Should().Be("Owner");
        i.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InvariantDraftedEvent>();
    }

    [Fact]
    public void Draft_guards_required_fields()
    {
        FluentActions.Invoking(() => Invariant.Draft("k", InvariantCategory.Data, InvariantScope.Platform, null!, Rationale, null, "o", "O", Now))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Invariant.Draft("k", InvariantCategory.Data, InvariantScope.Platform, Statement, null!, null, "o", "O", Now))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Invariant.Draft("k", InvariantCategory.Data, InvariantScope.Platform, Statement, Rationale, null, "", "O", Now))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateDraft_replaces_content_only_while_draft_and_guards_fields()
    {
        var i = Draft();
        i.UpdateDraft(InvariantCategory.Data, InvariantScope.OrgWide, Statement,
            LocalizedString.Create("new", "جديد"), LocalizedString.Create("exc", "استثناء"), "kc-owner2", "Owner2");
        i.Category.Should().Be(InvariantCategory.Data);
        i.Scope.Should().Be(InvariantScope.OrgWide);
        i.ExceptionsPolicy!.En.Should().Be("exc");
        i.OwnerName.Should().Be("Owner2");

        FluentActions.Invoking(() => i.UpdateDraft(InvariantCategory.Data, InvariantScope.OrgWide, null!, Rationale, null, "o", "O")).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => i.UpdateDraft(InvariantCategory.Data, InvariantScope.OrgWide, Statement, null!, null, "o", "O")).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => i.UpdateDraft(InvariantCategory.Data, InvariantScope.OrgWide, Statement, Rationale, null, "", "O")).Should().Throw<InvalidOperationException>();

        FluentActions.Invoking(() => Active().UpdateDraft(InvariantCategory.Data, InvariantScope.OrgWide, Statement, Rationale, null, "o", "O"))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Propose_moves_draft_to_proposed_and_only_from_draft()
    {
        var i = Draft();
        i.Propose(Now);
        i.Status.Should().Be(InvariantStatus.Proposed);
        i.DomainEvents.OfType<InvariantProposedEvent>().Should().ContainSingle();
        FluentActions.Invoking(() => i.Propose(Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RequestChanges_returns_proposed_to_draft_and_only_from_proposed()
    {
        var i = Proposed();
        i.RequestChanges(Now);
        i.Status.Should().Be(InvariantStatus.Draft);
        i.DomainEvents.OfType<InvariantChangesRequestedEvent>().Should().ContainSingle();
        FluentActions.Invoking(() => i.RequestChanges(Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Activate_records_attribution_and_only_from_proposed()
    {
        var i = Proposed();
        i.Activate("kc-chair", "Chair", Now);
        i.Status.Should().Be(InvariantStatus.Active);
        i.ActivatedByName.Should().Be("Chair");
        i.ActivatedAt.Should().Be(Now);
        i.DomainEvents.OfType<InvariantActivatedEvent>().Should().ContainSingle();
        FluentActions.Invoking(() => i.Activate("x", "X", Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Proposed().Activate("", "X", Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Supersede_freezes_the_prior_with_a_backlink_and_only_from_active()
    {
        var i = Active();
        var successor = Guid.NewGuid();
        i.Supersede(successor, Reason, Now);
        i.Status.Should().Be(InvariantStatus.Superseded);
        i.SupersededByInvariantId.Should().Be(successor);
        i.SupersessionReason.Should().Be(Reason);
        i.DomainEvents.OfType<InvariantSupersededEvent>().Should().ContainSingle();

        FluentActions.Invoking(() => Draft().Supersede(successor, Reason, Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Active().Supersede(Guid.Empty, Reason, Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Active().Supersede(successor, null!, Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkSupersedes_sets_the_forward_link_and_guards_empty()
    {
        var i = Active();
        var prior = Guid.NewGuid();
        i.MarkSupersedes(prior);
        i.SupersedesInvariantId.Should().Be(prior);
        FluentActions.Invoking(() => i.MarkSupersedes(Guid.Empty)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Retire_takes_an_active_invariant_out_of_force_and_only_from_active()
    {
        var i = Active();
        i.Retire(Reason, Now);
        i.Status.Should().Be(InvariantStatus.Retired);
        i.RetirementReason.Should().Be(Reason);
        i.DomainEvents.OfType<InvariantRetiredEvent>().Should().ContainSingle();
        FluentActions.Invoking(() => Draft().Retire(Reason, Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Active().Retire(null!, Now)).Should().Throw<InvalidOperationException>();
    }
}
