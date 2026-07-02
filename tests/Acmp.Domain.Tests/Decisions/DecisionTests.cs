using Acmp.Modules.Decisions.Domain;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Modules.Decisions.Domain.Events;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Domain.Tests.Decisions;

// W12/W21 aggregate behaviour: draft guards, issue (Draft→Issued + override rule), supersede
// (Issued→Superseded + back-link), and the AC-027 immutability that there is no edit path.
public class DecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Topic = Guid.NewGuid();
    private static readonly LocalizedString Title = LocalizedString.Create("Adopt Keycloak", "اعتماد كيكلوك");
    private static readonly LocalizedString Statement = LocalizedString.Create("The committee adopts Keycloak.", "تعتمد اللجنة كيكلوك.");
    private static readonly LocalizedString Rationale = LocalizedString.Create("Sound choice", "اختيار سليم");

    private static Decision Drafted(DecisionOutcome outcome = DecisionOutcome.Approved,
        IEnumerable<DecisionConditionInput>? conditions = null) =>
        Decision.Draft("DECN-2026-001", Topic, meetingId: null, outcome, Title, Statement, Rationale, alternatives: null,
            voteId: null, conditions ?? Array.Empty<DecisionConditionInput>(), "kc-chair", Now);

    [Fact]
    public void Draft_starts_Draft_and_raises_event()
    {
        var d = Drafted();

        d.Status.Should().Be(DecisionStatus.Draft);
        d.TopicId.Should().Be(Topic);
        d.Outcome.Should().Be(DecisionOutcome.Approved);
        d.DomainEvents.OfType<DecisionDraftedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Draft_requires_a_topic_a_title_and_a_rationale()
    {
        var noTopic = () => Decision.Draft("DECN-2026-002", Guid.Empty, null, DecisionOutcome.Approved,
            Title, Statement, Rationale, null, null, Array.Empty<DecisionConditionInput>(), "kc", Now);
        noTopic.Should().Throw<InvalidOperationException>().WithMessage("*topic*");

        var noTitle = () => Decision.Draft("DECN-2026-002", Topic, null, DecisionOutcome.Approved,
            null!, Statement, Rationale, null, null, Array.Empty<DecisionConditionInput>(), "kc", Now);
        noTitle.Should().Throw<InvalidOperationException>().WithMessage("*title*");

        var noStatement = () => Decision.Draft("DECN-2026-002", Topic, null, DecisionOutcome.Approved,
            Title, null!, Rationale, null, null, Array.Empty<DecisionConditionInput>(), "kc", Now);
        noStatement.Should().Throw<InvalidOperationException>().WithMessage("*statement*");

        var noRationale = () => Decision.Draft("DECN-2026-002", Topic, null, DecisionOutcome.Approved,
            Title, Statement, null!, null, null, Array.Empty<DecisionConditionInput>(), "kc", Now);
        noRationale.Should().Throw<InvalidOperationException>().WithMessage("*rationale*");
    }

    [Fact]
    public void ConditionallyApproved_requires_at_least_one_condition()
    {
        var act = () => Drafted(DecisionOutcome.ConditionallyApproved);
        act.Should().Throw<InvalidOperationException>().WithMessage("*condition*");

        var ok = Drafted(DecisionOutcome.ConditionallyApproved, new[]
        {
            new DecisionConditionInput(LocalizedString.Create("Add tests", "أضف اختبارات"), Now.AddDays(7)),
        });
        ok.Conditions.Should().ContainSingle();
        ok.Conditions.Single().Status.Should().Be(DecisionConditionStatus.Open);
    }

    [Fact]
    public void Issue_moves_Draft_to_Issued_and_raises_event()
    {
        var d = Drafted();

        d.Issue("kc-chair", "Sara Chair", chairOverride: false, overrideJustification: null, Now);

        d.Status.Should().Be(DecisionStatus.Issued);
        d.IssuedAt.Should().Be(Now);
        d.ChairApprovedByUserId.Should().Be("kc-chair");
        d.ChairApprovedByName.Should().Be("Sara Chair");
        d.DomainEvents.OfType<DecisionIssuedEvent>().Should().ContainSingle()
            .Which.ChairOverride.Should().BeFalse();
    }

    [Fact]
    public void Issue_with_override_requires_a_justification()
    {
        var d = Drafted();
        var act = () => d.Issue("kc-chair", "Sara", chairOverride: true, overrideJustification: null, Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*justification*");

        var ok = Drafted();
        var justification = LocalizedString.Create("Overriding the vote", "تجاوز التصويت");
        ok.Issue("kc-chair", "Sara", chairOverride: true, justification, Now);
        ok.ChairOverride.Should().BeTrue();
        ok.OverrideJustification.Should().Be(justification);
        ok.DomainEvents.OfType<DecisionIssuedEvent>().Single().ChairOverride.Should().BeTrue();
    }

    [Fact]
    public void Cannot_issue_twice_or_from_a_non_draft_state()
    {
        var d = Drafted();
        d.Issue("kc-chair", "Sara", false, null, Now);

        var act = () => d.Issue("kc-chair", "Sara", false, null, Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Issued*");
    }

    [Fact]
    public void Supersede_requires_an_issued_decision_and_records_the_backlink()
    {
        var d = Drafted();
        var reason = LocalizedString.Create("Corrected scope", "نطاق مصحح");

        var beforeIssue = () => d.Supersede(Guid.NewGuid(), reason, Now);
        beforeIssue.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");

        d.Issue("kc-chair", "Sara", false, null, Now);
        var successor = Guid.NewGuid();
        d.Supersede(successor, reason, Now.AddDays(1));

        d.Status.Should().Be(DecisionStatus.Superseded);
        d.SupersededByDecisionId.Should().Be(successor);
        d.SupersessionReason.Should().Be(reason);
        d.DomainEvents.OfType<DecisionSupersededEvent>().Should().ContainSingle();
    }

    [Fact] // AC-027: an issued decision is immutable — there is no edit path, and re-superseding throws.
    public void Issued_decision_is_immutable_no_re_supersede()
    {
        var d = Drafted();
        d.Issue("kc-chair", "Sara", false, null, Now);
        var reason = LocalizedString.Create("r", "ر");
        d.Supersede(Guid.NewGuid(), reason, Now);

        var act = () => d.Supersede(Guid.NewGuid(), reason, Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Superseded*");

        // No public mutators exist on the Decision aggregate's own state (outcome/rationale/conditions/…):
        // the only public members are the factory + Issue + Supersede. Base audit/identity stamps
        // (AuditableEntity/BaseEntity) are EF infra and out of scope here — hence DeclaredOnly.
        typeof(Decision)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(p => p.CanWrite && p.SetMethod!.IsPublic)
            .Should().BeEmpty();
    }
}
