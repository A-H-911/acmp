using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Features.ApproveInvariant;
using Acmp.Modules.Governance.Application.Features.ChangeInvariantStatus;
using Acmp.Modules.Governance.Application.Features.CreateInvariant;
using Acmp.Modules.Governance.Application.Features.GetInvariantByKey;
using Acmp.Modules.Governance.Application.Features.GetInvariantsRegister;
using Acmp.Modules.Governance.Application.Features.ProposeInvariant;
using Acmp.Modules.Governance.Application.Features.SupersedeInvariant;
using Acmp.Modules.Governance.Application.Features.UpdateInvariantDraft;
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

// Round-trips through the real GovernanceDbContext (InMemory): EF mapping incl. the nullable LocalizedString
// columns + int-backed enums; the key generator (AIV prefix over the shared counter table); the full W18/W21
// invariant command flow — draft, edit, propose (notify reviewers), activate (SoD-soft audit + committee
// fan-out), request-changes, retire, supersede (author + activate + link the prior), and register/detail reads.
public class InvariantHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly LocalizedString Statement = LocalizedString.Create("No cross-module DB access", "لا وصول لقاعدة وحدة أخرى");
    private static readonly LocalizedString Rationale = LocalizedString.Create("Preserves boundaries", "يحافظ على الحدود");
    private static readonly LocalizedString Reason = LocalizedString.Create("Stronger rule available", "توفر قاعدة أقوى");

    private static GovernanceDbContext Db(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<GovernanceDbContext>().UseInMemoryDatabase("gov-inv-" + Guid.NewGuid()).Options, clock, user);

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

    private sealed class RecordingChannel : INotificationChannel
    {
        public List<NotificationMessage> Sent { get; } = new();
        public Task PublishAsync(NotificationMessage m, CancellationToken ct = default) { Sent.Add(m); return Task.CompletedTask; }
    }

    private sealed class RecordingAudit : IAuditSink
    {
        public List<(string Event, string? Sub, object? Data)> Calls { get; } = new();
        public Task EmitAsync(string e, string? s, object? d = null, CancellationToken ct = default) { Calls.Add((e, s, d)); return Task.CompletedTask; }
        public Task EmitEnrichedAsync(string action, string subjectType, string? subjectId, string outcome = "Success", CancellationToken ct = default) { Calls.Add((action, subjectId, null)); return Task.CompletedTask; }
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

    private static CreateInvariantCommand CreateCmd() =>
        new(InvariantCategory.Security, InvariantScope.Platform, Statement with { }, Rationale with { }, null, "kc-owner", "Owner");

    private static async Task<InvariantSummaryDto> CreateAsync(GovernanceDbContext db, ICurrentUser user, IClock clock,
        IAuditSink? audit = null) =>
        await new CreateInvariantHandler(db, new InvariantKeyGenerator(db), user, clock, audit ?? Substitute.For<IAuditSink>())
            .Handle(CreateCmd(), CancellationToken.None);

    // ── Create ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Create_drafts_an_invariant_with_an_aiv_key_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var audit = new RecordingAudit();

        var summary = await CreateAsync(db, user, clock, audit);

        summary.Key.Should().Be("AIV-2026-001");
        summary.Status.Should().Be("Draft");
        summary.Category.Should().Be("Security");
        summary.Scope.Should().Be("Platform");
        summary.OwnerName.Should().Be("Owner");
        audit.Calls.Should().Contain(c => c.Event == "Governance.InvariantDrafted");
    }

    // ── Update draft ─────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Update_draft_replaces_content()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var created = await CreateAsync(db, user, clock);

        var summary = await new UpdateInvariantDraftHandler(db, user, Substitute.For<IAuditSink>()).Handle(
            new UpdateInvariantDraftCommand(created.Id, InvariantCategory.Data, InvariantScope.OrgWide,
                Statement with { }, Rationale with { }, LocalizedString.Create("exc", "استثناء"), "kc-owner2", "Owner2"),
            CancellationToken.None);

        summary.Category.Should().Be("Data");
        var stored = await db.Invariants.SingleAsync();
        stored.Scope.Should().Be(InvariantScope.OrgWide);
        stored.ExceptionsPolicy!.En.Should().Be("exc");
    }

    [Fact]
    public async Task Update_draft_on_a_missing_invariant_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new UpdateInvariantDraftHandler(db, user, Substitute.For<IAuditSink>()).Handle(
            new UpdateInvariantDraftCommand(Guid.NewGuid(), InvariantCategory.Data, InvariantScope.Platform,
                Statement, Rationale, null, "o", "O"), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Propose ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Propose_advances_to_proposed_and_notifies_reviewers_skipping_the_proposer()
    {
        var user = User("kc-owner"); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var created = await CreateAsync(db, user, clock);

        var channel = new RecordingChannel();
        var committee = new FakeCommittee(Array.Empty<CommitteeRecipient>(), new()
        {
            ["Reviewer"] = new[] { new CommitteeRecipient("kc-rev1", "R1"), new CommitteeRecipient("kc-owner", "Owner"), new CommitteeRecipient("kc-rev2", "R2") },
        });

        await new ProposeInvariantHandler(db, clock, new RecordingAudit(), user, committee, channel)
            .Handle(new ProposeInvariantCommand(created.Id), CancellationToken.None);

        (await db.Invariants.SingleAsync()).Status.Should().Be(InvariantStatus.Proposed);
        channel.Sent.Select(m => m.RecipientUserId).Should().BeEquivalentTo("kc-rev1", "kc-rev2");
    }

    [Fact]
    public async Task Propose_on_a_missing_invariant_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new ProposeInvariantHandler(db, clock, new RecordingAudit(), user,
            new FakeCommittee(Array.Empty<CommitteeRecipient>()), new RecordingChannel())
            .Handle(new ProposeInvariantCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Activate (SoD-soft) ──────────────────────────────────────────────────────────────────────────────
    // SoD-soft signal = the AUTHOR (server-derived creator, CreatedBy) approving, NOT the client-supplied
    // Owner field. Here creator/approver = kc-sec, Owner = kc-owner (a third party) → AuthorApprovedSelf is
    // TRUE off the creator match, and the distinct Owner proves we do not key off OwnerUserId.
    [Fact]
    public async Task Activate_records_author_approved_self_when_the_creator_approves_and_fans_out()
    {
        var creator = User("kc-sec", "Sam"); var clock = Clock(Now);
        await using var db = Db(creator, clock);              // CreatedBy = kc-sec
        var created = await CreateAsync(db, creator, clock);  // CreateCmd names Owner = kc-owner (a third party)
        await new ProposeInvariantHandler(db, clock, new RecordingAudit(), creator,
            new FakeCommittee(Array.Empty<CommitteeRecipient>()), new RecordingChannel())
            .Handle(new ProposeInvariantCommand(created.Id), CancellationToken.None);

        var audit = new RecordingAudit();
        var channel = new RecordingChannel();
        var committee = new FakeCommittee(new[]
        {
            new CommitteeRecipient("kc-sec", "Sam"), new CommitteeRecipient("kc-m1", "M1"), new CommitteeRecipient("kc-m2", "M2"),
        });

        await new ApproveInvariantHandler(db, clock, audit, creator, committee, channel)
            .Handle(new ApproveInvariantCommand(created.Id), CancellationToken.None);

        (await db.Invariants.SingleAsync()).Status.Should().Be(InvariantStatus.Active);
        audit.Calls.Should().Contain(c => c.Event == "Governance.InvariantActivated");
        var subject = await db.Invariants.SingleAsync();
        subject.ActivatedByUserId.Should().Be(subject.CreatedBy, "self-approval (SoD-soft)");
        channel.Sent.Select(m => m.RecipientUserId).Should().BeEquivalentTo("kc-m1", "kc-m2");
    }

    // The complement: a DIFFERENT user (not the creator) activates → AuthorApprovedSelf is FALSE, even though
    // the actor differs from the named Owner too. Locks that the flag keys off CreatedBy, not OwnerUserId.
    [Fact]
    public async Task Activate_records_author_approved_self_false_when_a_different_user_approves()
    {
        var creator = User("kc-sec", "Sam"); var clock = Clock(Now);
        await using var db = Db(creator, clock);              // CreatedBy = kc-sec, Owner = kc-owner
        var created = await CreateAsync(db, creator, clock);
        await new ProposeInvariantHandler(db, clock, new RecordingAudit(), creator,
            new FakeCommittee(Array.Empty<CommitteeRecipient>()), new RecordingChannel())
            .Handle(new ProposeInvariantCommand(created.Id), CancellationToken.None);

        var chair = User("kc-chair", "Chair");
        var audit = new RecordingAudit();
        await new ApproveInvariantHandler(db, clock, audit, chair,
            new FakeCommittee(Array.Empty<CommitteeRecipient>()), new RecordingChannel())
            .Handle(new ApproveInvariantCommand(created.Id), CancellationToken.None);

        audit.Calls.Should().Contain(c => c.Event == "Governance.InvariantActivated");
        var subject = await db.Invariants.SingleAsync();
        subject.ActivatedByUserId.Should().NotBe(subject.CreatedBy, "a different user approved");
    }

    [Fact]
    public async Task Approve_on_a_missing_invariant_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new ApproveInvariantHandler(db, clock, new RecordingAudit(), user,
            new FakeCommittee(Array.Empty<CommitteeRecipient>()), new RecordingChannel())
            .Handle(new ApproveInvariantCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Request changes ──────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Request_changes_returns_a_proposed_invariant_to_draft()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var created = await CreateAsync(db, user, clock);
        await new ProposeInvariantHandler(db, clock, new RecordingAudit(), user,
            new FakeCommittee(Array.Empty<CommitteeRecipient>()), new RecordingChannel())
            .Handle(new ProposeInvariantCommand(created.Id), CancellationToken.None);

        await new RequestInvariantChangesHandler(db, clock, new RecordingAudit(), user)
            .Handle(new RequestInvariantChangesCommand(created.Id), CancellationToken.None);

        (await db.Invariants.SingleAsync()).Status.Should().Be(InvariantStatus.Draft);
    }

    [Fact]
    public async Task Request_changes_on_a_missing_invariant_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new RequestInvariantChangesHandler(db, clock, new RecordingAudit(), user)
            .Handle(new RequestInvariantChangesCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Retire ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Retire_takes_an_active_invariant_out_of_force_and_notifies_the_committee()
    {
        var user = User("kc-chair", "Chair"); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var id = await ActiveIdAsync(db, user, clock);

        var channel = new RecordingChannel();
        await new RetireInvariantHandler(db, clock, new RecordingAudit(), user,
            new FakeCommittee(new[] { new CommitteeRecipient("kc-m1", "M1") }), channel)
            .Handle(new RetireInvariantCommand(id, Reason), CancellationToken.None);

        (await db.Invariants.SingleAsync()).Status.Should().Be(InvariantStatus.Retired);
        channel.Sent.Should().ContainSingle().Which.RecipientUserId.Should().Be("kc-m1");
    }

    [Fact]
    public async Task Retire_on_a_missing_invariant_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new RetireInvariantHandler(db, clock, new RecordingAudit(), user,
            new FakeCommittee(Array.Empty<CommitteeRecipient>()), new RecordingChannel())
            .Handle(new RetireInvariantCommand(Guid.NewGuid(), Reason), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Supersede ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Supersede_authors_a_new_active_invariant_and_freezes_the_prior_with_both_links()
    {
        var user = User("kc-chair", "Chair"); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var priorId = await ActiveIdAsync(db, user, clock);
        var priorKey = (await db.Invariants.SingleAsync()).Key;

        var channel = new RecordingChannel();
        var audit = new RecordingAudit();
        var successor = await new SupersedeInvariantHandler(db, new InvariantKeyGenerator(db), user, clock, audit,
            new FakeCommittee(new[] { new CommitteeRecipient("kc-m1", "M1"), new CommitteeRecipient("kc-chair", "Chair") }), channel)
            .Handle(new SupersedeInvariantCommand(priorId, InvariantCategory.Security, InvariantScope.Platform,
                Statement with { }, Rationale with { }, null, "kc-owner", "Owner", Reason with { }), CancellationToken.None);

        successor.Status.Should().Be("Active");
        var prior = await db.Invariants.FirstAsync(a => a.Key == priorKey);
        prior.Status.Should().Be(InvariantStatus.Superseded);
        prior.SupersededByInvariantId.Should().Be(successor.Id);

        // The born-Active successor records its own activation, and the prior records the supersede — both audited.
        audit.Calls.Should().Contain(c => c.Event == "Governance.InvariantActivated");
        audit.Calls.Should().Contain(c => c.Event == "Governance.InvariantSuperseded");

        var priorDetail = await new GetInvariantByKeyHandler(db).Handle(new GetInvariantByKeyQuery(priorKey), CancellationToken.None);
        priorDetail!.SupersededByInvariantKey.Should().Be(successor.Key);
        var succDetail = await new GetInvariantByKeyHandler(db).Handle(new GetInvariantByKeyQuery(successor.Key), CancellationToken.None);
        succDetail!.SupersedesInvariantKey.Should().Be(priorKey);

        channel.Sent.Select(m => m.RecipientUserId).Should().BeEquivalentTo("kc-m1"); // skips the actor
    }

    [Fact]
    public async Task Supersede_on_a_missing_invariant_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new SupersedeInvariantHandler(db, new InvariantKeyGenerator(db), user, clock, new RecordingAudit(),
            new FakeCommittee(Array.Empty<CommitteeRecipient>()), new RecordingChannel())
            .Handle(new SupersedeInvariantCommand(Guid.NewGuid(), InvariantCategory.Data, InvariantScope.Platform,
                Statement, Rationale, null, "o", "O", Reason), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Reads ────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetByKey_returns_null_for_an_unknown_key()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        (await new GetInvariantByKeyHandler(db).Handle(new GetInvariantByKeyQuery("AIV-2099-999"), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Register_filters_by_status_searches_sorts_and_pages()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        await CreateAsync(db, user, clock);          // AIV-2026-001 (Draft)
        var second = await CreateAsync(db, user, clock); // AIV-2026-002 (Draft)
        await new ProposeInvariantHandler(db, clock, new RecordingAudit(), user,
            new FakeCommittee(Array.Empty<CommitteeRecipient>()), new RecordingChannel())
            .Handle(new ProposeInvariantCommand(second.Id), CancellationToken.None);

        var handler = new GetInvariantsRegisterHandler(db);

        var proposed = await handler.Handle(new GetInvariantsRegisterQuery(Statuses: new[] { InvariantStatus.Proposed }), CancellationToken.None);
        proposed.Total.Should().Be(1);
        proposed.Items.Single().Key.Should().Be("AIV-2026-002");

        var byKey = await handler.Handle(new GetInvariantsRegisterQuery(Search: "AIV-2026-001", SortBy: "key", SortDir: "asc"), CancellationToken.None);
        byKey.Items.Single().Key.Should().Be("AIV-2026-001");

        var byStatement = await handler.Handle(new GetInvariantsRegisterQuery(Search: "cross-module"), CancellationToken.None);
        byStatement.Total.Should().Be(2);

        var page = await handler.Handle(new GetInvariantsRegisterQuery(SortBy: "statement", PageSize: 1, Page: 1), CancellationToken.None);
        page.Total.Should().Be(2);
        page.Items.Should().ContainSingle();

        var byCategory = await handler.Handle(new GetInvariantsRegisterQuery(SortBy: "category", SortDir: "asc"), CancellationToken.None);
        byCategory.Total.Should().Be(2);

        var byStatus = await handler.Handle(new GetInvariantsRegisterQuery(SortBy: "status", SortDir: "asc"), CancellationToken.None);
        byStatus.Items.First().Status.Should().Be("Draft"); // Draft (1) sorts before Proposed (2)
    }

    // ── Validators ───────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Validators_reject_missing_required_fields()
    {
        new CreateInvariantValidator().Validate(CreateCmd()).IsValid.Should().BeTrue();
        new CreateInvariantValidator().Validate(CreateCmd() with { Statement = new LocalizedString("EN", "") }).IsValid.Should().BeFalse();
        new CreateInvariantValidator().Validate(CreateCmd() with { Rationale = new LocalizedString("", "ar") }).IsValid.Should().BeFalse();
        new CreateInvariantValidator().Validate(CreateCmd() with { OwnerUserId = "" }).IsValid.Should().BeFalse();
        new CreateInvariantValidator().Validate(CreateCmd() with { Category = (InvariantCategory)99 }).IsValid.Should().BeFalse();
        new CreateInvariantValidator().Validate(CreateCmd() with { Scope = (InvariantScope)0 }).IsValid.Should().BeFalse();

        new UpdateInvariantDraftValidator().Validate(new UpdateInvariantDraftCommand(Guid.NewGuid(), InvariantCategory.Data, InvariantScope.Platform, Statement, Rationale, null, "o", "O")).IsValid.Should().BeTrue();
        new UpdateInvariantDraftValidator().Validate(new UpdateInvariantDraftCommand(Guid.Empty, InvariantCategory.Data, InvariantScope.Platform, Statement, Rationale, null, "o", "O")).IsValid.Should().BeFalse();

        new RetireInvariantValidator().Validate(new RetireInvariantCommand(Guid.NewGuid(), Reason)).IsValid.Should().BeTrue();
        new RetireInvariantValidator().Validate(new RetireInvariantCommand(Guid.NewGuid(), new LocalizedString("en", ""))).IsValid.Should().BeFalse();

        new SupersedeInvariantValidator().Validate(new SupersedeInvariantCommand(Guid.NewGuid(), InvariantCategory.Data, InvariantScope.Platform, Statement, Rationale, null, "o", "O", Reason)).IsValid.Should().BeTrue();
        new SupersedeInvariantValidator().Validate(new SupersedeInvariantCommand(Guid.Empty, InvariantCategory.Data, InvariantScope.Platform, Statement, Rationale, null, "o", "O", Reason)).IsValid.Should().BeFalse();
        new SupersedeInvariantValidator().Validate(new SupersedeInvariantCommand(Guid.NewGuid(), InvariantCategory.Data, InvariantScope.Platform, Statement, Rationale, null, "o", "O", new LocalizedString("en", ""))).IsValid.Should().BeFalse();
    }

    // Helper: create → propose → activate, returning the active invariant's PublicId.
    private static async Task<Guid> ActiveIdAsync(GovernanceDbContext db, ICurrentUser user, IClock clock)
    {
        var created = await CreateAsync(db, user, clock);
        var noCommittee = new FakeCommittee(Array.Empty<CommitteeRecipient>());
        await new ProposeInvariantHandler(db, clock, new RecordingAudit(), user, noCommittee, new RecordingChannel())
            .Handle(new ProposeInvariantCommand(created.Id), CancellationToken.None);
        await new ApproveInvariantHandler(db, clock, new RecordingAudit(), user, noCommittee, new RecordingChannel())
            .Handle(new ApproveInvariantCommand(created.Id), CancellationToken.None);
        return created.Id;
    }
}
