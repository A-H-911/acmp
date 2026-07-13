using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// The ITopicReader seam (P15c / FR-115): a topic's key + title snapshot, or null for an unknown id.
public class TopicReaderTests
{
    private static TopicsDbContext Db()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns("kc-sec");
        return new TopicsDbContext(
            new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("topicreader-" + Guid.NewGuid()).Options, clock, user);
    }

    [Fact]
    public async Task GetSummary_returns_key_and_title_or_null()
    {
        await using var db = Db();
        var topic = Topic.Draft("TOP-2026-009", "Auth study", "desc", "just",
            TopicType.ResearchDiscovery, TopicUrgency.Normal, TopicSource.CommitteeMember, "kc-sec", "Secretary",
            new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>());
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var reader = new TopicReader(db);
        var summary = await reader.GetSummaryAsync(topic.PublicId);
        summary!.Key.Should().Be("TOP-2026-009");
        summary.Title.Should().Be("Auth study");

        (await reader.GetSummaryAsync(Guid.NewGuid())).Should().BeNull();
    }
}
