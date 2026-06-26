using Acmp.Modules.Topics.Application.Features.DeferTopic;
using Acmp.Modules.Topics.Application.Features.RejectTopic;
using Acmp.Modules.Topics.Application.Features.SubmitTopic;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using FluentAssertions;

namespace Acmp.Application.Tests.Topics;

// Pure application-layer logic: input validation (AC-030/031) and backlog aging (AC-057). The
// DbContext-backed handler tests pair with the Topics infrastructure (next slice), as Membership does.
public class TopicApplicationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);

    private static SubmitTopicCommand ValidSubmit() => new(
        "Adopt Keycloak", "Consolidate IAM.", "Fragmented auth is risky.",
        TopicType.ArchitectureDecision, TopicUrgency.Urgent, TopicSource.CommitteeMember,
        new[] { "identity" }, Array.Empty<string>(), Array.Empty<string>());

    // ---- AC-030: required-field validation on submit ----

    [Fact]
    public void Submit_is_valid_with_all_required_fields()
    {
        new SubmitTopicValidator().Validate(ValidSubmit()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Submit_requires_a_title(string title)
    {
        var result = new SubmitTopicValidator().Validate(ValidSubmit() with { Title = title });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitTopicCommand.Title));
    }

    [Fact]
    public void Submit_requires_description_justification_and_a_stream()
    {
        var v = new SubmitTopicValidator();
        v.Validate(ValidSubmit() with { Description = "" }).IsValid.Should().BeFalse();
        v.Validate(ValidSubmit() with { Justification = "" }).IsValid.Should().BeFalse();
        v.Validate(ValidSubmit() with { Streams = Array.Empty<string>() }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Submit_rejects_an_overlong_title()
    {
        var result = new SubmitTopicValidator().Validate(ValidSubmit() with { Title = new string('x', 121) });
        result.IsValid.Should().BeFalse();
    }

    // ---- AC-031: reject/defer require a reason ----

    [Fact]
    public void Reject_requires_a_reason()
    {
        new RejectTopicValidator().Validate(new RejectTopicCommand(Guid.NewGuid(), "")).IsValid.Should().BeFalse();
        new RejectTopicValidator().Validate(new RejectTopicCommand(Guid.NewGuid(), "Duplicate")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Defer_requires_a_reason()
    {
        new DeferTopicValidator().Validate(new DeferTopicCommand(Guid.NewGuid(), "", null)).IsValid.Should().BeFalse();
        new DeferTopicValidator().Validate(new DeferTopicCommand(Guid.NewGuid(), "Awaiting budget", null)).IsValid.Should().BeTrue();
    }

    // ---- AC-057: SLA aging ----

    [Theory]
    [InlineData(TopicUrgency.Normal, 21)]
    [InlineData(TopicUrgency.Urgent, 7)]
    [InlineData(TopicUrgency.Critical, 3)]
    public void Sla_thresholds_match_the_taxonomy(TopicUrgency urgency, int days)
    {
        TopicAging.SlaThresholdDays(urgency).Should().Be(days);
    }

    [Fact]
    public void Critical_topic_in_triage_past_three_days_is_breaching()
    {
        var t = Topic.Draft("TOP-2026-030", "T", "D", "J", TopicType.GovernanceStandardization,
            TopicUrgency.Critical, TopicSource.SecurityFinding, "kc-x", "X", new[] { "platform" },
            Array.Empty<string>(), Array.Empty<string>());
        t.Submit(T0);
        t.BeginTriage("kc-sec", "Sec", T0);  // entered Triage at T0

        TopicAging.IsBreaching(t, T0.AddDays(2)).Should().BeFalse();  // within 3-day SLA
        TopicAging.IsBreaching(t, T0.AddDays(4)).Should().BeTrue();   // 4 days > 3-day SLA (AC-057)
    }

    [Fact]
    public void Decided_topic_does_not_age()
    {
        var t = Topic.Draft("TOP-2026-031", "T", "D", "J", TopicType.ArchitectureDecision,
            TopicUrgency.Critical, TopicSource.CommitteeMember, "kc-x", "X", new[] { "platform" },
            Array.Empty<string>(), Array.Empty<string>());
        t.Submit(T0);
        t.BeginTriage("kc-s", "S", T0);
        t.Accept(Guid.NewGuid(), "Owner", "kc-s", "S", T0);
        t.MarkPrepared("kc-s", "S", T0);
        t.Schedule(Guid.NewGuid(), "kc-s", "S", T0);
        t.EnterCommittee("kc-s", "S", T0);
        t.Decide("kc-s", "S", T0);

        TopicAging.IsBreaching(t, T0.AddDays(100)).Should().BeFalse();
    }
}
