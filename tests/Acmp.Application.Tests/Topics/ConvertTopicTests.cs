using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Features.ConvertTopic;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Traceability;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// FR-030 / AC-113 (SC-018, DEC-060) — convert a Decided topic to a different type.
//
// WHAT MAKES THIS DIFFERENT FROM THE OTHER LIFECYCLE EXITS: Close, Reactivate and Reopen move ONE
// aggregate. Conversion retires the original AND creates a successor AND links them, so the failure
// modes are about what travels between the two rather than about the transition alone. The carry-over
// assertions below are the point of this suite — the operator chose "everything carries over"
// (DEC-060 d4), and a carried comment that lost its author, or an attachment that duplicated its
// blob, would satisfy a naive "the successor exists" test while corrupting the record.
public class ConvertTopicTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Earlier = new(2026, 3, 2, 8, 30, 0, TimeSpan.Zero);

    private static TopicsDbContext NewDb(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("convert-" + Guid.NewGuid()).Options,
            clock, user);

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

    // Permits everything: this suite is about the conversion, not ABAC, which has its own suites.
    private static IResourceAuthorizer Authorizer() => Substitute.For<IResourceAuthorizer>();

    private static ITopicKeyGenerator Keys(string next = "TOP-2026-500")
    {
        var k = Substitute.For<ITopicKeyGenerator>();
        k.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(next);
        return k;
    }

    private static Topic NewTopic() => Topic.Draft(
        "TOP-2026-001", "Adopt Keycloak", "Consolidate IAM.", "Fragmented auth is risky.",
        TopicType.ResearchDiscovery, TopicUrgency.Urgent, TopicSource.SecurityFinding,
        "kc-omar", "Omar H.", new[] { "identity", "platform" }, new[] { "Auth Service" }, new[] { "iam" });

    private static Topic Decided()
    {
        var topic = NewTopic();
        topic.Submit(Now);
        topic.BeginTriage("kc-secretary", "Sara S.", Now);
        topic.Accept(Guid.NewGuid(), "Owner O.", "kc-secretary", "Sara S.", Now);
        topic.MarkPrepared("kc-secretary", "Sara S.", Now);
        topic.Schedule(Guid.NewGuid(), "kc-secretary", "Sara S.", Now);
        topic.EnterCommittee("kc-secretary", "Sara S.", Now);
        topic.Decide("kc-secretary", "Sara S.", Now);
        return topic;
    }

    private static async Task<TopicsDbContext> Seeded(Topic topic, ICurrentUser user, IClock clock)
    {
        var db = NewDb(user, clock);
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        return db;
    }

    private static ConvertTopicHandler Handler(TopicsDbContext db, ICurrentUser user, IClock clock,
        IAuditSink audit, ITraceabilityWriter trace, ITopicKeyGenerator? keys = null) =>
        new(db, keys ?? Keys(), Authorizer(), user, clock, audit, trace);

    // ---- the transition itself ----

    [Fact]
    public async Task Convert_retires_the_original_and_creates_a_successor_of_the_target_type()
    {
        var (user, clock, audit, trace) = (User(), Clock(), Substitute.For<IAuditSink>(), Substitute.For<ITraceabilityWriter>());
        var topic = Decided();
        await using var db = await Seeded(topic, user, clock);

        var result = await Handler(db, user, clock, audit, trace)
            .Handle(new ConvertTopicCommand(topic.PublicId, TopicType.ArchitectureDecision, "research concluded"),
                CancellationToken.None);

        var original = await db.Topics.SingleAsync(t => t.PublicId == topic.PublicId);
        original.Status.Should().Be(TopicStatus.Converted);

        var successor = await db.Topics.SingleAsync(t => t.PublicId == result.Id);
        successor.Key.Should().Be("TOP-2026-500");
        successor.Type.Should().Be(TopicType.ArchitectureDecision);
        successor.Status.Should().Be(TopicStatus.Submitted, "the successor re-enters the pipeline for triage");
    }

    [Fact]
    public async Task Convert_records_the_reason_on_the_originals_history()
    {
        var (user, clock, audit, trace) = (User(), Clock(), Substitute.For<IAuditSink>(), Substitute.For<ITraceabilityWriter>());
        var topic = Decided();
        await using var db = await Seeded(topic, user, clock);

        await Handler(db, user, clock, audit, trace)
            .Handle(new ConvertTopicCommand(topic.PublicId, TopicType.ArchitectureDecision, "research concluded"),
                CancellationToken.None);

        var original = await db.Topics.Include(t => t.History).SingleAsync(t => t.PublicId == topic.PublicId);
        original.History.Should().Contain(h => h.ToStatus == TopicStatus.Converted && h.Reason == "research concluded");
    }

    [Fact]
    public async Task Convert_writes_the_typed_original_to_successor_edge()
    {
        var (user, clock, audit, trace) = (User(), Clock(), Substitute.For<IAuditSink>(), Substitute.For<ITraceabilityWriter>());
        var topic = Decided();
        await using var db = await Seeded(topic, user, clock);

        var result = await Handler(db, user, clock, audit, trace)
            .Handle(new ConvertTopicCommand(topic.PublicId, TopicType.ArchitectureDecision, "research concluded"),
                CancellationToken.None);

        // The DIRECTION is asserted, not merely that some edge was written: FR-030 asks for a link
        // between "the original and converted artifact", and a reversed edge would render the
        // successor as the origin in both traceability panels.
        await trace.Received(1).RecordEdgeAsync(
            "Topic", topic.PublicId, topic.Key, topic.Title,
            "Topic", result.Id, result.Key, Arg.Any<string>(),
            "ConvertedTo", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Convert_audits_against_the_original()
    {
        var (user, clock, audit, trace) = (User(), Clock(), Substitute.For<IAuditSink>(), Substitute.For<ITraceabilityWriter>());
        var topic = Decided();
        await using var db = await Seeded(topic, user, clock);

        await Handler(db, user, clock, audit, trace)
            .Handle(new ConvertTopicCommand(topic.PublicId, TopicType.ArchitectureDecision, "research concluded"),
                CancellationToken.None);

        await audit.Received(1).EmitEnrichedAsync(
            "Topics.TopicConverted", nameof(Topic), topic.PublicId.ToString(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- refusals: each proven by forcing it ----

    [Fact]
    public async Task Convert_is_refused_from_a_status_other_than_decided()
    {
        var (user, clock, audit, trace) = (User(), Clock(), Substitute.For<IAuditSink>(), Substitute.For<ITraceabilityWriter>());
        var topic = NewTopic();
        topic.Submit(Now); // Submitted, not Decided
        await using var db = await Seeded(topic, user, clock);

        var act = () => Handler(db, user, clock, audit, trace)
            .Handle(new ConvertTopicCommand(topic.PublicId, TopicType.ArchitectureDecision, "why not"),
                CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await db.Topics.CountAsync()).Should().Be(1, "a refused conversion must not leave an orphan successor");
        await trace.DidNotReceiveWithAnyArgs().RecordEdgeAsync(default!, default, default!, default!, default!, default, default!, default!, default!);
    }

    [Fact]
    public async Task Convert_to_the_type_it_already_is_is_refused()
    {
        var (user, clock, audit, trace) = (User(), Clock(), Substitute.For<IAuditSink>(), Substitute.For<ITraceabilityWriter>());
        var topic = Decided(); // ResearchDiscovery
        await using var db = await Seeded(topic, user, clock);

        var act = () => Handler(db, user, clock, audit, trace)
            .Handle(new ConvertTopicCommand(topic.PublicId, TopicType.ResearchDiscovery, "no-op"),
                CancellationToken.None);

        // Without this guard the call would retire a Decided topic and hand back a duplicate — a
        // destructive no-op, and irreversible because Converted is terminal.
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await db.Topics.SingleAsync()).Status.Should().Be(TopicStatus.Decided);
    }

    [Fact]
    public async Task Convert_of_an_unknown_topic_throws_not_found()
    {
        var (user, clock, audit, trace) = (User(), Clock(), Substitute.For<IAuditSink>(), Substitute.For<ITraceabilityWriter>());
        await using var db = NewDb(user, clock);

        var act = () => Handler(db, user, clock, audit, trace)
            .Handle(new ConvertTopicCommand(Guid.NewGuid(), TopicType.ArchitectureDecision, "why"),
                CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ---- DEC-060 d4: everything carries over ----

    [Fact]
    public async Task Convert_carries_comments_with_their_ORIGINAL_author_and_timestamp()
    {
        var (user, clock, audit, trace) = (User(), Clock(), Substitute.For<IAuditSink>(), Substitute.For<ITraceabilityWriter>());
        var topic = Decided();
        topic.AddComment("We must document a rollback path.", "kc-noura", "Noura P.", Earlier);
        await using var db = await Seeded(topic, user, clock);

        var result = await Handler(db, user, clock, audit, trace)
            .Handle(new ConvertTopicCommand(topic.PublicId, TopicType.ArchitectureDecision, "research concluded"),
                CancellationToken.None);

        var successor = await db.Topics.Include(t => t.Comments).SingleAsync(t => t.PublicId == result.Id);
        var carried = successor.Comments.Should().ContainSingle().Subject;
        carried.Body.Should().Be("We must document a rollback path.");
        // The whole point: re-attributing a carried comment to the converting Secretary, or stamping
        // it "now", would forge a record that reads as Noura's. AddComment takes both explicitly, so
        // nothing had to be invented to keep this honest.
        carried.AuthorSub.Should().Be("kc-noura");
        carried.AuthorName.Should().Be("Noura P.");
        carried.PostedAt.Should().Be(Earlier);
    }

    [Fact]
    public async Task Convert_carries_attachments_pointing_at_the_SAME_stored_object()
    {
        var (user, clock, audit, trace) = (User(), Clock(), Substitute.For<IAuditSink>(), Substitute.For<ITraceabilityWriter>());
        var topic = NewTopic();
        topic.AddAttachment("eval.pdf", "application/pdf", 1400, "topics/TOP-2026-001/eval.pdf",
            "kc-omar", "Omar H.", Earlier);
        topic.Submit(Now);
        topic.BeginTriage("kc-secretary", "Sara S.", Now);
        topic.Accept(Guid.NewGuid(), "Owner O.", "kc-secretary", "Sara S.", Now);
        topic.MarkPrepared("kc-secretary", "Sara S.", Now);
        topic.Schedule(Guid.NewGuid(), "kc-secretary", "Sara S.", Now);
        topic.EnterCommittee("kc-secretary", "Sara S.", Now);
        topic.Decide("kc-secretary", "Sara S.", Now);
        await using var db = await Seeded(topic, user, clock);

        var result = await Handler(db, user, clock, audit, trace)
            .Handle(new ConvertTopicCommand(topic.PublicId, TopicType.ArchitectureDecision, "research concluded"),
                CancellationToken.None);

        var successor = await db.Topics.Include(t => t.Attachments).SingleAsync(t => t.PublicId == result.Id);
        var carried = successor.Attachments.Should().ContainSingle().Subject;
        // Same StorageKey = same object in MinIO. Copying the blob would double storage for no gain,
        // since an attachment is immutable once uploaded.
        carried.StorageKey.Should().Be("topics/TOP-2026-001/eval.pdf");
        carried.FileName.Should().Be("eval.pdf");
        carried.UploadedBySub.Should().Be("kc-omar");
        carried.UploadedAt.Should().Be(Earlier);
    }

    // ---- validator ----
    //
    // The validator is the line a new feature most often leaves uncovered, and here it is also the
    // only thing standing between a caller and a 500: the domain's RequireReason throws
    // InvalidOperationException, which surfaces as a server error rather than a 400.

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_rejects_a_missing_reason(string reason)
    {
        var result = new ConvertTopicValidator()
            .Validate(new ConvertTopicCommand(Guid.NewGuid(), TopicType.ArchitectureDecision, reason));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ConvertTopicCommand.Reason));
    }

    [Fact]
    public void Validator_rejects_an_empty_topic_id_and_an_undefined_target_type()
    {
        var result = new ConvertTopicValidator()
            .Validate(new ConvertTopicCommand(Guid.Empty, (TopicType)99, "why"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ConvertTopicCommand.TopicId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ConvertTopicCommand.TargetType));
    }

    [Fact]
    public void Validator_rejects_a_reason_over_the_length_cap()
    {
        var result = new ConvertTopicValidator()
            .Validate(new ConvertTopicCommand(Guid.NewGuid(), TopicType.ArchitectureDecision, new string('x', 1001)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_accepts_a_well_formed_command()
    {
        var result = new ConvertTopicValidator()
            .Validate(new ConvertTopicCommand(Guid.NewGuid(), TopicType.ArchitectureDecision, "research concluded"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Convert_carries_scope_streams_systems_tags_and_urgency()
    {
        var (user, clock, audit, trace) = (User(), Clock(), Substitute.For<IAuditSink>(), Substitute.For<ITraceabilityWriter>());
        var topic = Decided();
        var expectedScope = topic.Scope;
        await using var db = await Seeded(topic, user, clock);

        var result = await Handler(db, user, clock, audit, trace)
            .Handle(new ConvertTopicCommand(topic.PublicId, TopicType.ArchitectureDecision, "research concluded"),
                CancellationToken.None);

        var successor = await db.Topics.SingleAsync(t => t.PublicId == result.Id);
        successor.AffectedStreams.Should().BeEquivalentTo(new[] { "identity", "platform" });
        successor.Systems.Should().BeEquivalentTo(new[] { "Auth Service" });
        successor.Tags.Should().BeEquivalentTo(new[] { "iam" });
        successor.Urgency.Should().Be(TopicUrgency.Urgent);
        successor.Source.Should().Be(TopicSource.SecurityFinding);
        // Scope drives stream-scoped authorization. Draft() defaults it to SingleStream, so a
        // successor that silently narrowed from MultiStream would change who may act on it.
        successor.Scope.Should().Be(expectedScope);
    }
}
