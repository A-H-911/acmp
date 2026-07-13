using Acmp.Modules.Topics.Application.Features.ConvertResearchToTopic;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Research;
using Acmp.Shared.Contracts.Traceability;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// W16 / FR-113 convert over the real TopicsDbContext (InMemory): mission→topic and recommendation→topic create a
// native Topic + an Informs edge (via the ITraceabilityWriter seam), stamp SourceRecommendationId on the
// rec-seeded topic, and enforce the eligibility + one-per-recommendation guards (404, not-Completed 409,
// not-Accepted 409, already-converted 409 with edge self-heal).
public class ConvertResearchToTopicTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid MissionId = Guid.NewGuid();
    private static readonly Guid RecId = Guid.NewGuid();

    private static TopicsDbContext Db(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("topics-convert-" + Guid.NewGuid()).Options, clock, user);

    private static ICurrentUser User()
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns("kc-sec");
        u.DisplayName.Returns("Secretary");
        return u;
    }

    private static IClock Clock()
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(Now);
        return c;
    }

    private sealed class RecordingAudit : IAuditSink
    {
        public List<string> Events { get; } = new();
        public Task EmitAsync(string e, string? s, object? d = null, CancellationToken ct = default) { Events.Add(e); return Task.CompletedTask; }
        public Task EmitEnrichedAsync(string action, string subjectType, string? subjectId, string outcome = "Success", CancellationToken ct = default) { Events.Add(action); return Task.CompletedTask; }
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

    private sealed class FakeResearchReader : IResearchReader
    {
        private readonly MissionForConvert? _m;
        private readonly RecommendationForConvert? _r;
        public FakeResearchReader(MissionForConvert? m, RecommendationForConvert? r = null) { _m = m; _r = r; }
        public Task<MissionForConvert?> GetMissionForConvertAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_m);
        public Task<RecommendationForConvert?> GetRecommendationForConvertAsync(Guid mid, Guid rid, CancellationToken ct = default) => Task.FromResult(_r);
    }

    private static MissionForConvert Mission(string status = "Completed") =>
        new(MissionId, "RMS-2026-001", "Event-sourcing for the audit ledger", status);

    private static RecommendationForConvert Recommendation(string status = "Accepted") =>
        new(RecId, "REC-001", "Keep the hash-chained ledger.", status, MissionId, "RMS-2026-001", null);

    private static ConvertResearchToTopicHandler Handler(TopicsDbContext db, ICurrentUser user, IClock clock,
        IAuditSink audit, IResearchReader research, ITraceabilityWriter trace) =>
        new(db, new TopicKeyGenerator(db), user, clock, audit, research, trace);

    private static ConvertResearchToTopicCommand Command(Guid? recId = null) =>
        new(MissionId, recId, "Adopt event-sourcing", "Move the ledger to an event-sourced store.",
            "Strengthen tamper-evidence.", TopicType.ArchitectureDecision, TopicUrgency.Normal,
            new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>());

    [Fact]
    public async Task Convert_completed_mission_creates_topic_with_informs_edge_and_audit()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);
        var audit = new RecordingAudit(); var trace = new RecordingTraceWriter();

        var result = await Handler(db, user, clock, audit, new FakeResearchReader(Mission()), trace)
            .Handle(Command(), CancellationToken.None);

        result.Key.Should().Be("TOP-2026-001");
        var topic = await db.Topics.SingleAsync();
        topic.SourceRecommendationId.Should().BeNull("a mission-level convert has no source recommendation");
        trace.Edges.Should().ContainSingle();
        trace.Edges[0].Should().Be(("ResearchMission", MissionId, "Topic", topic.PublicId, "Informs"));
        audit.Events.Should().Contain("Topics.TopicConvertedFromResearch");
    }

    [Fact]
    public async Task Convert_accepted_recommendation_stamps_source_and_edges_from_the_recommendation()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);
        var trace = new RecordingTraceWriter();

        var result = await Handler(db, user, clock, new RecordingAudit(), new FakeResearchReader(Mission(), Recommendation()), trace)
            .Handle(Command(RecId), CancellationToken.None);

        var topic = await db.Topics.SingleAsync();
        topic.SourceRecommendationId.Should().Be(RecId);
        result.Key.Should().Be("TOP-2026-001");
        trace.Edges[0].Should().Be(("Recommendation", RecId, "Topic", topic.PublicId, "Informs"));
    }

    [Fact]
    public async Task Convert_missing_mission_throws_not_found()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);

        var act = () => Handler(db, user, clock, new RecordingAudit(), new FakeResearchReader(null), new RecordingTraceWriter())
            .Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Convert_mission_not_completed_is_rejected()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);

        var act = () => Handler(db, user, clock, new RecordingAudit(), new FakeResearchReader(Mission("Active")), new RecordingTraceWriter())
            .Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*completed*");
        (await db.Topics.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Convert_recommendation_not_accepted_is_rejected()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);

        var act = () => Handler(db, user, clock, new RecordingAudit(),
                new FakeResearchReader(Mission(), Recommendation("Proposed")), new RecordingTraceWriter())
            .Handle(Command(RecId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*accepted*");
        (await db.Topics.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Convert_missing_recommendation_throws_not_found()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);

        var act = () => Handler(db, user, clock, new RecordingAudit(), new FakeResearchReader(Mission(), r: null), new RecordingTraceWriter())
            .Handle(Command(RecId), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Convert_already_converted_recommendation_is_blocked_and_heals_the_edge()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);
        var reader = new FakeResearchReader(Mission(), Recommendation());

        var first = await Handler(db, user, clock, new RecordingAudit(), reader, new RecordingTraceWriter())
            .Handle(Command(RecId), CancellationToken.None);

        var heal = new RecordingTraceWriter();
        var act = () => Handler(db, user, clock, new RecordingAudit(), reader, heal)
            .Handle(Command(RecId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{first.Key}*");
        (await db.Topics.CountAsync()).Should().Be(1); // no duplicate topic
        heal.Edges.Should().ContainSingle();
        heal.Edges[0].Rel.Should().Be("Informs");
    }

    [Fact]
    public void Validator_requires_mission_title_and_streams()
    {
        var v = new ConvertResearchToTopicValidator();
        v.Validate(Command()).IsValid.Should().BeTrue();
        v.Validate(Command() with { MissionId = Guid.Empty }).IsValid.Should().BeFalse();
        v.Validate(Command() with { Title = "" }).IsValid.Should().BeFalse();
        v.Validate(Command() with { Streams = Array.Empty<string>() }).IsValid.Should().BeFalse();
    }
}
