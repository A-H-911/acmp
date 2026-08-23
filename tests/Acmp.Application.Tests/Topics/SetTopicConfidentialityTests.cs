using Acmp.Modules.Topics.Application.Features.SetTopicConfidentiality;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// FR-163 / C-AUTHZ-04 (DEC-063 d2) — classify and declassify.
//
// The role gate is asserted on the COMMAND rather than through the handler, because AllowedRoles is
// what AuthorizationBehavior enforces in the real pipeline; a handler test cannot see it, and a
// handler test that "proved" authorization would be measuring nothing.
public class SetTopicConfidentialityTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);

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
        "TOP-2026-060", "Adopt Keycloak", "Consolidate IAM.", "Fragmented auth is risky.",
        TopicType.ArchitectureDecision, TopicUrgency.Normal, TopicSource.SecurityFinding,
        "kc-omar", "Omar H.", new[] { "identity" }, Array.Empty<string>(), Array.Empty<string>());

    private static async Task<TopicsDbContext> Seeded(Topic topic, ICurrentUser user, IClock clock)
    {
        var db = new TopicsDbContext(
            new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("conf-" + Guid.NewGuid()).Options,
            clock, user);
        topic.Submit(Now);
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public void Only_the_chairman_and_secretary_may_classify()
    {
        // DEC-063 d2. The Owner is deliberately ABSENT: letting them classify would let a plain Member
        // hide a topic from the committee, which cuts against the read-visible/write-scoped default
        // that Restricted is a narrow carve-out FROM.
        new SetTopicConfidentialityCommand(Guid.NewGuid(), true).AllowedRoles
            .Should().BeEquivalentTo(new[] { AcmpRoles.Chairman, AcmpRoles.Secretary });
    }

    [Fact]
    public async Task Classifying_sets_the_flag_and_audits_it_under_its_own_action()
    {
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        var topic = NewTopic();
        await using var db = await Seeded(topic, user, clock);

        await new SetTopicConfidentialityHandler(db, user, clock, audit)
            .Handle(new SetTopicConfidentialityCommand(topic.PublicId, true), CancellationToken.None);

        (await db.Topics.SingleAsync()).IsRestricted.Should().BeTrue();
        // A distinct verb, not a flag in a payload: "who restricted this topic" should be answerable by
        // filtering the audit action rather than by parsing a diff.
        await audit.Received(1).EmitEnrichedAsync(
            "Topics.TopicRestricted", nameof(Topic), topic.PublicId.ToString(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Declassifying_clears_the_flag_and_audits_the_other_action()
    {
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        var topic = NewTopic();
        topic.Restrict("kc-sec", "Sara S.", Now);
        await using var db = await Seeded(topic, user, clock);

        await new SetTopicConfidentialityHandler(db, user, clock, audit)
            .Handle(new SetTopicConfidentialityCommand(topic.PublicId, false), CancellationToken.None);

        (await db.Topics.SingleAsync()).IsRestricted.Should().BeFalse();
        await audit.Received(1).EmitEnrichedAsync(
            "Topics.TopicDeclassified", nameof(Topic), topic.PublicId.ToString(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_no_op_classification_writes_no_audit_row()
    {
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        var topic = NewTopic();
        topic.Restrict("kc-sec", "Sara S.", Now);
        await using var db = await Seeded(topic, user, clock);

        await new SetTopicConfidentialityHandler(db, user, clock, audit)
            .Handle(new SetTopicConfidentialityCommand(topic.PublicId, true), CancellationToken.None);

        // A classification change that did not happen must not appear in the audit trail as though it
        // did. The endpoint is a PUT of the desired state, so repeats are expected rather than rare.
        await audit.DidNotReceiveWithAnyArgs().EmitEnrichedAsync(default!, default!, default!, default, default);
        (await db.Topics.SingleAsync()).IsRestricted.Should().BeTrue();
    }

    [Fact]
    public async Task A_terminal_topic_can_still_be_declassified_through_the_handler()
    {
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        var topic = NewTopic();
        topic.Restrict("kc-sec", "Sara S.", Now);
        await using var db = await Seeded(topic, user, clock);
        var stored = await db.Topics.SingleAsync();
        stored.BeginTriage("kc-sec", "Sara S.", Now);
        stored.Accept(Guid.NewGuid(), "Owner O.", "kc-sec", "Sara S.", Now);
        stored.MarkPrepared("kc-sec", "Sara S.", Now);
        stored.Schedule(Guid.NewGuid(), "kc-sec", "Sara S.", Now);
        stored.EnterCommittee("kc-sec", "Sara S.", Now);
        stored.Decide("kc-sec", "Sara S.", Now);
        await db.SaveChangesAsync();

        await new SetTopicConfidentialityHandler(db, user, clock, audit)
            .Handle(new SetTopicConfidentialityCommand(topic.PublicId, false), CancellationToken.None);

        // The EnsureMutable exemption, asserted through the real handler and not only on the aggregate:
        // an archived sensitive topic must not be permanently undeclassifiable.
        (await db.Topics.SingleAsync()).IsRestricted.Should().BeFalse();
    }

    [Fact]
    public async Task An_unknown_topic_throws_not_found()
    {
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        await using var db = await Seeded(NewTopic(), user, clock);

        var act = () => new SetTopicConfidentialityHandler(db, user, clock, audit)
            .Handle(new SetTopicConfidentialityCommand(Guid.NewGuid(), true), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void The_validator_rejects_an_empty_topic_id()
    {
        new SetTopicConfidentialityValidator()
            .Validate(new SetTopicConfidentialityCommand(Guid.Empty, true))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void The_validator_accepts_a_well_formed_command()
    {
        new SetTopicConfidentialityValidator()
            .Validate(new SetTopicConfidentialityCommand(Guid.NewGuid(), true))
            .IsValid.Should().BeTrue();
    }
}
