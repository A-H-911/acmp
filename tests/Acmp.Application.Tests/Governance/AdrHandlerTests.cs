using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Features.ApproveAdr;
using Acmp.Modules.Governance.Application.Features.ChangeAdrStatus;
using Acmp.Modules.Governance.Application.Features.CreateAdr;
using Acmp.Modules.Governance.Application.Features.GetAdrByKey;
using Acmp.Modules.Governance.Application.Features.GetAdrsRegister;
using Acmp.Modules.Governance.Application.Features.ProposeAdr;
using Acmp.Modules.Governance.Application.Features.SupersedeAdr;
using Acmp.Modules.Governance.Application.Features.UpdateAdrDraft;
using Acmp.Modules.Governance.Domain.Enums;
using Acmp.Modules.Governance.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Governance;

// Round-trips through the real GovernanceDbContext (InMemory): EF mapping incl. the owned option collection +
// nullable LocalizedString columns; the key generator; the full W17/W21 ADR command flow — draft, edit,
// propose (notify reviewers), approve (SoD-soft audit + committee fan-out), request-changes, deprecate,
// supersede (author + approve + link the prior), and the register/detail reads.
public class AdrHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly LocalizedString Title = LocalizedString.Create("Adopt Keycloak", "اعتماد Keycloak");
    private static readonly LocalizedString Context = LocalizedString.Create("We need OIDC", "نحتاج OIDC");
    private static readonly LocalizedString Decision = LocalizedString.Create("Use Keycloak", "استخدام Keycloak");
    private static readonly LocalizedString Reason = LocalizedString.Create("Newer IdP available", "توفر هوية أحدث");

    private static GovernanceDbContext Db(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<GovernanceDbContext>().UseInMemoryDatabase("gov-" + Guid.NewGuid()).Options, clock, user);

    private static ICurrentUser User(string sub = "kc-author", string name = "Author")
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

    private sealed class RecordingChannel : INotificationChannel
    {
        public List<NotificationMessage> Sent { get; } = new();
        public Task PublishAsync(NotificationMessage m, CancellationToken ct = default) { Sent.Add(m); return Task.CompletedTask; }
    }

    private sealed class RecordingAudit : IAuditSink
    {
        public List<(string Event, string? Sub, object? Data)> Calls { get; } = new();
        public Task EmitAsync(string e, string? s, object? d = null, CancellationToken ct = default) { Calls.Add((e, s, d)); return Task.CompletedTask; }
        public Task EmitEnrichedAsync(string action, string subjectType, string? subjectId, string outcome = "Success", CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCommittee : ICommitteeDirectory
    {
        private readonly CommitteeRecipient[] _all;
        private readonly Dictionary<string, CommitteeRecipient[]> _byRole;
        public FakeCommittee(CommitteeRecipient[] all, Dictionary<string, CommitteeRecipient[]>? byRole = null)
            => (_all, _byRole) = (all, byRole ?? new());
        public Task<IReadOnlyCollection<CommitteeRecipient>> GetActiveMembersAsync(CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyCollection<CommitteeRecipient>)_all);
        public Task<IReadOnlyCollection<CommitteeRecipient>> GetActiveMembersInRoleAsync(string role, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyCollection<CommitteeRecipient>)(_byRole.TryGetValue(role, out var r) ? r : Array.Empty<CommitteeRecipient>()));
    }

    private static IReadOnlyList<AdrOptionRequest> Opts() => new[]
    {
        new AdrOptionRequest(LocalizedString.Create("Keycloak", "Keycloak"), LocalizedString.Create("Chosen", "مختار"), true),
        new AdrOptionRequest(LocalizedString.Create("Auth0", "Auth0"), null, false),
    };

    // Fresh LocalizedString copies per command: EF keys owned value objects by their owner, so the SAME CLR
    // instance cannot be attached to two ADR aggregates in one context (prod deserializes fresh per request).
    private static CreateAdrCommand CreateCmd() =>
        new(Title with { }, Context with { }, null, Decision with { }, null, null, Opts());

    private static async Task<AdrSummaryDto> CreateAsync(GovernanceDbContext db, ICurrentUser user, IClock clock,
        IAuditSink? audit = null) =>
        await new CreateAdrHandler(db, new AdrKeyGenerator(db), user, clock, audit ?? Substitute.For<IAuditSink>())
            .Handle(CreateCmd(), CancellationToken.None);

    // ── Create ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Create_drafts_an_adr_with_a_key_persists_options_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var audit = new RecordingAudit();

        var summary = await CreateAsync(db, user, clock, audit);

        summary.Key.Should().Be("ADR-2026-001");
        summary.Status.Should().Be("Draft");
        summary.AuthorName.Should().Be("Author");
        audit.Calls.Should().Contain(c => c.Event == "Governance.AdrDrafted");

        var stored = await db.Adrs.Include(a => a.Options).SingleAsync();
        stored.Options.Should().HaveCount(2);
    }

    // ── Update draft ─────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Update_draft_replaces_content_and_options()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var created = await CreateAsync(db, user, clock);

        var summary = await new UpdateAdrDraftHandler(db, user, Substitute.For<IAuditSink>()).Handle(
            new UpdateAdrDraftCommand(created.Id, Title with { }, Context with { }, LocalizedString.Create("d", "د"), Decision with { }, null, null,
                new[] { new AdrOptionRequest(LocalizedString.Create("Only", "فقط"), null, true) }), CancellationToken.None);

        summary.Key.Should().Be("ADR-2026-001");
        var stored = await db.Adrs.Include(a => a.Options).SingleAsync();
        stored.Options.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_draft_on_a_missing_adr_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new UpdateAdrDraftHandler(db, user, Substitute.For<IAuditSink>()).Handle(
            new UpdateAdrDraftCommand(Guid.NewGuid(), Title, Context, null, Decision, null, null, null), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Propose ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Propose_advances_to_proposed_and_notifies_reviewers_skipping_the_proposer()
    {
        var user = User("kc-author"); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var created = await CreateAsync(db, user, clock);

        var channel = new RecordingChannel();
        var committee = new FakeCommittee(Array.Empty<CommitteeRecipient>(), new()
        {
            ["Reviewer"] = new[] { new CommitteeRecipient("kc-rev1", "R1"), new CommitteeRecipient("kc-author", "Author"), new CommitteeRecipient("kc-rev2", "R2") },
        });

        await new ProposeAdrHandler(db, clock, new RecordingAudit(), user, committee, channel)
            .Handle(new ProposeAdrCommand(created.Id), CancellationToken.None);

        (await db.Adrs.SingleAsync()).Status.Should().Be(AdrStatus.Proposed);
        channel.Sent.Select(m => m.RecipientUserId).Should().BeEquivalentTo("kc-rev1", "kc-rev2");
    }

    // ── Approve (SoD-soft) ───────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Approve_moves_to_approved_records_sod_soft_and_fans_out_to_the_committee()
    {
        var author = User("kc-author"); var clock = Clock(Now);
        await using var db = Db(author, clock);
        var created = await CreateAsync(db, author, clock);
        await new ProposeAdrHandler(db, clock, new RecordingAudit(), author,
            new FakeCommittee(Array.Empty<CommitteeRecipient>()), new RecordingChannel())
            .Handle(new ProposeAdrCommand(created.Id), CancellationToken.None);

        // The author approves their own ADR (SoD SOFT: allowed, but recorded).
        var audit = new RecordingAudit();
        var channel = new RecordingChannel();
        var committee = new FakeCommittee(new[]
        {
            new CommitteeRecipient("kc-author", "Author"), new CommitteeRecipient("kc-m1", "M1"), new CommitteeRecipient("kc-m2", "M2"),
        });

        await new ApproveAdrHandler(db, clock, audit, author, committee, channel)
            .Handle(new ApproveAdrCommand(created.Id), CancellationToken.None);

        (await db.Adrs.SingleAsync()).Status.Should().Be(AdrStatus.Approved);
        var data = audit.Calls.Single(c => c.Event == "Governance.AdrApproved").Data!;
        data.GetType().GetProperty("AuthorApprovedSelf")!.GetValue(data).Should().Be(true);
        // Fan-out reaches the other members, not the approver.
        channel.Sent.Select(m => m.RecipientUserId).Should().BeEquivalentTo("kc-m1", "kc-m2");
    }

    // ── Request changes ──────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Request_changes_returns_a_proposed_adr_to_draft()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var created = await CreateAsync(db, user, clock);
        await new ProposeAdrHandler(db, clock, new RecordingAudit(), user,
            new FakeCommittee(Array.Empty<CommitteeRecipient>()), new RecordingChannel())
            .Handle(new ProposeAdrCommand(created.Id), CancellationToken.None);

        await new RequestAdrChangesHandler(db, clock, new RecordingAudit(), user)
            .Handle(new RequestAdrChangesCommand(created.Id), CancellationToken.None);

        (await db.Adrs.SingleAsync()).Status.Should().Be(AdrStatus.Draft);
    }

    [Fact]
    public async Task Request_changes_on_a_missing_adr_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new RequestAdrChangesHandler(db, clock, new RecordingAudit(), user)
            .Handle(new RequestAdrChangesCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Deprecate ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Deprecate_retires_an_approved_adr_and_notifies_the_committee()
    {
        var user = User("kc-chair", "Chair"); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var id = await ApprovedIdAsync(db, user, clock);

        var channel = new RecordingChannel();
        await new DeprecateAdrHandler(db, clock, new RecordingAudit(), user,
            new FakeCommittee(new[] { new CommitteeRecipient("kc-m1", "M1") }), channel)
            .Handle(new DeprecateAdrCommand(id, Reason), CancellationToken.None);

        (await db.Adrs.SingleAsync()).Status.Should().Be(AdrStatus.Deprecated);
        channel.Sent.Should().ContainSingle().Which.RecipientUserId.Should().Be("kc-m1");
    }

    // ── Supersede ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Supersede_authors_a_new_approved_adr_and_freezes_the_prior_with_both_links()
    {
        var user = User("kc-chair", "Chair"); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var priorId = await ApprovedIdAsync(db, user, clock);
        var priorKey = (await db.Adrs.SingleAsync()).Key;

        var channel = new RecordingChannel();
        var audit = new RecordingAudit();
        var successor = await new SupersedeAdrHandler(db, new AdrKeyGenerator(db), user, clock, audit,
            new FakeCommittee(new[] { new CommitteeRecipient("kc-m1", "M1"), new CommitteeRecipient("kc-chair", "Chair") }), channel)
            .Handle(new SupersedeAdrCommand(priorId, Title with { }, Context with { }, null, Decision with { }, null, null, Opts(), Reason with { }), CancellationToken.None);

        successor.Status.Should().Be("Approved");
        var prior = await db.Adrs.FirstAsync(a => a.Key == priorKey);
        prior.Status.Should().Be(AdrStatus.Superseded);
        prior.SupersededByAdrId.Should().Be(successor.Id);

        // The born-Approved successor records its own approval, and the prior records the supersede — both audited.
        audit.Calls.Should().Contain(c => c.Event == "Governance.AdrApproved");
        audit.Calls.Should().Contain(c => c.Event == "Governance.AdrSuperseded");

        // Detail resolves peer keys in both directions.
        var priorDetail = await new GetAdrByKeyHandler(db).Handle(new GetAdrByKeyQuery(priorKey), CancellationToken.None);
        priorDetail!.SupersededByAdrKey.Should().Be(successor.Key);
        var succDetail = await new GetAdrByKeyHandler(db).Handle(new GetAdrByKeyQuery(successor.Key), CancellationToken.None);
        succDetail!.SupersedesAdrKey.Should().Be(priorKey);

        channel.Sent.Select(m => m.RecipientUserId).Should().BeEquivalentTo("kc-m1"); // skips the actor
    }

    // ── Reads ────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetByKey_returns_null_for_an_unknown_key()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        (await new GetAdrByKeyHandler(db).Handle(new GetAdrByKeyQuery("ADR-2099-999"), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Register_filters_by_status_searches_sorts_and_pages()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        await CreateAsync(db, user, clock); // ADR-2026-001 (Draft)
        var second = await CreateAsync(db, user, clock); // ADR-2026-002 (Draft)
        await new ProposeAdrHandler(db, clock, new RecordingAudit(), user,
            new FakeCommittee(Array.Empty<CommitteeRecipient>()), new RecordingChannel())
            .Handle(new ProposeAdrCommand(second.Id), CancellationToken.None);

        var handler = new GetAdrsRegisterHandler(db);

        var proposed = await handler.Handle(new GetAdrsRegisterQuery(Statuses: new[] { AdrStatus.Proposed }), CancellationToken.None);
        proposed.Total.Should().Be(1);
        proposed.Items.Single().Key.Should().Be("ADR-2026-002");

        var byKey = await handler.Handle(new GetAdrsRegisterQuery(Search: "ADR-2026-001", SortBy: "key", SortDir: "asc"), CancellationToken.None);
        byKey.Items.Single().Key.Should().Be("ADR-2026-001");

        var page = await handler.Handle(new GetAdrsRegisterQuery(SortBy: "title", PageSize: 1, Page: 1), CancellationToken.None);
        page.Total.Should().Be(2);
        page.Items.Should().ContainSingle();

        var byStatus = await handler.Handle(new GetAdrsRegisterQuery(SortBy: "status", SortDir: "asc"), CancellationToken.None);
        byStatus.Total.Should().Be(2);
        byStatus.Items.First().Status.Should().Be("Draft"); // Draft (1) sorts before Proposed (2)
    }

    // ── Validators ───────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Validators_reject_missing_required_bilingual_fields()
    {
        new CreateAdrValidator().Validate(CreateCmd()).IsValid.Should().BeTrue();
        new CreateAdrValidator().Validate(CreateCmd() with { Title = new LocalizedString("EN", "") }).IsValid.Should().BeFalse();
        new CreateAdrValidator().Validate(CreateCmd() with { Context = new LocalizedString("", "ar") }).IsValid.Should().BeFalse();
        new CreateAdrValidator().Validate(CreateCmd() with { DecisionText = new LocalizedString("en", "") }).IsValid.Should().BeFalse();
        new CreateAdrValidator().Validate(CreateCmd() with { Options = new[] { new AdrOptionRequest(new LocalizedString("en", ""), null, true) } }).IsValid.Should().BeFalse();

        new UpdateAdrDraftValidator().Validate(new UpdateAdrDraftCommand(Guid.NewGuid(), Title, Context, null, Decision, null, null, null)).IsValid.Should().BeTrue();
        new UpdateAdrDraftValidator().Validate(new UpdateAdrDraftCommand(Guid.Empty, Title, Context, null, Decision, null, null, null)).IsValid.Should().BeFalse();

        new DeprecateAdrValidator().Validate(new DeprecateAdrCommand(Guid.NewGuid(), Reason)).IsValid.Should().BeTrue();
        new DeprecateAdrValidator().Validate(new DeprecateAdrCommand(Guid.NewGuid(), new LocalizedString("en", ""))).IsValid.Should().BeFalse();

        new SupersedeAdrValidator().Validate(new SupersedeAdrCommand(Guid.NewGuid(), Title, Context, null, Decision, null, null, Opts(), Reason)).IsValid.Should().BeTrue();
        new SupersedeAdrValidator().Validate(new SupersedeAdrCommand(Guid.Empty, Title, Context, null, Decision, null, null, null, Reason)).IsValid.Should().BeFalse();
        new SupersedeAdrValidator().Validate(new SupersedeAdrCommand(Guid.NewGuid(), Title, Context, null, Decision, null, null, null, new LocalizedString("en", ""))).IsValid.Should().BeFalse();
    }

    // Helper: create → propose → approve, returning the approved ADR's PublicId.
    private static async Task<Guid> ApprovedIdAsync(GovernanceDbContext db, ICurrentUser user, IClock clock)
    {
        var created = await CreateAsync(db, user, clock);
        var noCommittee = new FakeCommittee(Array.Empty<CommitteeRecipient>());
        await new ProposeAdrHandler(db, clock, new RecordingAudit(), user, noCommittee, new RecordingChannel())
            .Handle(new ProposeAdrCommand(created.Id), CancellationToken.None);
        await new ApproveAdrHandler(db, clock, new RecordingAudit(), user, noCommittee, new RecordingChannel())
            .Handle(new ApproveAdrCommand(created.Id), CancellationToken.None);
        return created.Id;
    }
}
