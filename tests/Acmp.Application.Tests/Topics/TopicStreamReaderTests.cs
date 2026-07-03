using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// The Acmp.Shared ITopicStreamReader port (P10f / FR-095 Topic-scope): a topic's affected-stream codes, read
// through the Topics DbContext, for Topic↔Topic cross-stream classification in the impact graph. An unknown
// id returns empty so cross-stream detection degrades to "not cross" rather than throwing out of the graph.
public class TopicStreamReaderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);

    private static TopicsDbContext NewDb()
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-seed");
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(T0);
        return new TopicsDbContext(
            new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("streams-" + Guid.NewGuid()).Options,
            clock, user);
    }

    [Fact] // A known topic returns its affected-stream codes.
    public async Task Returns_the_topics_stream_codes()
    {
        await using var db = NewDb();
        var topic = Topic.Draft("TOP-2026-050", "Identity work", "Desc", "Just",
            TopicType.ArchitectureDecision, TopicUrgency.Normal, TopicSource.CommitteeMember,
            "kc-sub", "Actor", new[] { "identity", "platform" }, Array.Empty<string>(), Array.Empty<string>());
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var streams = await new TopicStreamReader(db).GetStreamsAsync(topic.PublicId);

        streams.Should().BeEquivalentTo("identity", "platform");
    }

    [Fact] // An unknown topic id returns empty (self-describing edge snapshot with no live topic row).
    public async Task Returns_empty_for_an_unknown_topic()
    {
        await using var db = NewDb();
        var streams = await new TopicStreamReader(db).GetStreamsAsync(Guid.NewGuid());
        streams.Should().BeEmpty();
    }
}
