using Acmp.Modules.Risks.Domain;
using Acmp.Modules.Risks.Domain.Enums;
using Acmp.Modules.Risks.Domain.Events;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Domain.Tests.Risks;

// The Risk aggregate state machine (docs/12 §10, W15): raise → Open → Mitigating → Closed; side Accepted
// (terminal) and Escalated (transient, returns to Mitigating/Closed). Plus the derived exposure scale and
// the owned Mitigation lifecycle. Pure domain — no EF, no MediatR.
public class RiskTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly LocalizedString Title = LocalizedString.Create("Auth migration risk", "خطر ترحيل الهوية");
    private static readonly LocalizedString Plan = LocalizedString.Create("Dual-run then cut over", "تشغيل مزدوج ثم التحويل");

    private static Risk Raise(RiskLevel likelihood = RiskLevel.Medium, RiskLevel impact = RiskLevel.High) =>
        Risk.Create("RSK-2026-001", Title, null, likelihood, impact, "kc-owner", "Owner",
            RiskSubjectType.Topic, Guid.NewGuid(), "TOP-2026-014", Now);

    private static Risk Mitigating()
    {
        var r = Raise();
        r.AddMitigation(Plan, MitigationType.Reduce, null, null, null);
        r.BeginMitigation(Now);
        return r;
    }

    [Fact]
    public void Create_opens_the_risk_sets_the_key_and_raises_an_event()
    {
        var r = Raise();
        r.Status.Should().Be(RiskStatus.Open);
        r.Key.Should().Be("RSK-2026-001");
        r.OwnerName.Should().Be("Owner");
        r.SubjectKey.Should().Be("TOP-2026-014");
        r.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RiskRaisedEvent>();
    }

    [Theory]
    [InlineData("", "Owner", "OWNER")]                 // owner id required
    public void Create_guards_required_fields(string owner, string ownerName, string _)
    {
        var act = () => Risk.Create("RSK-2026-001", Title, null, RiskLevel.Low, RiskLevel.Low, owner, ownerName,
            RiskSubjectType.Topic, Guid.NewGuid(), null, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_guards_title_subject_and_levels()
    {
        FluentActions.Invoking(() => Risk.Create("k", null!, null, RiskLevel.Low, RiskLevel.Low, "o", "O", RiskSubjectType.Topic, Guid.NewGuid(), null, Now))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Risk.Create("k", Title, null, RiskLevel.Low, RiskLevel.Low, "o", "O", RiskSubjectType.Topic, Guid.Empty, null, Now))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Risk.Create("k", Title, null, (RiskLevel)0, RiskLevel.Low, "o", "O", RiskSubjectType.Topic, Guid.NewGuid(), null, Now))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Risk.Create("k", Title, null, RiskLevel.Low, (RiskLevel)9, "o", "O", RiskSubjectType.Topic, Guid.NewGuid(), null, Now))
            .Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(RiskLevel.Low, RiskLevel.Low, 1, RiskExposure.Low)]
    [InlineData(RiskLevel.Low, RiskLevel.Medium, 2, RiskExposure.Low)]
    [InlineData(RiskLevel.Low, RiskLevel.High, 3, RiskExposure.Medium)]
    [InlineData(RiskLevel.Medium, RiskLevel.Medium, 4, RiskExposure.Medium)]
    [InlineData(RiskLevel.Medium, RiskLevel.High, 6, RiskExposure.High)]
    [InlineData(RiskLevel.High, RiskLevel.Medium, 6, RiskExposure.High)]
    [InlineData(RiskLevel.High, RiskLevel.High, 9, RiskExposure.Critical)]
    public void Severity_and_exposure_are_derived_from_likelihood_times_impact(
        RiskLevel likelihood, RiskLevel impact, int severity, RiskExposure exposure)
    {
        var r = Raise(likelihood, impact);
        r.Severity().Should().Be(severity);
        r.Exposure().Should().Be(exposure);
        RiskExposureScale.Severity(likelihood, impact).Should().Be(severity);
        RiskExposureScale.Band(likelihood, impact).Should().Be(exposure);
    }

    [Fact]
    public void AddMitigation_adds_a_planned_mitigation_and_guards_type_and_terminal_state()
    {
        var r = Raise();
        var m = r.AddMitigation(Plan, MitigationType.Reduce, "kc-m", Guid.NewGuid(), Now.AddDays(7));
        r.Mitigations.Should().ContainSingle();
        m.Status.Should().Be(MitigationStatus.Planned);
        m.OwnerUserId.Should().Be("kc-m");

        FluentActions.Invoking(() => r.AddMitigation(Plan, (MitigationType)0, null, null, null))
            .Should().Throw<InvalidOperationException>();

        r.Accept(Plan, "Chairman", Now);
        FluentActions.Invoking(() => r.AddMitigation(Plan, MitigationType.Reduce, null, null, null))
            .Should().Throw<InvalidOperationException>("a terminal risk takes no new mitigation");
    }

    [Fact]
    public void Mitigation_status_is_forward_only()
    {
        var r = Raise();
        var m = r.AddMitigation(Plan, MitigationType.Reduce, null, null, null);
        r.SetMitigationStatus(m.PublicId, MitigationStatus.InProgress);
        m.Status.Should().Be(MitigationStatus.InProgress);
        r.SetMitigationStatus(m.PublicId, MitigationStatus.Done);
        m.IsDone.Should().BeTrue();

        FluentActions.Invoking(() => r.SetMitigationStatus(m.PublicId, MitigationStatus.Planned))
            .Should().Throw<InvalidOperationException>("a mitigation never regresses");
        FluentActions.Invoking(() => r.SetMitigationStatus(Guid.NewGuid(), MitigationStatus.Done))
            .Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void BeginMitigation_requires_a_mitigation_and_moves_open_to_mitigating()
    {
        var bare = Raise();
        FluentActions.Invoking(() => bare.BeginMitigation(Now))
            .Should().Throw<InvalidOperationException>("no mitigation planned yet");

        var r = Mitigating();
        r.Status.Should().Be(RiskStatus.Mitigating);
        r.DomainEvents.Should().ContainItemsAssignableTo<RiskMitigatingEvent>();
    }

    [Fact]
    public void Close_needs_a_note_or_all_mitigations_done()
    {
        var r = Mitigating();
        FluentActions.Invoking(() => r.Close(null, Now))
            .Should().Throw<InvalidOperationException>("mitigations not done and no note");

        r.Close(LocalizedString.Create("no longer applicable", "لم يعد ساريًا"), Now);
        r.Status.Should().Be(RiskStatus.Closed);
        r.ClosedAt.Should().Be(Now);
        r.ClosureNote.Should().NotBeNull();
    }

    [Fact]
    public void Close_is_allowed_when_all_mitigations_are_done_without_a_note()
    {
        var r = Raise();
        var m = r.AddMitigation(Plan, MitigationType.Reduce, null, null, null);
        r.BeginMitigation(Now);
        r.SetMitigationStatus(m.PublicId, MitigationStatus.InProgress);
        r.SetMitigationStatus(m.PublicId, MitigationStatus.Done);
        r.Close(null, Now);
        r.Status.Should().Be(RiskStatus.Closed);
    }

    [Fact]
    public void Accept_records_rationale_and_authority_and_is_terminal()
    {
        var r = Raise();
        r.Accept(Plan, "Chairman", Now);
        r.Status.Should().Be(RiskStatus.Accepted);
        r.AcceptingAuthority.Should().Be("Chairman");
        r.AcceptanceRationale.Should().NotBeNull();
        r.ClosedAt.Should().Be(Now);

        FluentActions.Invoking(() => r.Escalate(Plan, "Board", Now))
            .Should().Throw<InvalidOperationException>("Accepted is terminal");
    }

    [Fact]
    public void Accept_guards_rationale_and_authority()
    {
        FluentActions.Invoking(() => Raise().Accept(null!, "Chairman", Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Raise().Accept(Plan, " ", Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Escalate_records_reason_and_target_then_returns_to_mitigating_or_closed()
    {
        var r = Mitigating();
        r.Escalate(Plan, "Steering Board", Now);
        r.Status.Should().Be(RiskStatus.Escalated);
        r.EscalationTarget.Should().Be("Steering Board");
        r.EscalationReason.Should().NotBeNull();
        r.DomainEvents.OfType<RiskEscalatedEvent>().Single().Target.Should().Be("Steering Board");

        // Escalated is transient: resume mitigating (needs the existing mitigation), then close.
        r.BeginMitigation(Now);
        r.Status.Should().Be(RiskStatus.Mitigating);
        r.Escalate(Plan, "Board", Now);
        r.Close(LocalizedString.Create("handled", "تمت المعالجة"), Now);
        r.Status.Should().Be(RiskStatus.Closed);
    }

    [Fact]
    public void Escalate_guards_reason_and_target_and_state()
    {
        FluentActions.Invoking(() => Raise().Escalate(null!, "Board", Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Raise().Escalate(Plan, "", Now)).Should().Throw<InvalidOperationException>();

        var closed = Mitigating();
        closed.Close(LocalizedString.Create("done", "تم"), Now);
        FluentActions.Invoking(() => closed.Escalate(Plan, "Board", Now))
            .Should().Throw<InvalidOperationException>("Closed is terminal");
    }

    [Fact]
    public void Mitigation_create_guards_description()
    {
        FluentActions.Invoking(() => Raise().AddMitigation(null!, MitigationType.Reduce, null, null, null))
            .Should().Throw<InvalidOperationException>();
    }
}
