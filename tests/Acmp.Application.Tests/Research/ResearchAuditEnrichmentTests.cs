using Acmp.Modules.Research.Application.Features.CreateMission;
using Acmp.Modules.Research.Application.Features.ManageFindings;
using Acmp.Modules.Research.Application.Features.MissionLifecycle;
using Acmp.Modules.Research.Domain;
using Acmp.Modules.Research.Domain.Enums;
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

    // DW-017 — OWNED-CHILD operations must enrich too, and before this they did not.
    //
    // THE GAP THIS PROVES CLOSED: Finding and Recommendation were BaseEntity, and
    // AuditCaptureInterceptor only walks ChangeTracker.Entries<AuditableEntity>() — so a child add or
    // change was invisible to the capture, while the parent mission's own scalars were UNCHANGED and
    // therefore captured nothing either. The result was an audit row that recorded THAT something
    // happened with EMPTY before/after. ⚠ That is weaker than it looks and is why nothing caught it:
    // INV-005 still held (a row WAS emitted for every child state change), so a check that only asked
    // "is there an audit event" passed the whole time.
    //
    // The subject assertions are half the point. The capture is drained by (subjectType, subjectId),
    // so an emit subjected to the MISSION could never collect a FINDING's diff no matter how the
    // entity was mapped — making the child the subject is what connects the two.
    [Fact]
    public async Task Owned_child_operations_write_enriched_audit_rows_subjected_to_the_child()
    {
        var name = "res-child-audit-" + Guid.NewGuid();
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

        var created = await new CreateMissionHandler(db, new ResearchKeyGenerator(db), user, clock, sink,
                Substitute.For<Acmp.Shared.Contracts.Topics.ITopicReader>(),
                Substitute.For<Acmp.Shared.Contracts.Traceability.ITraceabilityWriter>())
            .Handle(new CreateMissionCommand(L("Title"), L("Question"), null, null), CancellationToken.None);

        // Findings may only be added to an ACTIVE mission (the aggregate refuses while Proposed), so
        // the activation is a fixture precondition rather than part of what is under test.
        await new ActivateMissionHandler(db, clock, sink)
            .Handle(new ActivateMissionCommand(created.Id), CancellationToken.None);

        // ADD → the Finding is Added, so the capture is After-only.
        await new AddFindingHandler(db, sink)
            .Handle(new AddFindingCommand(created.Id, L("Cache hit rate collapses"), L("Detail"), Confidence.Medium),
                CancellationToken.None);

        // Read the child's identity from the STORE rather than from the audit row we are about to
        // assert on — otherwise the correlation check would be comparing the row to itself.
        var findingId = (await db.Missions.Include(m => m.Findings).SingleAsync()).Findings.Single().PublicId;

        // VERIFY → IsVerified false→true, so the capture must carry Before AND After.
        await new VerifyFindingHandler(db, sink)
            .Handle(new VerifyFindingCommand(created.Id, findingId), CancellationToken.None);

        var rows = await auditDb.AuditEvents.OrderBy(e => e.Sequence).ToListAsync();

        var added = rows.Single(r => r.Action == "Research.FindingAdded");
        added.SubjectType.Should().Be(nameof(Finding), "the subject is the child that actually changed");
        added.SubjectId.Should().Be(findingId.ToString());
        added.AfterJson.Should().NotBeNull("this is the enrichment DW-017 was raised for");
        added.AfterJson.Should().Contain("\"IsVerified\":false");
        added.BeforeJson.Should().BeNull("an insert has no prior state");

        var verified = rows.Single(r => r.Action == "Research.FindingVerified");
        verified.SubjectType.Should().Be(nameof(Finding));
        verified.SubjectId.Should().Be(findingId.ToString());
        verified.BeforeJson.Should().NotBeNull();
        verified.BeforeJson.Should().Contain("\"IsVerified\":false");
        verified.AfterJson.Should().NotBeNull();
        verified.AfterJson.Should().Contain("\"IsVerified\":true",
            "the audit now records WHAT changed, not merely that something did");
    }
}
