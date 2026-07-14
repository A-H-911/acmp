using Acmp.Modules.Knowledge.Application.Features.CreateDocument;
using Acmp.Modules.Knowledge.Application.Features.DocumentLifecycle;
using Acmp.Modules.Knowledge.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acmp.Application.Tests.Knowledge;

// INV-005 / guardrail 1: proves the Knowledge module actually produces ENRICHED (before/after populated) audit
// rows — not silently lean ones. It wires the SAME collaborators AddKnowledgeModule wires in production: the
// AuditCaptureInterceptor (attached to KnowledgeDbContext) records the document's scalar deltas into a shared
// AuditChangeBuffer on SaveChanges, and the real SqlAuditSink drains that buffer by (subjectType, subjectId)
// when the handler emits its governance event. A create captures After; a publish captures both Before and After.
public class KnowledgeAuditEnrichmentTests
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
    public async Task Document_lifecycle_writes_enriched_audit_rows_with_before_and_after()
    {
        var name = "kn-audit-" + Guid.NewGuid();
        var buffer = new AuditChangeBuffer();
        var interceptor = new AuditCaptureInterceptor(buffer);
        var clock = new FixedClock();
        var user = User();

        await using var auditDb = new AuditDbContext(
            new DbContextOptionsBuilder<AuditDbContext>().UseInMemoryDatabase(name + "-audit").Options);
        var sink = new SqlAuditSink(auditDb, clock, user, buffer, NullLogger<SqlAuditSink>.Instance);

        await using var db = new KnowledgeDbContext(
            new DbContextOptionsBuilder<KnowledgeDbContext>().UseInMemoryDatabase(name).AddInterceptors(interceptor).Options,
            clock, user);

        // Create → the document is Added, so the capture is After-only.
        var created = await new CreateDocumentHandler(db, new KnowledgeKeyGenerator(db), user, clock, sink)
            .Handle(new CreateDocumentCommand(L("Title"), "Guides", L("Body"), null), CancellationToken.None);

        // Publish → the document is Modified (Status), so the capture has Before AND After.
        await new PublishDocumentHandler(db, clock, sink)
            .Handle(new PublishDocumentCommand(created.Id), CancellationToken.None);

        var rows = await auditDb.AuditEvents.OrderBy(e => e.Sequence).ToListAsync();

        // Status serializes as its numeric value (Draft=1, Published=2 — System.Text.Json default for enums).
        var createdRow = rows.Single(r => r.Action == "Knowledge.DocumentCreated");
        createdRow.SubjectType.Should().Be("Document");
        createdRow.SubjectId.Should().Be(created.Id.ToString());
        createdRow.BeforeJson.Should().BeNull("an insert has no prior state");
        createdRow.AfterJson.Should().NotBeNull();
        createdRow.AfterJson.Should().Contain("\"Status\":1");
        createdRow.ActorUserId.Should().Be("kc-owner");

        var publishedRow = rows.Single(r => r.Action == "Knowledge.DocumentPublished");
        publishedRow.BeforeJson.Should().NotBeNull();
        publishedRow.BeforeJson.Should().Contain("\"Status\":1");
        publishedRow.AfterJson.Should().NotBeNull();
        publishedRow.AfterJson.Should().Contain("\"Status\":2");
    }
}
