using Acmp.Modules.Topics.Application.Features.GetBacklog;
using Acmp.Modules.Topics.Application.Features.GetTopicDetail;
using Acmp.Modules.Topics.Application.Features.SubmitTopic;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// Round-trip through the real TopicsDbContext (InMemory) — validates the EF mapping for the JSON string
// collections, the owned child tables, and the key counter, plus the submit → backlog → detail flow.
public class TopicHandlerTests
{
    // The context's clock stamps CreatedAt/UpdatedAt — share it with the handler so aging is deterministic.
    private static TopicsDbContext NewDb(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("topics-" + Guid.NewGuid()).Options,
            clock, user);

    private static ICurrentUser User(string sub, string name)
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns(sub);
        u.DisplayName.Returns(name);
        return u;
    }

    private static IClock Clock(DateTimeOffset now)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(now);
        return c;
    }

    private static SubmitTopicCommand Command() => new(
        "Adopt Keycloak", "Consolidate IAM onto Keycloak.", "Fragmented auth is risky.",
        TopicType.ArchitectureDecision, TopicUrgency.Urgent, TopicSource.CommitteeMember,
        new[] { "identity", "platform" }, new[] { "API Gateway" }, new[] { "SecurityArch" });

    [Fact]
    public async Task Submit_persists_topic_with_generated_key_streams_and_history()
    {
        var now = new DateTimeOffset(2026, 2, 15, 9, 0, 0, TimeSpan.Zero);
        var user = User("kc-omar", "Omar H.");
        var clock = Clock(now);
        await using var db = NewDb(user, clock);
        var audit = Substitute.For<IAuditSink>();

        var result = await new SubmitTopicHandler(db, new TopicKeyGenerator(db), user, clock, audit)
            .Handle(Command(), CancellationToken.None);

        result.Key.Should().Be("TOP-2026-001");

        var stored = await db.Topics.Include(t => t.History).SingleAsync();
        stored.Status.Should().Be(TopicStatus.Submitted);
        stored.Scope.Should().Be(TopicScope.MultiStream);            // derived from 2 streams
        stored.AffectedStreams.Should().BeEquivalentTo("identity", "platform");  // JSON collection round-trips
        stored.SubmittedByName.Should().Be("Omar H.");
        stored.History.Should().ContainSingle(h => h.ToStatus == TopicStatus.Submitted);  // owned child table
        await audit.Received(1).EmitAsync("Topics.TopicSubmitted", "kc-omar", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Backlog_and_detail_read_back_the_submitted_topic()
    {
        var now = new DateTimeOffset(2026, 2, 15, 9, 0, 0, TimeSpan.Zero);
        var user = User("kc-omar", "Omar H.");
        var clock = Clock(now);
        await using var db = NewDb(user, clock);

        var result = await new SubmitTopicHandler(db, new TopicKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
            .Handle(Command(), CancellationToken.None);

        // SLA aging: Urgent (7-day threshold) submitted 9 days ago → breaching (AC-057).
        var later = now.AddDays(9);
        var backlog = await new GetBacklogHandler(db, Clock(later)).Handle(new GetBacklogQuery(), CancellationToken.None);
        backlog.Total.Should().Be(1);
        backlog.Items[0].Key.Should().Be(result.Key);
        backlog.Items[0].SlaBreached.Should().BeTrue();
        backlog.Items[0].AgeDays.Should().Be(9);

        var detail = await new GetTopicDetailHandler(db, Clock(later)).Handle(new GetTopicDetailQuery(result.Key), CancellationToken.None);
        detail.Should().NotBeNull();
        detail!.Streams.Should().BeEquivalentTo("identity", "platform");
        detail.Tags.Should().BeEquivalentTo("SecurityArch");
        detail.History.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Key_generator_increments_per_year()
    {
        var user = User("kc-x", "X");
        await using var db = NewDb(user, Clock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        var gen = new TopicKeyGenerator(db);

        (await gen.NextAsync(2026)).Should().Be("TOP-2026-001");
        (await gen.NextAsync(2026)).Should().Be("TOP-2026-002");
        (await gen.NextAsync(2027)).Should().Be("TOP-2027-001");
    }
}
