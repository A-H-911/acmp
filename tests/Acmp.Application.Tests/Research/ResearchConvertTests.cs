using Acmp.Modules.Research.Application.Features.CreateMission;
using Acmp.Modules.Research.Application.Features.ManageRecommendations;
using Acmp.Modules.Research.Application.Features.MissionLifecycle;
using Acmp.Modules.Research.Domain.Enums;
using Acmp.Modules.Research.Infrastructure.Directory;
using Acmp.Modules.Research.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Topics;
using Acmp.Shared.Contracts.Traceability;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Research;

// P15c convert wiring on the Research side (InMemory ResearchDbContext): the recommendation Converted transition
// (via MarkRecommendationConverted), the FR-115 source-topic edge emitted on CreateMission, and the IResearch
// reader seam consumed by the Topics convert.
public class ResearchConvertTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static LocalizedString L(string s = "x") => LocalizedString.Create(s, s);

    private static ResearchDbContext Db(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<ResearchDbContext>().UseInMemoryDatabase("resc-" + Guid.NewGuid()).Options, clock, user);

    private static ICurrentUser User()
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns("kc-owner");
        u.DisplayName.Returns("Owner");
        return u;
    }

    private static IClock Clock()
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(Now);
        return c;
    }

    private sealed class FakeTopicReader : ITopicReader
    {
        private readonly TopicSummary? _t;
        public FakeTopicReader(TopicSummary? t) => _t = t;
        public Task<TopicSummary?> GetSummaryAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_t);
    }

    private sealed class RecordingTraceWriter : ITraceabilityWriter
    {
        public List<(string SrcType, Guid SrcId, string TgtType, Guid TgtId, string Rel)> Edges { get; } = new();
        public Task RecordEdgeAsync(string sourceType, Guid sourceId, string sourceKey, string sourceTitle,
            string targetType, Guid targetId, string targetKey, string targetTitle, string relTypeName, CancellationToken ct = default)
        {
            Edges.Add((sourceType, sourceId, targetType, targetId, relTypeName));
            return Task.CompletedTask;
        }
    }

    private static CreateMissionHandler CreateHandler(ResearchDbContext db, ICurrentUser user, IClock clock,
        ITopicReader topics, ITraceabilityWriter trace) =>
        new(db, new ResearchKeyGenerator(db), user, clock, Substitute.For<IAuditSink>(), topics, trace);

    // Create → Activate → add + accept a recommendation. Returns (missionId, recommendationId).
    private static async Task<(Guid MissionId, Guid RecId)> AcceptedRecommendationAsync(ResearchDbContext db, ICurrentUser user, IClock clock)
    {
        var created = await CreateHandler(db, user, clock, new FakeTopicReader(null), new RecordingTraceWriter())
            .Handle(new CreateMissionCommand(L("Title"), L("Question"), null, null), CancellationToken.None);
        await new ActivateMissionHandler(db, clock, Substitute.For<IAuditSink>()).Handle(new ActivateMissionCommand(created.Id), CancellationToken.None);
        await new AddRecommendationHandler(db, Substitute.For<IAuditSink>()).Handle(
            new AddRecommendationCommand(created.Id, L("Statement"), null, RecommendationPriority.High, null), CancellationToken.None);
        var recId = (await db.Missions.FirstAsync(m => m.PublicId == created.Id)).Recommendations.First().PublicId;
        await new SetRecommendationStatusHandler(db, Substitute.For<IAuditSink>()).Handle(
            new SetRecommendationStatusCommand(created.Id, recId, RecommendationStatus.Accepted), CancellationToken.None);
        return (created.Id, recId);
    }

    [Fact]
    public async Task MarkConverted_sets_status_and_linked_topic()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);
        var (missionId, recId) = await AcceptedRecommendationAsync(db, user, clock);
        var topicId = Guid.NewGuid();

        await new MarkRecommendationConvertedHandler(db, Substitute.For<IAuditSink>())
            .Handle(new MarkRecommendationConvertedCommand(missionId, recId, topicId), CancellationToken.None);

        var rec = (await db.Missions.FirstAsync(m => m.PublicId == missionId)).Recommendations.Single();
        rec.Status.Should().Be(RecommendationStatus.Converted);
        rec.LinkedTopicId.Should().Be(topicId);
    }

    [Fact]
    public async Task MarkConverted_requires_an_accepted_recommendation()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);
        // A Proposed (never accepted) recommendation.
        var created = await CreateHandler(db, user, clock, new FakeTopicReader(null), new RecordingTraceWriter())
            .Handle(new CreateMissionCommand(L("t"), L("q"), null, null), CancellationToken.None);
        await new ActivateMissionHandler(db, clock, Substitute.For<IAuditSink>()).Handle(new ActivateMissionCommand(created.Id), CancellationToken.None);
        await new AddRecommendationHandler(db, Substitute.For<IAuditSink>()).Handle(
            new AddRecommendationCommand(created.Id, L("s"), null, RecommendationPriority.Low, null), CancellationToken.None);
        var recId = (await db.Missions.FirstAsync(m => m.PublicId == created.Id)).Recommendations.First().PublicId;

        var act = () => new MarkRecommendationConvertedHandler(db, Substitute.For<IAuditSink>())
            .Handle(new MarkRecommendationConvertedCommand(created.Id, recId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*accepted*");
    }

    [Fact]
    public async Task CreateMission_with_source_topic_records_a_reference_edge()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);
        var sourceTopicId = Guid.NewGuid();
        var trace = new RecordingTraceWriter();
        var reader = new FakeTopicReader(new TopicSummary(sourceTopicId, "TOP-2026-009", "Auth study"));

        var created = await CreateHandler(db, user, clock, reader, trace)
            .Handle(new CreateMissionCommand(L("Title"), L("Question"), null, sourceTopicId), CancellationToken.None);

        trace.Edges.Should().ContainSingle();
        var e = trace.Edges[0];
        e.SrcType.Should().Be("Topic");
        e.SrcId.Should().Be(sourceTopicId);
        e.TgtType.Should().Be("ResearchMission");
        e.Rel.Should().Be("References");
    }

    [Fact]
    public async Task CreateMission_with_unknown_source_topic_records_no_edge()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);
        var trace = new RecordingTraceWriter();

        await CreateHandler(db, user, clock, new FakeTopicReader(null), trace)
            .Handle(new CreateMissionCommand(L("Title"), L("Question"), null, Guid.NewGuid()), CancellationToken.None);

        trace.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateMission_without_source_topic_records_no_edge()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);
        var trace = new RecordingTraceWriter();

        await CreateHandler(db, user, clock, new FakeTopicReader(null), trace)
            .Handle(new CreateMissionCommand(L("Title"), L("Question"), null, null), CancellationToken.None);

        trace.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task ResearchReader_projects_mission_and_recommendation_and_returns_null_when_absent()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);
        var (missionId, recId) = await AcceptedRecommendationAsync(db, user, clock);
        var reader = new ResearchReader(db);

        (await reader.GetMissionForConvertAsync(missionId))!.Status.Should().Be("Active");
        (await reader.GetMissionForConvertAsync(Guid.NewGuid())).Should().BeNull();

        var rec = await reader.GetRecommendationForConvertAsync(missionId, recId);
        rec!.Status.Should().Be("Accepted");
        rec.MissionId.Should().Be(missionId);
        (await reader.GetRecommendationForConvertAsync(missionId, Guid.NewGuid())).Should().BeNull();
        (await reader.GetRecommendationForConvertAsync(Guid.NewGuid(), recId)).Should().BeNull();
    }
}
