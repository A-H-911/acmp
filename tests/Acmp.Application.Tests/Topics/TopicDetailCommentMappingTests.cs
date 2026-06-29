using Acmp.Modules.Topics.Application.Features.GetTopicDetail;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// Exercises the TopicCommentDto constructor (line currently at 0% coverage).
// The only way to reach it is to run GetTopicDetailHandler on a topic that has at least one comment,
// so the Select(c => new TopicCommentDto(...)) mapping line executes.
public class TopicDetailCommentMappingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 4, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddMinutes(5);

    private static TopicsDbContext NewDb(IClock clock)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-owner");
        return new TopicsDbContext(
            new DbContextOptionsBuilder<TopicsDbContext>()
                .UseInMemoryDatabase("comment-map-" + Guid.NewGuid())
                .Options,
            clock, user);
    }

    private static IClock ClockAt(DateTimeOffset t)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(t);
        return c;
    }

    [Fact]
    public async Task GetTopicDetail_maps_comments_into_TopicCommentDto_with_correct_values()
    {
        // Arrange
        var clock = ClockAt(T0);
        await using var db = NewDb(clock);

        var topic = Topic.Draft(
            "TOP-2026-999",
            "Commented Topic",
            "Description",
            "Justification",
            TopicType.ArchitectureDecision,
            TopicUrgency.Normal,
            TopicSource.CommitteeMember,
            "kc-author-sub",
            "Author Name",
            new[] { "platform" },
            Array.Empty<string>(),
            Array.Empty<string>());
        topic.Submit(T0);

        // Add a comment via the domain aggregate before persisting
        topic.AddComment("Great topic!", "kc-commenter", "Commenter Name", T1);

        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var handler = new GetTopicDetailHandler(db, clock);

        // Act
        var dto = await handler.Handle(new GetTopicDetailQuery(topic.Key), default);

        // Assert — TopicCommentDto constructor is reached only inside the Select mapping
        dto.Should().NotBeNull();
        dto!.Comments.Should().HaveCount(1);

        var comment = dto.Comments[0];
        comment.Body.Should().Be("Great topic!");
        comment.AuthorName.Should().Be("Commenter Name");
        comment.PostedAt.Should().Be(T1);
    }

    [Fact]
    public async Task GetTopicDetail_returns_empty_comments_list_when_topic_has_no_comments()
    {
        // Arrange — baseline: topic without comments must also compile/map cleanly
        var clock = ClockAt(T0);
        await using var db = NewDb(clock);

        var topic = Topic.Draft(
            "TOP-2026-998",
            "No Comments Topic",
            "Description",
            "Justification",
            TopicType.ArchitectureDecision,
            TopicUrgency.Normal,
            TopicSource.CommitteeMember,
            "kc-sub",
            "Actor",
            new[] { "platform" },
            Array.Empty<string>(),
            Array.Empty<string>());
        topic.Submit(T0);

        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var handler = new GetTopicDetailHandler(db, clock);

        // Act
        var dto = await handler.Handle(new GetTopicDetailQuery(topic.Key), default);

        // Assert
        dto.Should().NotBeNull();
        dto!.Comments.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTopicDetail_orders_multiple_comments_by_PostedAt_ascending()
    {
        // Arrange — validates the OrderBy(c => c.PostedAt) in the mapping
        var clock = ClockAt(T0);
        await using var db = NewDb(clock);

        var topic = Topic.Draft(
            "TOP-2026-997",
            "Multi Comment Topic",
            "Description",
            "Justification",
            TopicType.ArchitectureDecision,
            TopicUrgency.Normal,
            TopicSource.CommitteeMember,
            "kc-sub",
            "Actor",
            new[] { "platform" },
            Array.Empty<string>(),
            Array.Empty<string>());
        topic.Submit(T0);

        var second = T0.AddHours(2);
        var first = T0.AddHours(1);

        // Add later comment first to confirm OrderBy, not insertion order
        topic.AddComment("Second comment", "kc-c1", "First Commenter", second);
        topic.AddComment("First comment", "kc-c2", "Second Commenter", first);

        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var handler = new GetTopicDetailHandler(db, clock);

        // Act
        var dto = await handler.Handle(new GetTopicDetailQuery(topic.Key), default);

        // Assert
        dto!.Comments.Should().HaveCount(2);
        dto.Comments[0].PostedAt.Should().Be(first);
        dto.Comments[1].PostedAt.Should().Be(second);
    }
}
