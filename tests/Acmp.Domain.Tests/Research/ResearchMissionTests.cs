using Acmp.Modules.Research.Domain;
using Acmp.Modules.Research.Domain.Enums;
using Acmp.Modules.Research.Domain.Events;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Domain.Tests.Research;

// Unit tests for the ResearchMission aggregate (P15a) — the Proposed→Active→Completed lifecycle, the
// side-exit Cancel, terminal immutability, and the owned Finding/Recommendation child operations with their
// per-mission ordinal keys. No EF: the aggregate invariants are exercised directly.
public class ResearchMissionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static LocalizedString L(string s = "x") => LocalizedString.Create(s, s);

    private static ResearchMission Proposed() =>
        ResearchMission.Propose("RMS-2026-001", L("Title"), L("Question"), "kc-owner", "Owner", null, null, Now);

    private static ResearchMission Active()
    {
        var m = Proposed();
        m.Activate(Now);
        return m;
    }

    // ── Propose ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Propose_creates_a_proposed_mission_and_raises_the_event()
    {
        var m = ResearchMission.Propose("RMS-2026-001", L("T"), L("Q"), "kc-owner", "Owner", "keystone-ref", Guid.NewGuid(), Now);

        m.Key.Should().Be("RMS-2026-001");
        m.Status.Should().Be(ResearchMissionStatus.Proposed);
        m.OwnerUserId.Should().Be("kc-owner");
        m.KeystonePackageRef.Should().Be("keystone-ref");
        m.SourceTopicId.Should().NotBeNull();
        m.Findings.Should().BeEmpty();
        m.Recommendations.Should().BeEmpty();
        m.DomainEvents.OfType<ResearchProposedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Propose_blanks_a_whitespace_keystone_ref_to_null()
    {
        var m = ResearchMission.Propose("RMS-2026-001", L(), L(), "kc-owner", "Owner", "   ", null, Now);
        m.KeystonePackageRef.Should().BeNull();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Propose_requires_title_question_and_owner(bool nullTitle, bool nullQuestion, bool blankOwner)
    {
        var act = () => ResearchMission.Propose("RMS-2026-001",
            nullTitle ? null! : L(), nullQuestion ? null! : L(),
            blankOwner ? " " : "kc-owner", "Owner", null, null, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    // ── Update draft ─────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void UpdateDraft_replaces_fields_while_proposed()
    {
        var m = Proposed();
        var topic = Guid.NewGuid();
        m.UpdateDraft(L("New title"), L("New question"), "ref-2", topic);

        m.Title.En.Should().Be("New title");
        m.Question.En.Should().Be("New question");
        m.KeystonePackageRef.Should().Be("ref-2");
        m.SourceTopicId.Should().Be(topic);
    }

    [Fact]
    public void UpdateDraft_requires_title_and_question()
    {
        var m = Proposed();
        m.Invoking(x => x.UpdateDraft(null!, L(), null, null)).Should().Throw<InvalidOperationException>();
        m.Invoking(x => x.UpdateDraft(L(), null!, null, null)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateDraft_is_rejected_once_active()
    {
        var m = Active();
        m.Invoking(x => x.UpdateDraft(L(), L(), null, null)).Should().Throw<InvalidOperationException>();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Activate_moves_proposed_to_active()
    {
        var m = Proposed();
        m.Activate(Now);
        m.Status.Should().Be(ResearchMissionStatus.Active);
        m.DomainEvents.OfType<ResearchActivatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Activate_is_rejected_when_not_proposed()
    {
        var m = Active();
        m.Invoking(x => x.Activate(Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_moves_active_to_completed_and_stamps_the_time()
    {
        var m = Active();
        m.Complete(Now);
        m.Status.Should().Be(ResearchMissionStatus.Completed);
        m.CompletedAt.Should().Be(Now);
        m.DomainEvents.OfType<ResearchCompletedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Complete_is_rejected_when_not_active()
    {
        var m = Proposed();
        m.Invoking(x => x.Complete(Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_from_proposed_records_the_reason()
    {
        var m = Proposed();
        m.Cancel(L("no budget"), Now);
        m.Status.Should().Be(ResearchMissionStatus.Cancelled);
        m.CancellationReason!.En.Should().Be("no budget");
        m.DomainEvents.OfType<ResearchCancelledEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Cancel_from_active_is_allowed()
    {
        var m = Active();
        m.Cancel(L("superseded"), Now);
        m.Status.Should().Be(ResearchMissionStatus.Cancelled);
    }

    [Fact]
    public void Cancel_requires_a_reason()
    {
        var m = Proposed();
        m.Invoking(x => x.Cancel(null!, Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_is_rejected_once_terminal()
    {
        var m = Active();
        m.Complete(Now);
        m.Invoking(x => x.Cancel(L(), Now)).Should().Throw<InvalidOperationException>();
    }

    // ── Findings ─────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void AddFinding_assigns_per_mission_ordinal_keys_while_active()
    {
        var m = Active();
        var f1 = m.AddFinding(L("first"), L("detail"), Confidence.High);
        var f2 = m.AddFinding(L("second"), null, Confidence.Low);

        f1.Key.Should().Be("FND-001");
        f2.Key.Should().Be("FND-002");
        f1.Confidence.Should().Be(Confidence.High);
        f1.IsVerified.Should().BeFalse();
        m.Findings.Should().HaveCount(2);
    }

    [Fact]
    public void AddFinding_is_rejected_while_proposed()
    {
        var m = Proposed();
        m.Invoking(x => x.AddFinding(L(), null, Confidence.Low)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddFinding_rejects_an_invalid_confidence_and_a_null_summary()
    {
        var m = Active();
        m.Invoking(x => x.AddFinding(L(), null, (Confidence)99)).Should().Throw<InvalidOperationException>();
        m.Invoking(x => x.AddFinding(null!, null, Confidence.Low)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateFinding_replaces_content()
    {
        var m = Active();
        var f = m.AddFinding(L("old"), null, Confidence.Low);
        m.UpdateFinding(f.PublicId, L("new"), L("more"), Confidence.Medium);

        f.Summary.En.Should().Be("new");
        f.Detail!.En.Should().Be("more");
        f.Confidence.Should().Be(Confidence.Medium);
    }

    [Fact]
    public void UpdateFinding_guards_null_summary_and_invalid_confidence()
    {
        var m = Active();
        var f = m.AddFinding(L("s"), null, Confidence.Low);
        m.Invoking(x => x.UpdateFinding(f.PublicId, null!, null, Confidence.Low)).Should().Throw<InvalidOperationException>();
        m.Invoking(x => x.UpdateFinding(f.PublicId, L(), null, (Confidence)99)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateFinding_on_an_unknown_id_throws_key_not_found()
    {
        var m = Active();
        m.Invoking(x => x.UpdateFinding(Guid.NewGuid(), L(), null, Confidence.Low)).Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void VerifyFinding_flips_the_flag_and_is_idempotent()
    {
        var m = Active();
        var f = m.AddFinding(L("s"), null, Confidence.Low);
        m.VerifyFinding(f.PublicId);
        m.VerifyFinding(f.PublicId);
        f.IsVerified.Should().BeTrue();
    }

    [Fact]
    public void VerifyFinding_on_an_unknown_id_throws_key_not_found()
    {
        var m = Active();
        m.Invoking(x => x.VerifyFinding(Guid.NewGuid())).Should().Throw<KeyNotFoundException>();
    }

    // ── Recommendations ──────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void AddRecommendation_assigns_ordinal_keys_and_starts_proposed()
    {
        var m = Active();
        var r1 = m.AddRecommendation(L("do x"), L("because"), RecommendationPriority.High, Guid.NewGuid());
        var r2 = m.AddRecommendation(L("do y"), null, RecommendationPriority.Low, null);

        r1.Key.Should().Be("REC-001");
        r2.Key.Should().Be("REC-002");
        r1.Status.Should().Be(RecommendationStatus.Proposed);
        r1.LinkedTopicId.Should().NotBeNull();
        m.Recommendations.Should().HaveCount(2);
    }

    [Fact]
    public void AddRecommendation_is_rejected_while_proposed_and_guards_input()
    {
        Proposed().Invoking(x => x.AddRecommendation(L(), null, RecommendationPriority.Low, null))
            .Should().Throw<InvalidOperationException>();
        Active().Invoking(x => x.AddRecommendation(null!, null, RecommendationPriority.Low, null))
            .Should().Throw<InvalidOperationException>();
        Active().Invoking(x => x.AddRecommendation(L(), null, (RecommendationPriority)99, null))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateRecommendation_replaces_content()
    {
        var m = Active();
        var r = m.AddRecommendation(L("old"), null, RecommendationPriority.Low, null);
        var topic = Guid.NewGuid();
        m.UpdateRecommendation(r.PublicId, L("new"), L("why"), RecommendationPriority.High, topic);

        r.Statement.En.Should().Be("new");
        r.Rationale!.En.Should().Be("why");
        r.Priority.Should().Be(RecommendationPriority.High);
        r.LinkedTopicId.Should().Be(topic);
    }

    [Fact]
    public void UpdateRecommendation_guards_input_and_unknown_id()
    {
        var m = Active();
        var r = m.AddRecommendation(L("s"), null, RecommendationPriority.Low, null);
        m.Invoking(x => x.UpdateRecommendation(r.PublicId, null!, null, RecommendationPriority.Low, null)).Should().Throw<InvalidOperationException>();
        m.Invoking(x => x.UpdateRecommendation(r.PublicId, L(), null, (RecommendationPriority)99, null)).Should().Throw<InvalidOperationException>();
        m.Invoking(x => x.UpdateRecommendation(Guid.NewGuid(), L(), null, RecommendationPriority.Low, null)).Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void SetRecommendationStatus_accepts_then_freezes()
    {
        var m = Active();
        var r = m.AddRecommendation(L("s"), null, RecommendationPriority.Low, null);
        m.SetRecommendationStatus(r.PublicId, RecommendationStatus.Accepted);
        r.Status.Should().Be(RecommendationStatus.Accepted);

        // A decided recommendation does not flip.
        m.Invoking(x => x.SetRecommendationStatus(r.PublicId, RecommendationStatus.Rejected)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetRecommendationStatus_rejects_a_non_terminal_target_and_unknown_id()
    {
        var m = Active();
        var r = m.AddRecommendation(L("s"), null, RecommendationPriority.Low, null);
        m.Invoking(x => x.SetRecommendationStatus(r.PublicId, RecommendationStatus.Proposed)).Should().Throw<InvalidOperationException>();
        m.Invoking(x => x.SetRecommendationStatus(Guid.NewGuid(), RecommendationStatus.Accepted)).Should().Throw<KeyNotFoundException>();
    }

    // ── Terminal immutability ────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Child_mutations_are_rejected_once_the_mission_is_completed()
    {
        var m = Active();
        var f = m.AddFinding(L("s"), null, Confidence.Low);
        var r = m.AddRecommendation(L("s"), null, RecommendationPriority.Low, null);
        m.Complete(Now);

        m.Invoking(x => x.AddFinding(L(), null, Confidence.Low)).Should().Throw<InvalidOperationException>();
        m.Invoking(x => x.UpdateFinding(f.PublicId, L(), null, Confidence.Low)).Should().Throw<InvalidOperationException>();
        m.Invoking(x => x.VerifyFinding(f.PublicId)).Should().Throw<InvalidOperationException>();
        m.Invoking(x => x.AddRecommendation(L(), null, RecommendationPriority.Low, null)).Should().Throw<InvalidOperationException>();
        m.Invoking(x => x.UpdateRecommendation(r.PublicId, L(), null, RecommendationPriority.Low, null)).Should().Throw<InvalidOperationException>();
        m.Invoking(x => x.SetRecommendationStatus(r.PublicId, RecommendationStatus.Accepted)).Should().Throw<InvalidOperationException>();
    }
}
