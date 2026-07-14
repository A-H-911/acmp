using Acmp.Modules.Knowledge.Application.Contracts;
using Acmp.Modules.Knowledge.Application.Features.CreateTemplate;
using Acmp.Modules.Knowledge.Application.Features.DeprecateTemplate;
using Acmp.Modules.Knowledge.Application.Features.EditTemplate;
using Acmp.Modules.Knowledge.Application.Features.GetTemplateByKey;
using Acmp.Modules.Knowledge.Application.Features.GetTemplatesRegister;
using Acmp.Modules.Knowledge.Domain.Enums;
using Acmp.Modules.Knowledge.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Knowledge;

// Round-trips through the real KnowledgeDbContext (InMemory): EF mapping of the flat Template table + the bilingual
// owned Name; the key generator (TPL-); the P15d-2 command flow (create, edit, deprecate) and the register/detail
// reads. Audit emits are captured with a recording fake; the before/after ENRICHMENT is proven in
// KnowledgeAuditEnrichmentTests (guardrail 1).
public class TemplateHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static LocalizedString L(string s = "x") => LocalizedString.Create(s, s);

    private static KnowledgeDbContext Db(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<KnowledgeDbContext>().UseInMemoryDatabase("kn-" + Guid.NewGuid()).Options, clock, user);

    private static ICurrentUser User(string sub = "kc-sec")
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns(sub);
        u.DisplayName.Returns("Secretary");
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

    private static async Task<TemplateSummaryDto> CreateAsync(KnowledgeDbContext db, IClock clock, IAuditSink? audit = null,
        TemplateTargetType type = TemplateTargetType.Topic) =>
        await new CreateTemplateHandler(db, new KnowledgeKeyGenerator(db), clock, audit ?? Substitute.For<IAuditSink>())
            .Handle(new CreateTemplateCommand(L("Intake"), type, "# {{title}}"), CancellationToken.None);

    // ── Create / edit ────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Create_makes_an_active_template_with_a_key_and_audits()
    {
        var clock = Clock(Now);
        await using var db = Db(User(), clock);
        var audit = new RecordingAudit();

        var summary = await CreateAsync(db, clock, audit, TemplateTargetType.Adr);

        summary.Key.Should().Be("TPL-2026-001");
        summary.Status.Should().Be("Active");
        summary.TargetType.Should().Be("Adr");
        summary.Version.Should().Be(1);
        audit.Events.Should().Contain("Knowledge.TemplateCreated");

        (await db.Templates.SingleAsync()).Name.En.Should().Be("Intake");
    }

    [Fact]
    public async Task Edit_bumps_version_and_audits()
    {
        var clock = Clock(Now);
        await using var db = Db(User(), clock);
        var created = await CreateAsync(db, clock);
        var audit = new RecordingAudit();

        var summary = await new EditTemplateHandler(db, clock, audit)
            .Handle(new EditTemplateCommand(created.Id, L("New"), "# new"), CancellationToken.None);

        summary.Version.Should().Be(2);
        var template = await db.Templates.SingleAsync();
        template.Name.En.Should().Be("New");
        template.Body.Should().Be("# new");
        audit.Events.Should().Contain("Knowledge.TemplateEdited");
    }

    [Fact]
    public async Task Edit_on_a_missing_template_throws()
    {
        var clock = Clock(Now);
        await using var db = Db(User(), clock);
        var act = () => new EditTemplateHandler(db, clock, Substitute.For<IAuditSink>())
            .Handle(new EditTemplateCommand(Guid.NewGuid(), L(), "b"), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Deprecate ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Deprecate_retires_the_template_and_audits()
    {
        var clock = Clock(Now);
        await using var db = Db(User(), clock);
        var created = await CreateAsync(db, clock);
        var audit = new RecordingAudit();

        await new DeprecateTemplateHandler(db, clock, audit)
            .Handle(new DeprecateTemplateCommand(created.Id), CancellationToken.None);

        (await db.Templates.SingleAsync()).Status.Should().Be(TemplateStatus.Deprecated);
        audit.Events.Should().Contain("Knowledge.TemplateDeprecated");
    }

    [Fact]
    public async Task Deprecate_on_a_missing_template_throws()
    {
        var clock = Clock(Now);
        await using var db = Db(User(), clock);
        var act = () => new DeprecateTemplateHandler(db, clock, Substitute.For<IAuditSink>())
            .Handle(new DeprecateTemplateCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Reads ────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetByKey_returns_the_detail_or_null()
    {
        var clock = Clock(Now);
        await using var db = Db(User(), clock);
        await CreateAsync(db, clock);

        var detail = await new GetTemplateByKeyHandler(db).Handle(new GetTemplateByKeyQuery("TPL-2026-001"), CancellationToken.None);
        detail!.TargetType.Should().Be("Topic");
        detail.Body.Should().Be("# {{title}}");

        (await new GetTemplateByKeyHandler(db).Handle(new GetTemplateByKeyQuery("TPL-2099-999"), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Register_filters_by_status_target_searches_sorts_and_pages()
    {
        var clock = Clock(Now);
        await using var db = Db(User(), clock);
        var first = await CreateAsync(db, clock, type: TemplateTargetType.Topic);   // TPL-2026-001
        await CreateAsync(db, clock, type: TemplateTargetType.Adr);                 // TPL-2026-002
        await new DeprecateTemplateHandler(db, clock, Substitute.For<IAuditSink>())
            .Handle(new DeprecateTemplateCommand(first.Id), CancellationToken.None);

        var handler = new GetTemplatesRegisterHandler(db);

        var active = await handler.Handle(new GetTemplatesRegisterQuery(Statuses: new[] { TemplateStatus.Active }), CancellationToken.None);
        active.Total.Should().Be(1);
        active.Items.Single().Key.Should().Be("TPL-2026-002");

        // The TargetType filter is the P15h seam.
        var adrOnly = await handler.Handle(new GetTemplatesRegisterQuery(TargetType: TemplateTargetType.Adr), CancellationToken.None);
        adrOnly.Total.Should().Be(1);
        adrOnly.Items.Single().TargetType.Should().Be("Adr");

        var byKey = await handler.Handle(new GetTemplatesRegisterQuery(Search: "TPL-2026-002", SortBy: "key", SortDir: "asc"), CancellationToken.None);
        byKey.Items.Single().Key.Should().Be("TPL-2026-002");

        var page = await handler.Handle(new GetTemplatesRegisterQuery(SortBy: "name", PageSize: 1, Page: 1), CancellationToken.None);
        page.Total.Should().Be(2);
        page.Items.Should().ContainSingle();

        var byStatus = await handler.Handle(new GetTemplatesRegisterQuery(SortBy: "status", SortDir: "asc"), CancellationToken.None);
        byStatus.Items.First().Status.Should().Be("Active"); // Active (1) sorts before Deprecated (2)

        var byTarget = await handler.Handle(new GetTemplatesRegisterQuery(SortBy: "target", SortDir: "asc"), CancellationToken.None);
        byTarget.Items.First().TargetType.Should().Be("Topic"); // Topic (1) before Adr (2)
    }

    // ── Validators ───────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Validators_reject_missing_required_fields()
    {
        new CreateTemplateValidator().Validate(new CreateTemplateCommand(L(), TemplateTargetType.Topic, "b")).IsValid.Should().BeTrue();
        new CreateTemplateValidator().Validate(new CreateTemplateCommand(new LocalizedString("en", ""), TemplateTargetType.Topic, "b")).IsValid.Should().BeFalse();
        new CreateTemplateValidator().Validate(new CreateTemplateCommand(L(), TemplateTargetType.Topic, "")).IsValid.Should().BeFalse();
        new CreateTemplateValidator().Validate(new CreateTemplateCommand(L(), (TemplateTargetType)99, "b")).IsValid.Should().BeFalse();

        new EditTemplateValidator().Validate(new EditTemplateCommand(Guid.NewGuid(), L(), "b")).IsValid.Should().BeTrue();
        new EditTemplateValidator().Validate(new EditTemplateCommand(Guid.Empty, L(), "b")).IsValid.Should().BeFalse();
        new EditTemplateValidator().Validate(new EditTemplateCommand(Guid.NewGuid(), L(), "")).IsValid.Should().BeFalse();
    }
}
