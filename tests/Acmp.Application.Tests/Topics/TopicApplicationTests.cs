using Acmp.Modules.Topics.Application.Features.DeferTopic;
using Acmp.Modules.Topics.Application.Features.PrepareTopic;
using Acmp.Modules.Topics.Application.Features.PrioritizeTopic;
using Acmp.Modules.Topics.Application.Features.RejectTopic;
using Acmp.Modules.Topics.Application.Features.SubmitTopic;
using Acmp.Modules.Topics.Application.Features.UpdateTopic;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Contracts.Membership;
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
        new[] { "core" }, Array.Empty<string>(), Array.Empty<string>());

    // The seeded taxonomy (ADR-0042 step 1). Stated here independently of the migration: these tests
    // are about the RULE, and a fake that fetched the real list would make them pass for the wrong
    // reason on a database that had not been seeded.
    private static readonly string[] Seeded =
        { "core", "communications", "smart-cities", "government", "shared-services" };

    private static SubmitTopicValidator SubmitValidator() => new(new FakeStreamCatalog(Seeded));
    private static UpdateTopicValidator UpdateValidator() => new(new FakeStreamCatalog(Seeded));

    // ⚠ Returns the ASSIGNABLE codes only - the real StreamCatalog filters the wildcard out at the
    // source (ADR-0042 clause 4), so a fake that included it would test a contract nobody implements.
    private sealed class FakeStreamCatalog : IStreamCatalog
    {
        private readonly IReadOnlyCollection<string> _codes;
        public FakeStreamCatalog(IReadOnlyCollection<string> codes) => _codes = codes;
        public Task<IReadOnlyCollection<string>> GetAssignableStreamCodesAsync(CancellationToken ct = default) =>
            Task.FromResult(_codes);
    }

    // ---- AC-030: required-field validation on submit ----

    [Fact]
    public async Task Submit_is_valid_with_all_required_fields()
    {
        (await SubmitValidator().ValidateAsync(ValidSubmit())).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Submit_requires_a_title(string title)
    {
        var result = await SubmitValidator().ValidateAsync(ValidSubmit() with { Title = title });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitTopicCommand.Title));
    }

    [Fact]
    public async Task Submit_requires_description_justification_and_a_stream()
    {
        var v = SubmitValidator();
        (await v.ValidateAsync(ValidSubmit() with { Description = "" })).IsValid.Should().BeFalse();
        (await v.ValidateAsync(ValidSubmit() with { Justification = "" })).IsValid.Should().BeFalse();
        (await v.ValidateAsync(ValidSubmit() with { Streams = Array.Empty<string>() })).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Submit_rejects_an_overlong_title()
    {
        var result = await SubmitValidator().ValidateAsync(ValidSubmit() with { Title = new string('x', 121) });
        result.IsValid.Should().BeFalse();
    }

    // ---- ADR-0042 clause (7): affected streams come from the seeded taxonomy, never free text ----

    [Fact] // the whole point: "Platform" is what topics carried before the taxonomy existed
    public async Task Submit_rejects_a_stream_outside_the_taxonomy()
    {
        var result = await SubmitValidator().ValidateAsync(ValidSubmit() with { Streams = new[] { "Platform" } });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitTopicCommand.Streams));
    }

    // ⚠ One bad code among good ones must still fail. An "any match" rule would let free text ride
    // along beside a real stream, and the topic would then carry a value ABAC can never resolve.
    [Fact]
    public async Task Submit_rejects_a_mixed_list_containing_an_unknown_stream()
    {
        var mixed = ValidSubmit() with { Streams = new[] { "core", "Platform" } };

        (await SubmitValidator().ValidateAsync(mixed)).IsValid.Should().BeFalse();
    }

    // ⚠ THE WILDCARD IS MEMBER-SIDE ONLY (ADR-0042 clause 4). It says a PERSON is unrestricted; a
    // topic claiming it would assert universal scope. Enforced by the catalog excluding it, so this
    // fails for the same reason any unknown code does - which is exactly the intended design.
    [Fact]
    public async Task Submit_rejects_the_wildcard_stream()
    {
        var wildcard = ValidSubmit() with { Streams = new[] { "all-streams" } };

        (await SubmitValidator().ValidateAsync(wildcard)).IsValid.Should().BeFalse();
    }

    // Case-insensitive on purpose: StreamScopeHandler intersects with OrdinalIgnoreCase, so "Core"
    // authorizes identically to "core". Validating more strictly than the control that consumes the
    // value would refuse input that would have worked.
    [Fact]
    public async Task Submit_accepts_a_seeded_stream_in_any_case()
    {
        var mixedCase = ValidSubmit() with { Streams = new[] { "Core", "SMART-CITIES" } };

        (await SubmitValidator().ValidateAsync(mixedCase)).IsValid.Should().BeTrue();
    }

    // ⚠ The rule must hold on UPDATE too. Submit-only enforcement would leave PUT /api/topics/{id}
    // writing free text through Topic.AssignStreams to the identical field (DEF-059's root cause).
    [Fact]
    public async Task Update_rejects_a_stream_outside_the_taxonomy()
    {
        var cmd = new UpdateTopicCommand(Guid.NewGuid(), "T", "D", "J", TopicUrgency.Normal,
            new[] { "Platform" }, Array.Empty<string>(), Array.Empty<string>());

        (await UpdateValidator().ValidateAsync(cmd)).IsValid.Should().BeFalse();
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

    // ---- AC-043: backlog prioritization ordinal must be a non-negative, identified target ----

    [Fact]
    public void Prepare_requires_a_topic_id()
    {
        new PrepareTopicValidator().Validate(new PrepareTopicCommand(Guid.Empty)).IsValid.Should().BeFalse();
        new PrepareTopicValidator().Validate(new PrepareTopicCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Prioritize_requires_a_topic_id()
    {
        new PrioritizeTopicValidator().Validate(new PrioritizeTopicCommand(Guid.Empty, 3)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Prioritize_rejects_a_negative_ordinal()
    {
        new PrioritizeTopicValidator().Validate(new PrioritizeTopicCommand(Guid.NewGuid(), -1)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Prioritize_accepts_a_zero_or_positive_ordinal_for_a_real_topic()
    {
        var v = new PrioritizeTopicValidator();
        v.Validate(new PrioritizeTopicCommand(Guid.NewGuid(), 0)).IsValid.Should().BeTrue();
        v.Validate(new PrioritizeTopicCommand(Guid.NewGuid(), 9)).IsValid.Should().BeTrue();
    }

    // ---- AC-034: edit command must identify the topic and carry a valid urgency ----

    [Fact]
    public async Task Update_requires_a_topic_id()
    {
        var cmd = new UpdateTopicCommand(Guid.Empty, "T", "D", "J", TopicUrgency.Normal,
            new[] { "core" }, Array.Empty<string>(), Array.Empty<string>());
        (await UpdateValidator().ValidateAsync(cmd)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Update_rejects_an_out_of_range_urgency()
    {
        var cmd = new UpdateTopicCommand(Guid.NewGuid(), "T", "D", "J", (TopicUrgency)999,
            new[] { "core" }, Array.Empty<string>(), Array.Empty<string>());
        (await UpdateValidator().ValidateAsync(cmd)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Update_is_valid_with_an_identified_topic_and_known_urgency()
    {
        var cmd = new UpdateTopicCommand(Guid.NewGuid(), "T", "D", "J", TopicUrgency.Critical,
            new[] { "core" }, Array.Empty<string>(), Array.Empty<string>());
        (await UpdateValidator().ValidateAsync(cmd)).IsValid.Should().BeTrue();
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
