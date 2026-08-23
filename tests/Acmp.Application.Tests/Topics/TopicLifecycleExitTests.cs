using Acmp.Modules.Topics.Application.Features.CloseTopic;
using Acmp.Modules.Topics.Application.Features.ReactivateTopic;
using Acmp.Modules.Topics.Application.Features.ReopenTopic;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// FR-160 / FR-161 / FR-045 — AC-109, AC-110, AC-112.
//
// THE GAP THESE CLOSE. Topic.Decide and Topic.Defer were both called in production, so topics
// reached Decided and Deferred; nothing called Close, Reactivate or Reopen, so those were terminal
// states with no exit. The DW-026 reachability check found the three orphaned methods (DEF-084).
//
// Each transition is asserted in BOTH directions — the move happens AND it is refused from a status
// that must not permit it. The negative half is not padding: RequireStatus already existed on every
// one of these methods and was never once exercised through a handler, and a transition guard that
// nothing exercises is indistinguishable from an absent one.
public class TopicLifecycleExitTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 10, 0, 0, TimeSpan.Zero);

    private static TopicsDbContext NewDb(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("exits-" + Guid.NewGuid()).Options,
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

    // Permits everything: these tests are about the STATUS guard in the aggregate, not about ABAC,
    // which has its own suites. A substitute that refused would mask the transition under a 403.
    private static IResourceAuthorizer Authorizer() => Substitute.For<IResourceAuthorizer>();

    private static Topic NewTopic() => Topic.Draft(
        "TOP-2026-001", "Adopt Keycloak", "Consolidate IAM.", "Fragmented auth is risky.",
        TopicType.ArchitectureDecision, TopicUrgency.Urgent, TopicSource.CommitteeMember,
        "kc-omar", "Omar H.", new[] { "identity" }, Array.Empty<string>(), Array.Empty<string>());

    /// <summary>Walks a topic all the way to Decided — the only status Close accepts.</summary>
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

    // ---- AC-109: close ----

    [Fact]
    public async Task Close_moves_a_decided_topic_to_closed_and_audits_it()
    {
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        var topic = Decided();
        await using var db = await Seeded(topic, user, clock);

        await new CloseTopicHandler(db, Authorizer(), user, clock, audit)
            .Handle(new CloseTopicCommand(topic.PublicId), CancellationToken.None);

        var stored = await db.Topics.Include(t => t.History).SingleAsync();
        stored.Status.Should().Be(TopicStatus.Closed);
        stored.History.Should().Contain(h => h.ToStatus == TopicStatus.Closed,
            "the transition is written to the topic's immutable history (AC-109)");

        await audit.Received(1).EmitEnrichedAsync(
            "Topics.TopicClosed", nameof(Topic), topic.PublicId.ToString(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Close_is_refused_from_a_status_other_than_decided()
    {
        var (user, clock) = (User(), Clock());
        var topic = NewTopic();
        topic.Submit(Now);                       // Submitted — nowhere near Decided
        await using var db = await Seeded(topic, user, clock);

        var act = () => new CloseTopicHandler(db, Authorizer(), user, clock, Substitute.For<IAuditSink>())
            .Handle(new CloseTopicCommand(topic.PublicId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await db.Topics.SingleAsync()).Status.Should().Be(TopicStatus.Submitted, "the status is unchanged");
    }

    // ---- AC-110: return from deferred ----

    [Fact]
    public async Task Reactivate_returns_a_deferred_topic_to_triage_and_keeps_its_revisit_date()
    {
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        var revisitOn = Now.AddDays(30);
        var topic = NewTopic();
        topic.Submit(Now);
        topic.BeginTriage("kc-secretary", "Sara S.", Now);
        topic.Defer("awaiting vendor confirmation", revisitOn, "kc-secretary", "Sara S.", Now);
        await using var db = await Seeded(topic, user, clock);

        await new ReactivateTopicHandler(db, Authorizer(), user, clock, audit)
            .Handle(new ReactivateTopicCommand(topic.PublicId), CancellationToken.None);

        var stored = await db.Topics.Include(t => t.History).SingleAsync();
        stored.Status.Should().Be(TopicStatus.Triage);
        stored.RevisitOn.Should().Be(revisitOn,
            "the revisit date records what the committee agreed when they deferred; the history is "
            + "immutable (FR-044) and coming back must not erase it");
        stored.History.Should().Contain(h => h.Reason == "awaiting vendor confirmation",
            "the original deferral reason survives the return trip");
    }

    [Fact]
    public async Task Reactivate_is_refused_when_the_topic_is_not_deferred()
    {
        var (user, clock) = (User(), Clock());
        var topic = NewTopic();
        topic.Submit(Now);
        await using var db = await Seeded(topic, user, clock);

        var act = () => new ReactivateTopicHandler(db, Authorizer(), user, clock, Substitute.For<IAuditSink>())
            .Handle(new ReactivateTopicCommand(topic.PublicId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await db.Topics.SingleAsync()).Status.Should().Be(TopicStatus.Submitted);
    }

    // ---- AC-112: reopen (FR-045) ----

    [Fact]
    public async Task Reopen_returns_a_rejected_topic_to_the_triage_workflow_with_its_justification()
    {
        var (user, clock, audit) = (User(), Clock(), Substitute.For<IAuditSink>());
        var topic = NewTopic();
        topic.Submit(Now);
        topic.Reject("out of committee scope", "kc-secretary", "Sara S.", Now);
        await using var db = await Seeded(topic, user, clock);

        await new ReopenTopicHandler(db, Authorizer(), user, clock, audit)
            .Handle(new ReopenTopicCommand(topic.PublicId, "new regulatory guidance changes the assessment"),
                CancellationToken.None);

        var stored = await db.Topics.Include(t => t.History).SingleAsync();
        stored.Status.Should().Be(TopicStatus.Reopened);
        stored.History.Should().Contain(h => h.Reason == "new regulatory guidance changes the assessment",
            "the justification is recorded in the immutable history (AC-112)");
    }

    // ⚠ THE CLOSED BRANCH ONLY BECAME REACHABLE WITH FR-160. Before Close was wired, no topic could
    // ever be Closed, so half of Reopen's RequireStatus(Rejected, Closed) was dead by construction —
    // which is why AC-112 exercises BOTH source statuses and why the two ship together.
    [Fact]
    public async Task Reopen_also_accepts_a_closed_topic_the_branch_that_only_exists_because_close_was_wired()
    {
        var (user, clock) = (User(), Clock());
        var topic = Decided();
        topic.Close("kc-secretary", "Sara S.", Now);
        await using var db = await Seeded(topic, user, clock);

        await new ReopenTopicHandler(db, Authorizer(), user, clock, Substitute.For<IAuditSink>())
            .Handle(new ReopenTopicCommand(topic.PublicId, "the decision rested on a superseded standard"),
                CancellationToken.None);

        (await db.Topics.SingleAsync()).Status.Should().Be(TopicStatus.Reopened);
    }

    [Fact]
    public async Task Close_reports_an_unknown_topic_as_not_found()
    {
        var (user, clock) = (User(), Clock());
        await using var db = NewDb(user, clock);

        var act = () => new CloseTopicHandler(db, Authorizer(), user, clock, Substitute.For<IAuditSink>())
            .Handle(new CloseTopicCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Reactivate_reports_an_unknown_topic_as_not_found()
    {
        var (user, clock) = (User(), Clock());
        await using var db = NewDb(user, clock);

        var act = () => new ReactivateTopicHandler(db, Authorizer(), user, clock, Substitute.For<IAuditSink>())
            .Handle(new ReactivateTopicCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void Close_requires_a_topic_id()
        => new CloseTopicValidator().Validate(new CloseTopicCommand(Guid.Empty)).IsValid.Should().BeFalse();

    [Fact]
    public void Reactivate_requires_a_topic_id()
        => new ReactivateTopicValidator().Validate(new ReactivateTopicCommand(Guid.Empty)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reopen_requires_a_justification(string justification)
    {
        var result = new ReopenTopicValidator()
            .Validate(new ReopenTopicCommand(Guid.NewGuid(), justification));

        result.IsValid.Should().BeFalse(
            "a reopen justification is mandatory, matching the rejection and deferral rule (FR-044)");
    }
}
