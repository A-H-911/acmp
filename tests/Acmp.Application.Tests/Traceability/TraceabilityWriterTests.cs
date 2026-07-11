using Acmp.Modules.Traceability.Infrastructure.Directory;
using Acmp.Modules.Traceability.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Traceability;

// The ITraceabilityWriter seam (P11e) over the real TraceabilityDbContext (InMemory): a system edge is
// persisted + audited, a repeat is a no-op (idempotent per source/target/relType), and an unknown type name
// is rejected. The FR-068 happy path is covered end to end by PromoteDecisionToAdrApiTests.
public class TraceabilityWriterTests
{
    private static TraceabilityDbContext Db(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<TraceabilityDbContext>().UseInMemoryDatabase("tw-" + Guid.NewGuid()).Options, clock, user);

    private static ICurrentUser User()
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns("kc-chair");
        return u;
    }

    private static IClock Clock()
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        return c;
    }

    private sealed class RecordingAudit : IAuditSink
    {
        public List<string> Events { get; } = new();
        public Task EmitAsync(string e, string? s, object? d = null, CancellationToken ct = default) { Events.Add(e); return Task.CompletedTask; }
        public Task EmitEnrichedAsync(string action, string subjectType, string? subjectId, string outcome = "Success", CancellationToken ct = default) { Events.Add(action); return Task.CompletedTask; }
    }

    private static readonly Guid Src = Guid.NewGuid();
    private static readonly Guid Tgt = Guid.NewGuid();

    private static Task Record(TraceabilityWriter w) =>
        w.RecordEdgeAsync("Decision", Src, "DECN-2026-008", "Adopt Keycloak",
            "Adr", Tgt, "ADR-2026-004", "Adopt Keycloak", "RecordedAs");

    [Fact]
    public async Task Records_a_system_edge_and_audits()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);
        var audit = new RecordingAudit();

        await Record(new TraceabilityWriter(db, user, audit));

        var edge = await db.Relationships.SingleAsync();
        edge.RelType.ToString().Should().Be("RecordedAs");
        edge.SourceKey.Should().Be("DECN-2026-008");
        edge.TargetKey.Should().Be("ADR-2026-004");
        audit.Events.Should().Contain("Relationship.Created");
    }

    [Fact]
    public async Task Repeat_is_idempotent_no_duplicate_edge()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);

        await Record(new TraceabilityWriter(db, user, new RecordingAudit()));
        await Record(new TraceabilityWriter(db, user, new RecordingAudit()));

        (await db.Relationships.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Unknown_type_name_is_rejected()
    {
        var user = User(); var clock = Clock();
        await using var db = Db(user, clock);
        var w = new TraceabilityWriter(db, user, new RecordingAudit());

        var act = () => w.RecordEdgeAsync("Decision", Src, "k", "t", "Adr", Tgt, "k2", "t2", "NotARealRelType");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
