using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Domain.Events;
using Acmp.Shared.Authorization.Abac;
using FluentAssertions;

namespace Acmp.Domain.Tests.Topics;

/// <summary>
/// FR-163 / C-AUTHZ-04 (DEC-063) — the Restricted classification on the aggregate.
///
/// The assertion that matters most here is the EnsureMutable EXEMPTION. Every other field setter on
/// this aggregate refuses once a topic is Decided/Closed/Converted, and inheriting that would make an
/// archived sensitive topic PERMANENTLY undeclassifiable — which is exactly when a declassification
/// request arrives. A test that only checked "Restrict sets the flag" would pass against the broken
/// version, so the terminal-status cases are the point of this file rather than padding.
/// </summary>
public class TopicConfidentialityTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);
    private const string ActorSub = "kc-sara";
    private const string ActorName = "Sara S.";

    private static Topic NewDraft() => Topic.Draft(
        "TOP-2026-050", "Adopt Keycloak", "Consolidate IAM.", "Fragmented auth is risky.",
        TopicType.ArchitectureDecision, TopicUrgency.Normal, TopicSource.SecurityFinding,
        "kc-omar", "Omar H.", new[] { "identity" }, Array.Empty<string>(), Array.Empty<string>());

    private static Topic Decided()
    {
        var t = NewDraft();
        t.Submit(Now);
        t.BeginTriage(ActorSub, ActorName, Now);
        t.Accept(Guid.NewGuid(), "Owner O.", ActorSub, ActorName, Now);
        t.MarkPrepared(ActorSub, ActorName, Now);
        t.Schedule(Guid.NewGuid(), ActorSub, ActorName, Now);
        t.EnterCommittee(ActorSub, ActorName, Now);
        t.Decide(ActorSub, ActorName, Now);
        return t;
    }

    [Fact]
    public void A_topic_is_unrestricted_by_default()
    {
        // The default must be OPEN: confidentiality narrows, and a classification nobody chose would
        // silently hide committee business from the committee.
        NewDraft().IsRestricted.Should().BeFalse();
    }

    [Fact]
    public void Restrict_sets_the_flag_and_raises_the_event_carrying_the_new_state()
    {
        var t = NewDraft();

        t.Restrict(ActorSub, ActorName, Now);

        t.IsRestricted.Should().BeTrue();
        var evt = t.DomainEvents.OfType<TopicRestrictedEvent>().Should().ContainSingle().Subject;
        evt.IsRestricted.Should().BeTrue();
        evt.ActorSub.Should().Be(ActorSub);
    }

    [Fact]
    public void Declassify_clears_the_flag_and_raises_the_event_carrying_the_new_state()
    {
        var t = NewDraft();
        t.Restrict(ActorSub, ActorName, Now);

        t.Declassify(ActorSub, ActorName, Now);

        t.IsRestricted.Should().BeFalse();
        t.DomainEvents.OfType<TopicRestrictedEvent>().Last().IsRestricted.Should().BeFalse();
    }

    [Fact]
    public void Re_restricting_an_already_restricted_topic_raises_nothing()
    {
        var t = NewDraft();
        t.Restrict(ActorSub, ActorName, Now);

        t.Restrict(ActorSub, ActorName, Now);

        // Idempotent by state, not by call count: a second Restrict is not a classification EVENT, and
        // emitting one would put a change in the audit trail that never happened.
        t.DomainEvents.OfType<TopicRestrictedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Declassifying_an_already_open_topic_raises_nothing()
    {
        var t = NewDraft();

        t.Declassify(ActorSub, ActorName, Now);

        t.DomainEvents.OfType<TopicRestrictedEvent>().Should().BeEmpty();
    }

    // ---- the EnsureMutable exemption (the reason this file exists) ----

    [Theory]
    [InlineData(TopicStatus.Decided)]
    [InlineData(TopicStatus.Closed)]
    [InlineData(TopicStatus.Converted)]
    public void A_terminal_topic_can_still_be_restricted_and_declassified(TopicStatus terminal)
    {
        var t = Decided();
        if (terminal == TopicStatus.Closed) t.Close(ActorSub, ActorName, Now);
        if (terminal == TopicStatus.Converted) t.Convert("superseded by an ADR", ActorSub, ActorName, Now);
        t.Status.Should().Be(terminal, "the fixture must actually reach the status under test");

        // Ordinary metadata edits ARE refused here — that contrast is the control.
        var blocked = () => t.SetUrgency(TopicUrgency.Critical);
        blocked.Should().Throw<InvalidOperationException>();

        t.Restrict(ActorSub, ActorName, Now);
        t.IsRestricted.Should().BeTrue();

        t.Declassify(ActorSub, ActorName, Now);
        t.IsRestricted.Should().BeFalse("an archived sensitive topic must not be permanently undeclassifiable");
    }

    [Fact]
    public void Topic_exposes_its_classification_through_the_shared_ABAC_contract()
    {
        var t = NewDraft();
        t.Restrict(ActorSub, ActorName, Now);

        // The shared kernel authorizes against IConfidentialResource, never against Topic. If the
        // aggregate stopped implementing it the handler would silently never be invoked — the DEF-068
        // shape — so the contract is asserted rather than assumed.
        ((IConfidentialResource)t).IsRestricted.Should().BeTrue();
    }
}
