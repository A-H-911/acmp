using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Decisions;

// Covers the REAL cross-module seam the Decisions module drives on issue (W12, ADR-0001): the Topics-side
// TopicDecisionRecorder advancing a topic InCommittee→Decided, and its idempotent no-op for any other
// state. The handler tests use a fake recorder (to assert Decisions calls the seam); this exercises the
// actual Topics implementation end-to-end against a real TopicsDbContext.
public class TopicDecisionRecorderTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static TopicsDbContext NewDb(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("topics-" + Guid.NewGuid()).Options, clock, user);

    private static ICurrentUser User(string sub = "kc-chair", string name = "Sara Chair")
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns(sub);
        u.DisplayName.Returns(name);
        return u;
    }

    private static IClock Clock()
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(Now);
        return c;
    }

    private static Topic DraftTopic() => Topic.Draft("TOP-2026-001", "Adopt Keycloak", "Desc", "Just",
        TopicType.ArchitectureDecision, TopicUrgency.Normal, TopicSource.CommitteeMember,
        "kc-sub", "Submitter", new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>());

    private static void WalkToInCommittee(Topic t)
    {
        t.Submit(Now);
        t.BeginTriage("kc-sec", "Sec", Now);
        t.Accept(Guid.NewGuid(), "Owner", "kc-sec", "Sec", Now);
        t.MarkPrepared("kc-owner", "Owner", Now);
        t.Schedule(Guid.NewGuid(), "kc-sec", "Sec", Now);
        t.EnterCommittee("kc-sec", "Sec", Now);
    }

    [Fact] // W12 happy path: an InCommittee topic is advanced to Decided
    public async Task MarkDecided_advances_an_in_committee_topic_to_decided()
    {
        var user = User(); var clock = Clock();
        await using var db = NewDb(user, clock);
        var topic = DraftTopic();
        WalkToInCommittee(topic);
        topic.Status.Should().Be(TopicStatus.InCommittee);
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        await new TopicDecisionRecorder(db, user, clock).MarkDecidedAsync(topic.PublicId, default);

        var reloaded = await db.Topics.AsNoTracking().SingleAsync(t => t.PublicId == topic.PublicId);
        reloaded.Status.Should().Be(TopicStatus.Decided);
    }

    [Fact] // idempotent: a topic that is not InCommittee is left untouched (no throw, no change)
    public async Task MarkDecided_is_a_noop_when_the_topic_is_not_in_committee()
    {
        var user = User(); var clock = Clock();
        await using var db = NewDb(user, clock);
        var topic = DraftTopic();   // still Draft
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        await new TopicDecisionRecorder(db, user, clock).MarkDecidedAsync(topic.PublicId, default);
        await new TopicDecisionRecorder(db, user, clock).MarkDecidedAsync(Guid.NewGuid(), default); // unknown id

        var reloaded = await db.Topics.AsNoTracking().SingleAsync(t => t.PublicId == topic.PublicId);
        reloaded.Status.Should().Be(TopicStatus.Draft);
    }
}
