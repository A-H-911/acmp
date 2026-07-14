using Acmp.Modules.Knowledge.Application.Contracts;
using Acmp.Modules.Knowledge.Application.Features.CreateDocument;
using Acmp.Modules.Knowledge.Application.Features.DocumentLifecycle;
using Acmp.Modules.Knowledge.Application.Features.EditDocument;
using Acmp.Modules.Knowledge.Application.Features.GetDocumentByKey;
using Acmp.Modules.Knowledge.Application.Features.GetDocumentsRegister;
using Acmp.Modules.Knowledge.Domain.Enums;
using Acmp.Modules.Knowledge.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Knowledge;

// Round-trips through the real KnowledgeDbContext (InMemory): EF mapping incl. the owned DocumentVersion
// collection + JSON tags; the key generator; the full P15d command flow (create, edit, publish, archive) and
// the register/detail reads. Audit emits are captured with a recording fake; the before/after ENRICHMENT is
// proven separately in KnowledgeAuditEnrichmentTests (guardrail 1).
public class KnowledgeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static LocalizedString L(string s = "x") => LocalizedString.Create(s, s);

    private static KnowledgeDbContext Db(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<KnowledgeDbContext>().UseInMemoryDatabase("kn-" + Guid.NewGuid()).Options, clock, user);

    private static ICurrentUser User(string sub = "kc-owner", string name = "Owner")
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns(sub);
        u.DisplayName.Returns(name);
        return u;
    }

    private static IClock Clock(DateTimeOffset now)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(now);
        return c;
    }

    private sealed class RecordingAudit : IAuditSink
    {
        public List<string> Events { get; } = new();
        public Task EmitAsync(string e, string? s, object? d = null, CancellationToken ct = default) { Events.Add(e); return Task.CompletedTask; }
        public Task EmitEnrichedAsync(string action, string subjectType, string? subjectId, string outcome = "Success", CancellationToken ct = default) { Events.Add(action); return Task.CompletedTask; }
    }

    private static async Task<DocumentSummaryDto> CreateAsync(KnowledgeDbContext db, ICurrentUser user, IClock clock, IAuditSink? audit = null) =>
        await new CreateDocumentHandler(db, new KnowledgeKeyGenerator(db), user, clock, audit ?? Substitute.For<IAuditSink>())
            .Handle(new CreateDocumentCommand(L("Title"), "Guides", L("Body"), new[] { "wiki" }), CancellationToken.None);

    // ── Create / edit ────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Create_drafts_a_document_with_a_key_v1_snapshot_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var audit = new RecordingAudit();

        var summary = await CreateAsync(db, user, clock, audit);

        summary.Key.Should().Be("DOC-2026-001");
        summary.Status.Should().Be("Draft");
        summary.Version.Should().Be(1);
        summary.Tags.Should().BeEquivalentTo("wiki");
        audit.Events.Should().Contain("Knowledge.DocumentCreated");

        var persisted = await db.Documents.Include(d => d.Versions).SingleAsync();
        persisted.OwnerUserId.Should().Be("kc-owner");
        persisted.Versions.Should().ContainSingle();
    }

    [Fact]
    public async Task Edit_bumps_version_appends_snapshot_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var created = await CreateAsync(db, user, clock);
        var audit = new RecordingAudit();

        var summary = await new EditDocumentHandler(db, User("kc-editor", "Editor"), clock, audit)
            .Handle(new EditDocumentCommand(created.Id, L("New"), "Playbooks", L("NewBody")), CancellationToken.None);

        summary.Version.Should().Be(2);
        var doc = await db.Documents.Include(d => d.Versions).SingleAsync();
        doc.Title.En.Should().Be("New");
        doc.Category.Should().Be("Playbooks");
        doc.Versions.Should().HaveCount(2);
        audit.Events.Should().Contain("Knowledge.DocumentEdited");
    }

    [Fact]
    public async Task Edit_on_a_missing_document_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new EditDocumentHandler(db, user, clock, Substitute.For<IAuditSink>())
            .Handle(new EditDocumentCommand(Guid.NewGuid(), L(), "Cat", L()), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Publish_then_archive_walks_the_lifecycle_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var created = await CreateAsync(db, user, clock);
        var audit = new RecordingAudit();

        await new PublishDocumentHandler(db, clock, audit).Handle(new PublishDocumentCommand(created.Id), CancellationToken.None);
        (await db.Documents.SingleAsync()).Status.Should().Be(DocumentStatus.Published);

        await new ArchiveDocumentHandler(db, clock, audit).Handle(new ArchiveDocumentCommand(created.Id), CancellationToken.None);
        (await db.Documents.SingleAsync()).Status.Should().Be(DocumentStatus.Archived);

        audit.Events.Should().Contain("Knowledge.DocumentPublished").And.Contain("Knowledge.DocumentArchived");
    }

    [Fact]
    public async Task Publish_on_a_missing_document_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new PublishDocumentHandler(db, clock, Substitute.For<IAuditSink>())
            .Handle(new PublishDocumentCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Archive_on_a_missing_document_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new ArchiveDocumentHandler(db, clock, Substitute.For<IAuditSink>())
            .Handle(new ArchiveDocumentCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Reads ────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetByKey_returns_the_detail_with_versions_or_null()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var created = await CreateAsync(db, user, clock);
        await new EditDocumentHandler(db, user, clock, Substitute.For<IAuditSink>())
            .Handle(new EditDocumentCommand(created.Id, L("v2"), "Guides", L("b2")), CancellationToken.None);

        var detail = await new GetDocumentByKeyHandler(db).Handle(new GetDocumentByKeyQuery("DOC-2026-001"), CancellationToken.None);
        detail!.Version.Should().Be(2);
        detail.Versions.Should().HaveCount(2);
        detail.Versions.First().Version.Should().Be(1); // ordered ascending
        detail.Body.En.Should().Be("b2");

        (await new GetDocumentByKeyHandler(db).Handle(new GetDocumentByKeyQuery("DOC-2099-999"), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Register_filters_by_status_and_category_searches_sorts_and_pages()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var first = await CreateAsync(db, user, clock);   // DOC-2026-001 (Draft, Guides)
        await CreateAsync(db, user, clock);               // DOC-2026-002 (Draft, Guides)
        await new PublishDocumentHandler(db, clock, Substitute.For<IAuditSink>())
            .Handle(new PublishDocumentCommand(first.Id), CancellationToken.None);

        var handler = new GetDocumentsRegisterHandler(db);

        var published = await handler.Handle(new GetDocumentsRegisterQuery(Statuses: new[] { DocumentStatus.Published }), CancellationToken.None);
        published.Total.Should().Be(1);
        published.Items.Single().Key.Should().Be("DOC-2026-001");

        var byCategory = await handler.Handle(new GetDocumentsRegisterQuery(Category: "Guides"), CancellationToken.None);
        byCategory.Total.Should().Be(2);

        var byKey = await handler.Handle(new GetDocumentsRegisterQuery(Search: "DOC-2026-002", SortBy: "key", SortDir: "asc"), CancellationToken.None);
        byKey.Items.Single().Key.Should().Be("DOC-2026-002");

        var page = await handler.Handle(new GetDocumentsRegisterQuery(SortBy: "title", PageSize: 1, Page: 1), CancellationToken.None);
        page.Total.Should().Be(2);
        page.Items.Should().ContainSingle();

        var byStatus = await handler.Handle(new GetDocumentsRegisterQuery(SortBy: "status", SortDir: "asc"), CancellationToken.None);
        byStatus.Items.First().Status.Should().Be("Draft"); // Draft (1) sorts before Published (2)
    }

    // ── Validators ───────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Validators_reject_missing_required_fields()
    {
        new CreateDocumentValidator().Validate(new CreateDocumentCommand(L(), "Cat", L(), null)).IsValid.Should().BeTrue();
        new CreateDocumentValidator().Validate(new CreateDocumentCommand(new LocalizedString("en", ""), "Cat", L(), null)).IsValid.Should().BeFalse();
        new CreateDocumentValidator().Validate(new CreateDocumentCommand(L(), "Cat", new LocalizedString("", "ar"), null)).IsValid.Should().BeFalse();
        new CreateDocumentValidator().Validate(new CreateDocumentCommand(L(), "", L(), null)).IsValid.Should().BeFalse();

        new EditDocumentValidator().Validate(new EditDocumentCommand(Guid.NewGuid(), L(), "Cat", L())).IsValid.Should().BeTrue();
        new EditDocumentValidator().Validate(new EditDocumentCommand(Guid.Empty, L(), "Cat", L())).IsValid.Should().BeFalse();
        new EditDocumentValidator().Validate(new EditDocumentCommand(Guid.NewGuid(), L(), "", L())).IsValid.Should().BeFalse();
    }
}
