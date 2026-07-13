using Acmp.Modules.Research.Application.Contracts;
using Acmp.Modules.Research.Application.Features.CreateMission;
using Acmp.Modules.Research.Application.Features.GetMissionByKey;
using Acmp.Modules.Research.Application.Features.GetMissionsRegister;
using Acmp.Modules.Research.Application.Features.ManageFindings;
using Acmp.Modules.Research.Application.Features.ManageRecommendations;
using Acmp.Modules.Research.Application.Features.MissionLifecycle;
using Acmp.Modules.Research.Application.Features.UpdateMissionDraft;
using Acmp.Modules.Research.Domain.Enums;
using Acmp.Modules.Research.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Research;

// Round-trips through the real ResearchDbContext (InMemory): EF mapping incl. the owned Finding/Recommendation
// collections + nullable LocalizedString columns; the key generator; the full P15a command flow (create, edit,
// activate, complete, cancel, add/update/verify findings, add/update/set-status recommendations) and the
// register/detail reads. Audit emits are captured with a recording fake; the before/after ENRICHMENT is proven
// separately in ResearchAuditEnrichmentTests (guardrail 1).
public class ResearchHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static LocalizedString L(string s = "x") => LocalizedString.Create(s, s);

    private static ResearchDbContext Db(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<ResearchDbContext>().UseInMemoryDatabase("res-" + Guid.NewGuid()).Options, clock, user);

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

    private static async Task<ResearchMissionSummaryDto> CreateAsync(ResearchDbContext db, ICurrentUser user, IClock clock, IAuditSink? audit = null) =>
        await new CreateMissionHandler(db, new ResearchKeyGenerator(db), user, clock, audit ?? Substitute.For<IAuditSink>(),
                Substitute.For<Acmp.Shared.Contracts.Topics.ITopicReader>(),
                Substitute.For<Acmp.Shared.Contracts.Traceability.ITraceabilityWriter>())
            .Handle(new CreateMissionCommand(L("Title"), L("Question"), "ref", Guid.NewGuid()), CancellationToken.None);

    private static async Task<Guid> ActiveMissionAsync(ResearchDbContext db, ICurrentUser user, IClock clock)
    {
        var created = await CreateAsync(db, user, clock);
        await new ActivateMissionHandler(db, clock, Substitute.For<IAuditSink>()).Handle(new ActivateMissionCommand(created.Id), CancellationToken.None);
        return created.Id;
    }

    // ── Create / update draft ────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Create_drafts_a_mission_with_a_key_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var audit = new RecordingAudit();

        var summary = await CreateAsync(db, user, clock, audit);

        summary.Key.Should().Be("RMS-2026-001");
        summary.Status.Should().Be("Proposed");
        summary.OwnerName.Should().Be("Owner");
        audit.Events.Should().Contain("Research.MissionProposed");
        (await db.Missions.SingleAsync()).OwnerUserId.Should().Be("kc-owner");
    }

    [Fact]
    public async Task Update_draft_replaces_fields_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var created = await CreateAsync(db, user, clock);
        var audit = new RecordingAudit();

        var summary = await new UpdateMissionDraftHandler(db, audit).Handle(
            new UpdateMissionDraftCommand(created.Id, L("New"), L("NewQ"), null, null), CancellationToken.None);

        summary.Key.Should().Be("RMS-2026-001");
        (await db.Missions.SingleAsync()).Title.En.Should().Be("New");
        audit.Events.Should().Contain("Research.MissionDraftUpdated");
    }

    [Fact]
    public async Task Update_draft_on_a_missing_mission_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new UpdateMissionDraftHandler(db, Substitute.For<IAuditSink>())
            .Handle(new UpdateMissionDraftCommand(Guid.NewGuid(), L(), L(), null, null), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Activate_complete_walks_the_lifecycle_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var created = await CreateAsync(db, user, clock);
        var audit = new RecordingAudit();

        await new ActivateMissionHandler(db, clock, audit).Handle(new ActivateMissionCommand(created.Id), CancellationToken.None);
        (await db.Missions.SingleAsync()).Status.Should().Be(ResearchMissionStatus.Active);

        await new CompleteMissionHandler(db, clock, audit).Handle(new CompleteMissionCommand(created.Id), CancellationToken.None);
        var done = await db.Missions.SingleAsync();
        done.Status.Should().Be(ResearchMissionStatus.Completed);
        done.CompletedAt.Should().Be(Now);
        audit.Events.Should().Contain("Research.MissionActivated").And.Contain("Research.MissionCompleted");
    }

    [Fact]
    public async Task Cancel_records_the_reason_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var created = await CreateAsync(db, user, clock);
        var audit = new RecordingAudit();

        await new CancelMissionHandler(db, clock, audit).Handle(new CancelMissionCommand(created.Id, L("no budget")), CancellationToken.None);
        var m = await db.Missions.SingleAsync();
        m.Status.Should().Be(ResearchMissionStatus.Cancelled);
        m.CancellationReason!.En.Should().Be("no budget");
        audit.Events.Should().Contain("Research.MissionCancelled");
    }

    [Fact]
    public async Task Activate_on_a_missing_mission_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new ActivateMissionHandler(db, clock, Substitute.For<IAuditSink>())
            .Handle(new ActivateMissionCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Findings ─────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Add_update_verify_finding_round_trips_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var missionId = await ActiveMissionAsync(db, user, clock);
        var audit = new RecordingAudit();

        await new AddFindingHandler(db, audit).Handle(new AddFindingCommand(missionId, L("f1"), L("detail"), Confidence.High), CancellationToken.None);
        var findingId = (await db.Missions.Include(m => m.Findings).SingleAsync()).Findings.Single().PublicId;

        await new UpdateFindingHandler(db, audit).Handle(new UpdateFindingCommand(missionId, findingId, L("f1b"), null, Confidence.Low), CancellationToken.None);
        await new VerifyFindingHandler(db, audit).Handle(new VerifyFindingCommand(missionId, findingId), CancellationToken.None);

        var f = (await db.Missions.Include(m => m.Findings).SingleAsync()).Findings.Single();
        f.Summary.En.Should().Be("f1b");
        f.IsVerified.Should().BeTrue();
        audit.Events.Should().Contain("Research.FindingAdded").And.Contain("Research.FindingUpdated").And.Contain("Research.FindingVerified");
    }

    [Fact]
    public async Task Add_finding_on_a_missing_mission_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new AddFindingHandler(db, Substitute.For<IAuditSink>())
            .Handle(new AddFindingCommand(Guid.NewGuid(), L(), null, Confidence.Low), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Recommendations ──────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Add_update_set_status_recommendation_round_trips_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var missionId = await ActiveMissionAsync(db, user, clock);
        var audit = new RecordingAudit();

        await new AddRecommendationHandler(db, audit).Handle(new AddRecommendationCommand(missionId, L("r1"), L("why"), RecommendationPriority.High, Guid.NewGuid()), CancellationToken.None);
        var recId = (await db.Missions.Include(m => m.Recommendations).SingleAsync()).Recommendations.Single().PublicId;

        await new UpdateRecommendationHandler(db, audit).Handle(new UpdateRecommendationCommand(missionId, recId, L("r1b"), null, RecommendationPriority.Low, null), CancellationToken.None);
        await new SetRecommendationStatusHandler(db, audit).Handle(new SetRecommendationStatusCommand(missionId, recId, RecommendationStatus.Accepted), CancellationToken.None);

        var r = (await db.Missions.Include(m => m.Recommendations).SingleAsync()).Recommendations.Single();
        r.Statement.En.Should().Be("r1b");
        r.Status.Should().Be(RecommendationStatus.Accepted);
        audit.Events.Should().Contain("Research.RecommendationAdded").And.Contain("Research.RecommendationUpdated").And.Contain("Research.RecommendationStatusChanged");
    }

    [Fact]
    public async Task Add_recommendation_on_a_missing_mission_throws()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var act = () => new AddRecommendationHandler(db, Substitute.For<IAuditSink>())
            .Handle(new AddRecommendationCommand(Guid.NewGuid(), L(), null, RecommendationPriority.Low, null), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Reads ────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetByKey_returns_the_detail_with_children_or_null()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var missionId = await ActiveMissionAsync(db, user, clock);
        await new AddFindingHandler(db, Substitute.For<IAuditSink>()).Handle(new AddFindingCommand(missionId, L("f"), null, Confidence.Low), CancellationToken.None);
        await new AddRecommendationHandler(db, Substitute.For<IAuditSink>()).Handle(new AddRecommendationCommand(missionId, L("r"), null, RecommendationPriority.Low, null), CancellationToken.None);

        var detail = await new GetMissionByKeyHandler(db).Handle(new GetMissionByKeyQuery("RMS-2026-001"), CancellationToken.None);
        detail!.Findings.Should().ContainSingle();
        detail.Recommendations.Should().ContainSingle();
        detail.Status.Should().Be("Active");

        (await new GetMissionByKeyHandler(db).Handle(new GetMissionByKeyQuery("RMS-2099-999"), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Register_filters_by_status_searches_sorts_and_pages()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        await CreateAsync(db, user, clock);                 // RMS-2026-001 (Proposed)
        var second = await CreateAsync(db, user, clock);    // RMS-2026-002 (Proposed)
        await new ActivateMissionHandler(db, clock, Substitute.For<IAuditSink>()).Handle(new ActivateMissionCommand(second.Id), CancellationToken.None);

        var handler = new GetMissionsRegisterHandler(db);

        var active = await handler.Handle(new GetMissionsRegisterQuery(Statuses: new[] { ResearchMissionStatus.Active }), CancellationToken.None);
        active.Total.Should().Be(1);
        active.Items.Single().Key.Should().Be("RMS-2026-002");

        var byKey = await handler.Handle(new GetMissionsRegisterQuery(Search: "RMS-2026-001", SortBy: "key", SortDir: "asc"), CancellationToken.None);
        byKey.Items.Single().Key.Should().Be("RMS-2026-001");

        var page = await handler.Handle(new GetMissionsRegisterQuery(SortBy: "title", PageSize: 1, Page: 1), CancellationToken.None);
        page.Total.Should().Be(2);
        page.Items.Should().ContainSingle();

        var byStatus = await handler.Handle(new GetMissionsRegisterQuery(SortBy: "status", SortDir: "asc"), CancellationToken.None);
        byStatus.Items.First().Status.Should().Be("Proposed"); // Proposed (1) sorts before Active (2)
    }

    // ── Validators ───────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Validators_reject_missing_required_fields()
    {
        new CreateMissionValidator().Validate(new CreateMissionCommand(L(), L(), null, null)).IsValid.Should().BeTrue();
        new CreateMissionValidator().Validate(new CreateMissionCommand(new LocalizedString("en", ""), L(), null, null)).IsValid.Should().BeFalse();
        new CreateMissionValidator().Validate(new CreateMissionCommand(L(), new LocalizedString("", "ar"), null, null)).IsValid.Should().BeFalse();

        new UpdateMissionDraftValidator().Validate(new UpdateMissionDraftCommand(Guid.NewGuid(), L(), L(), null, null)).IsValid.Should().BeTrue();
        new UpdateMissionDraftValidator().Validate(new UpdateMissionDraftCommand(Guid.Empty, L(), L(), null, null)).IsValid.Should().BeFalse();

        new CancelMissionValidator().Validate(new CancelMissionCommand(Guid.NewGuid(), L())).IsValid.Should().BeTrue();
        new CancelMissionValidator().Validate(new CancelMissionCommand(Guid.NewGuid(), new LocalizedString("en", ""))).IsValid.Should().BeFalse();

        new AddFindingValidator().Validate(new AddFindingCommand(Guid.NewGuid(), L(), L(), Confidence.Low)).IsValid.Should().BeTrue();
        new AddFindingValidator().Validate(new AddFindingCommand(Guid.NewGuid(), new LocalizedString("en", ""), null, Confidence.Low)).IsValid.Should().BeFalse();
        new AddFindingValidator().Validate(new AddFindingCommand(Guid.NewGuid(), L(), new LocalizedString("en", ""), Confidence.Low)).IsValid.Should().BeFalse();
        new AddFindingValidator().Validate(new AddFindingCommand(Guid.NewGuid(), L(), null, (Confidence)99)).IsValid.Should().BeFalse();

        new UpdateFindingValidator().Validate(new UpdateFindingCommand(Guid.NewGuid(), Guid.NewGuid(), L(), null, Confidence.Low)).IsValid.Should().BeTrue();
        new UpdateFindingValidator().Validate(new UpdateFindingCommand(Guid.NewGuid(), Guid.Empty, L(), null, Confidence.Low)).IsValid.Should().BeFalse();

        new AddRecommendationValidator().Validate(new AddRecommendationCommand(Guid.NewGuid(), L(), L(), RecommendationPriority.Low, null)).IsValid.Should().BeTrue();
        new AddRecommendationValidator().Validate(new AddRecommendationCommand(Guid.NewGuid(), new LocalizedString("en", ""), null, RecommendationPriority.Low, null)).IsValid.Should().BeFalse();
        new AddRecommendationValidator().Validate(new AddRecommendationCommand(Guid.NewGuid(), L(), new LocalizedString("en", ""), RecommendationPriority.Low, null)).IsValid.Should().BeFalse();
        new AddRecommendationValidator().Validate(new AddRecommendationCommand(Guid.NewGuid(), L(), null, (RecommendationPriority)99, null)).IsValid.Should().BeFalse();

        new UpdateRecommendationValidator().Validate(new UpdateRecommendationCommand(Guid.NewGuid(), Guid.NewGuid(), L(), null, RecommendationPriority.Low, null)).IsValid.Should().BeTrue();
        new UpdateRecommendationValidator().Validate(new UpdateRecommendationCommand(Guid.Empty, Guid.NewGuid(), L(), null, RecommendationPriority.Low, null)).IsValid.Should().BeFalse();

        new SetRecommendationStatusValidator().Validate(new SetRecommendationStatusCommand(Guid.NewGuid(), Guid.NewGuid(), RecommendationStatus.Accepted)).IsValid.Should().BeTrue();
        new SetRecommendationStatusValidator().Validate(new SetRecommendationStatusCommand(Guid.NewGuid(), Guid.NewGuid(), (RecommendationStatus)99)).IsValid.Should().BeFalse();
    }
}
