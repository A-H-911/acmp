using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// Covers the two remaining BE gaps:
//  - TopicScheduler idempotency: ScheduleAsync/EnterCommitteeAsync are no-ops when the topic is
//    missing or not in the expected source state (the Api happy-path tests only hit the success path).
//  - TopicAttachment private EF constructor: only reached when EF materialises the entity from a
//    fresh DbContext, so a save-then-reload round-trip is the honest way to cover it.
public sealed class TopicSchedulerAndPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);

    private static (TopicsDbContext db, ICurrentUser user, IClock clock) NewDb(string name)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-sec");
        user.DisplayName.Returns("Sec User");
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        var db = new TopicsDbContext(
            new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase(name).Options,
            clock, user);
        return (db, user, clock);
    }

    private static Topic SubmittedTopic(string key) =>
        TopicFromDraft(key, submit: true);

    private static Topic TopicFromDraft(string key, bool submit)
    {
        var t = Topic.Draft(key, "Scheduler test", "Description", "Justification",
            TopicType.ArchitectureDecision, TopicUrgency.Normal, TopicSource.CommitteeMember,
            "kc-author", "Author", new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>());
        if (submit) t.Submit(Now);
        return t;
    }

    // ---- TopicScheduler idempotency (the early-return guards) ----

    [Fact]
    public async Task ScheduleAsync_is_a_noop_when_the_topic_does_not_exist()
    {
        var (db, _, clock) = NewDb("sched-missing-" + Guid.NewGuid());
        await using var _db = db;
        var scheduler = new TopicScheduler(db, Substitute.For<ICurrentUser>(), clock);

        var act = async () => await scheduler.ScheduleAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ScheduleAsync_leaves_a_topic_that_is_not_Prepared_untouched()
    {
        var (db, _, clock) = NewDb("sched-wrongstate-" + Guid.NewGuid());
        await using var _db = db;
        var topic = SubmittedTopic("TOP-2026-201"); // Submitted, not Prepared
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        var scheduler = new TopicScheduler(db, Substitute.For<ICurrentUser>(), clock);

        await scheduler.ScheduleAsync(topic.PublicId, Guid.NewGuid());

        topic.Status.Should().Be(TopicStatus.Submitted); // unchanged — idempotent no-op
    }

    [Fact]
    public async Task EnterCommitteeAsync_is_a_noop_when_the_topic_does_not_exist()
    {
        var (db, _, clock) = NewDb("enter-missing-" + Guid.NewGuid());
        await using var _db = db;
        var scheduler = new TopicScheduler(db, Substitute.For<ICurrentUser>(), clock);

        var act = async () => await scheduler.EnterCommitteeAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnterCommitteeAsync_leaves_a_topic_that_is_not_Scheduled_untouched()
    {
        var (db, _, clock) = NewDb("enter-wrongstate-" + Guid.NewGuid());
        await using var _db = db;
        var topic = SubmittedTopic("TOP-2026-202");
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        var scheduler = new TopicScheduler(db, Substitute.For<ICurrentUser>(), clock);

        await scheduler.EnterCommitteeAsync(topic.PublicId);

        topic.Status.Should().Be(TopicStatus.Submitted);
    }

    private static Topic PreparedTopic(string key)
    {
        var t = TopicFromDraft(key, submit: true);
        t.BeginTriage("kc-sec", "Sec", Now);
        t.Accept(Guid.NewGuid(), "Owner", "kc-sec", "Sec", Now);
        t.MarkPrepared("kc-sec", "Sec", Now);
        return t;
    }

    [Fact]
    public async Task ScheduleAsync_moves_a_Prepared_topic_to_Scheduled()
    {
        var (db, user, clock) = NewDb("sched-ok-" + Guid.NewGuid());
        await using var _db = db;
        var topic = PreparedTopic("TOP-2026-210");
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        var scheduler = new TopicScheduler(db, user, clock);

        await scheduler.ScheduleAsync(topic.PublicId, Guid.NewGuid());

        topic.Status.Should().Be(TopicStatus.Scheduled);
    }

    [Fact]
    public async Task EnterCommitteeAsync_moves_a_Scheduled_topic_into_committee()
    {
        var (db, user, clock) = NewDb("enter-ok-" + Guid.NewGuid());
        await using var _db = db;
        var topic = PreparedTopic("TOP-2026-211");
        topic.Schedule(Guid.NewGuid(), "kc-sec", "Sec", Now);
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        var scheduler = new TopicScheduler(db, user, clock);

        await scheduler.EnterCommitteeAsync(topic.PublicId);

        topic.Status.Should().Be(TopicStatus.InCommittee);
    }

    // ---- TopicAttachment private EF constructor (materialised on reload) ----

    [Fact]
    public async Task TopicAttachment_survives_an_EF_round_trip_through_a_fresh_context()
    {
        var dbName = "attach-roundtrip-" + Guid.NewGuid();

        // Arrange + save in the first context
        var (db1, _, _) = NewDb(dbName);
        await using (db1)
        {
            var topic = Topic.Draft("TOP-2026-203", "Attach", "Description", "Justification",
                TopicType.ArchitectureDecision, TopicUrgency.Normal, TopicSource.CommitteeMember,
                "kc-author", "Author", new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>());
            topic.Submit(Now);
            topic.BeginTriage("kc-sec", "Sec", Now);
            topic.Accept(Guid.NewGuid(), "Owner", "kc-sec", "Sec", Now);
            topic.AddAttachment("spec.pdf", "application/pdf", 4096, "topics/203/spec.pdf", "kc-up", "Uploader", Now);
            db1.Topics.Add(topic);
            await db1.SaveChangesAsync();
        }

        // Act — a brand-new context has no tracked entities, so EF must materialise
        // TopicAttachment through its private parameterless constructor.
        var (db2, _, _) = NewDb(dbName);
        await using (db2)
        {
            var reloaded = await db2.Topics.Include(t => t.Attachments).FirstAsync();

            // Assert — the metadata survived the private-ctor materialisation
            var att = reloaded.Attachments.Should().ContainSingle().Subject;
            att.FileName.Should().Be("spec.pdf");
            att.StorageKey.Should().Be("topics/203/spec.pdf");
            att.SizeBytes.Should().Be(4096);
        }
    }
}
