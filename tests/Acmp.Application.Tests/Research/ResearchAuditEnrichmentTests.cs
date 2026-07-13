using Acmp.Modules.Research.Application.Features.CreateMission;
using Acmp.Modules.Research.Application.Features.MissionLifecycle;
using Acmp.Modules.Research.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acmp.Application.Tests.Research;

// INV-005 / guardrail 1: proves the Research module actually produces ENRICHED (before/after populated) audit
// rows — not silently lean ones. It wires the SAME collaborators AddResearchModule wires in production: the
// AuditCaptureInterceptor (attached to ResearchDbContext) records the mission's scalar deltas into a shared
// AuditChangeBuffer on SaveChanges, and the real SqlAuditSink drains that buffer by (subjectType, subjectId)
// when the handler emits its governance event. A create captures After; a lifecycle transition captures both
// Before and After.
public class ResearchAuditEnrichmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static LocalizedString L(string s = "x") => LocalizedString.Create(s, s);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private static ICurrentUser User()
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns("kc-owner");
        u.DisplayName.Returns("Owner");
        u.Roles.Returns(new[] { "Chairman" });
        return u;
    }

    [Fact]
    public async Task Mission_lifecycle_writes_enriched_audit_rows_with_before_and_after()
    {
        var name = "res-audit-" + Guid.NewGuid();
        var buffer = new AuditChangeBuffer();
        var interceptor = new AuditCaptureInterceptor(buffer);
        var clock = new FixedClock();
        var user = User();

        await using var auditDb = new AuditDbContext(
            new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(name + "-audit").Options);
        var sink = new SqlAuditSink(auditDb, clock, user, buffer, NullLogger<SqlAuditSink>.Instance);

        await using var db = new ResearchDbContext(
            new DbContextOptionsBuilder<ResearchDbContext>().UseInMemoryDatabase(name).AddInterceptors(interceptor).Options,
            clock, user);

        // Create → the mission is Added, so the capture is After-only.
        var created = await new CreateMissionHandler(db, new ResearchKeyGenerator(db), user, clock, sink,
                Substitute.For<Acmp.Shared.Contracts.Topics.ITopicReader>(),
                Substitute.For<Acmp.Shared.Contracts.Traceability.ITraceabilityWriter>())
            .Handle(new CreateMissionCommand(L("Title"), L("Question"), null, null), CancellationToken.None);

        // Activate → the mission is Modified (Status), so the capture has Before AND After.
        await new ActivateMissionHandler(db, clock, sink)
            .Handle(new ActivateMissionCommand(created.Id), CancellationToken.None);

        var rows = await auditDb.AuditEvents.OrderBy(e => e.Sequence).ToListAsync();

        // Status serializes as its numeric value (Proposed=1, Active=2 — System.Text.Json default for enums).
        var proposed = rows.Single(r => r.Action == "Research.MissionProposed");
        proposed.SubjectType.Should().Be("ResearchMission");
        proposed.SubjectId.Should().Be(created.Id.ToString());
        proposed.BeforeJson.Should().BeNull("an insert has no prior state");
        proposed.AfterJson.Should().NotBeNull();
        proposed.AfterJson.Should().Contain("\"Status\":1");
        proposed.ActorUserId.Should().Be("kc-owner");

        var activated = rows.Single(r => r.Action == "Research.MissionActivated");
        activated.BeforeJson.Should().NotBeNull();
        activated.BeforeJson.Should().Contain("\"Status\":1");
        activated.AfterJson.Should().NotBeNull();
        activated.AfterJson.Should().Contain("\"Status\":2");
    }
}
