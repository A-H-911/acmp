using Acmp.Modules.Topics.Application.Features.ReclassifyTopic;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// FR-164 / DW-032 (DEC-070) — the Secretary's triage-time correction of a topic's type and source.
//
// The role gate is asserted on the COMMAND rather than through the handler, for the same reason
// SetTopicConfidentialityTests states: AllowedRoles is what AuthorizationBehavior enforces in the real
// pipeline, so a handler test that "proved" authorization would be measuring nothing.
public class ReclassifyTopicTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);

    private static ICurrentUser User()
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns("kc-secretary");
        u.DisplayName.Returns("Sara S.");
        return u;
    }

    private static IClock Clock()
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(Now);
        return c;
    }

    private static Topic NewTopic() => Topic.Draft(
        "TOP-2026-061", "Adopt Keycloak", "Consolidate IAM.", "Fragmented auth is risky.",
        TopicType.ArchitectureDecision, TopicUrgency.Normal, TopicSource.CommitteeMember,
        "kc-omar", "Omar H.", new[] { "identity" }, Array.Empty<string>(), Array.Empty<string>());

    private static async Task<TopicsDbContext> Seeded(Topic topic, ICurrentUser user, IClock clock)
    {
        var db = new TopicsDbContext(
            new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("recl-" + Guid.NewGuid()).Options,
            clock, user);
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public void Only_the_chairman_and_secretary_may_reclassify()
    {
        // The OWNER is deliberately absent, and so is the submitter. Classification drives the required
        // template, the triage workflow and the SLA thresholds, so letting the submitter set it after the
        // fact would make it self-service — which is exactly why this is not part of UpdateTopic.
        new ReclassifyTopicCommand(Guid.NewGuid(), TopicType.ResearchDiscovery, TopicSource.Modernization)
            .AllowedRoles.Should().BeEquivalentTo(new[] { AcmpRoles.Chairman, AcmpRoles.Secretary });
    }

    [Fact]
    public async Task Reclassifying_a_pre_accept_topic_changes_both_fields_and_audits_it()
    {
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        var topic = NewTopic();
        topic.Submit(Now);
        await using var db = await Seeded(topic, user, clock);

        await new ReclassifyTopicHandler(db, audit).Handle(
            new ReclassifyTopicCommand(topic.PublicId, TopicType.ResearchDiscovery, TopicSource.Modernization),
            CancellationToken.None);

        var saved = await db.Topics.FirstAsync(t => t.PublicId == topic.PublicId);
        saved.Type.Should().Be(TopicType.ResearchDiscovery);
        saved.Source.Should().Be(TopicSource.Modernization);
        await audit.Received(1).EmitEnrichedAsync(
            "Topics.TopicReclassified", nameof(Topic), topic.PublicId.ToString(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_reclassification_that_changes_nothing_writes_no_audit_row()
    {
        // A no-op must not appear in the audit trail as though it happened. The triage form submits both
        // fields whether or not either moved, so this is the common case, not an edge case.
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        var topic = NewTopic();
        topic.Submit(Now);
        await using var db = await Seeded(topic, user, clock);

        await new ReclassifyTopicHandler(db, audit).Handle(
            new ReclassifyTopicCommand(topic.PublicId, topic.Type, topic.Source), CancellationToken.None);

        await audit.DidNotReceiveWithAnyArgs().EmitEnrichedAsync(default!, default!, default!);
    }

    [Fact]
    public async Task A_no_op_on_a_topic_past_triage_is_accepted_rather_than_refused()
    {
        // The no-op check runs BEFORE the domain call on purpose. Without that ordering, re-submitting a
        // Decided topic's OWN classification would throw — a request that asks for nothing to change
        // cannot sensibly be a 409.
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        var topic = NewTopic();
        topic.Submit(Now);
        topic.BeginTriage("kc-secretary", "Sara S.", Now);
        topic.Accept(Guid.NewGuid(), "Owner O.", "kc-secretary", "Sara S.", Now);
        await using var db = await Seeded(topic, user, clock);

        var act = () => new ReclassifyTopicHandler(db, audit).Handle(
            new ReclassifyTopicCommand(topic.PublicId, topic.Type, topic.Source), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Reclassifying_after_acceptance_is_refused_by_the_aggregate()
    {
        // THE GUARD IS THE AGGREGATE'S, not the handler's. Forced here rather than asserted: the point of
        // the domain guard is that it holds for every caller, so the handler must not be the thing that
        // knows the rule.
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        var topic = NewTopic();
        topic.Submit(Now);
        topic.BeginTriage("kc-secretary", "Sara S.", Now);
        topic.Accept(Guid.NewGuid(), "Owner O.", "kc-secretary", "Sara S.", Now);
        await using var db = await Seeded(topic, user, clock);

        var act = () => new ReclassifyTopicHandler(db, audit).Handle(
            new ReclassifyTopicCommand(topic.PublicId, TopicType.ResearchDiscovery, TopicSource.Modernization),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await audit.DidNotReceiveWithAnyArgs().EmitEnrichedAsync(default!, default!, default!);
    }

    [Fact]
    public async Task An_unknown_topic_is_a_not_found_rather_than_a_silent_success()
    {
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        await using var db = await Seeded(NewTopic(), user, clock);

        var act = () => new ReclassifyTopicHandler(db, audit).Handle(
            new ReclassifyTopicCommand(Guid.NewGuid(), TopicType.ResearchDiscovery, TopicSource.Modernization),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void The_validator_refuses_an_undefined_enum_value()
    {
        // IsInEnum is not decoration: the body binds straight from JSON, so an out-of-range integer
        // would otherwise reach the aggregate and be persisted as a type that does not exist.
        var v = new ReclassifyTopicValidator();
        v.TestValidate(new ReclassifyTopicCommand(Guid.NewGuid(), (TopicType)99, TopicSource.CommitteeMember))
            .ShouldHaveValidationErrorFor(x => x.Type);
        v.TestValidate(new ReclassifyTopicCommand(Guid.NewGuid(), TopicType.ResearchDiscovery, (TopicSource)99))
            .ShouldHaveValidationErrorFor(x => x.Source);
        v.TestValidate(new ReclassifyTopicCommand(Guid.Empty, TopicType.ResearchDiscovery, TopicSource.Modernization))
            .ShouldHaveValidationErrorFor(x => x.TopicId);
    }
}
