using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Integration.Tests;

// P15c / W16 (ADR-0001). The one-topic-per-recommendation guard is DB-enforced by a FILTERED unique index on
// Topic.SourceRecommendationId — the backstop the app-level check (ConvertResearchToTopicHandler) cannot
// guarantee under a concurrent double-convert. EF-InMemory ignores unique indexes, so this is only observable
// against real SQL Server: two topics claiming the same recommendation must fail; the filter must still allow
// many topics with a NULL source (ordinary submissions).
[Collection(SqlBackstopCollection.Name)]
public sealed class TopicConvertGuardTests
{
    private readonly SqlBackstopFixture _fixture;

    public TopicConvertGuardTests(SqlBackstopFixture fixture) => _fixture = fixture;

    private static Topic DraftFromRecommendation(string key, Guid? sourceRecommendationId) =>
        Topic.Draft(key, "Converted topic", "desc", "just",
            TopicType.ArchitectureDecision, TopicUrgency.Normal, TopicSource.CommitteeMember, "seed-actor", "Seed",
            new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>(), sourceRecommendationId);

    [Fact]
    public async Task Two_topics_from_one_recommendation_are_rejected_by_the_unique_index()
    {
        var recId = Guid.NewGuid();

        await using (var db = _fixture.NewTopicsSql())
        {
            db.Topics.Add(DraftFromRecommendation("TOP-CONV-1", recId));
            await db.SaveChangesAsync();
        }

        await using var db2 = _fixture.NewTopicsSql();
        db2.Topics.Add(DraftFromRecommendation("TOP-CONV-2", recId)); // same SourceRecommendationId
        var act = () => db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("the filtered unique index forbids a second topic per recommendation");
    }

    [Fact]
    public async Task Many_topics_with_no_source_recommendation_are_allowed_by_the_filter()
    {
        await using var db = _fixture.NewTopicsSql();
        db.Topics.Add(DraftFromRecommendation("TOP-NULL-1", null));
        db.Topics.Add(DraftFromRecommendation("TOP-NULL-2", null));

        var act = () => db.SaveChangesAsync();

        await act.Should().NotThrowAsync("the unique index is filtered to non-NULL SourceRecommendationId");
    }
}
