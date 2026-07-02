using Acmp.Modules.Risks.Application.Contracts;
using Acmp.Modules.Risks.Application.Features.AcceptRisk;
using Acmp.Modules.Risks.Application.Features.ChangeRiskStatus;
using Acmp.Modules.Risks.Application.Features.GetRiskByKey;
using Acmp.Modules.Risks.Application.Features.GetRisksRegister;
using Acmp.Modules.Risks.Application.Features.ManageMitigations;
using Acmp.Modules.Risks.Application.Features.RaiseRisk;
using Acmp.Modules.Risks.Domain.Enums;
using Acmp.Modules.Risks.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Behaviors;
using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Risks;

// Round-trips through the real RisksDbContext (InMemory): EF mapping incl. the owned mitigation collection
// + nullable LocalizedString columns; the key generator; the full W15 command flow. The escalation fan-out
// (BL-135) and the narrower Risk.Accept authorization are proven here.
public class RiskHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly LocalizedString Title = LocalizedString.Create("Auth migration risk", "خطر الترحيل");
    private static readonly LocalizedString Plan = LocalizedString.Create("Dual-run", "تشغيل مزدوج");

    private static RisksDbContext Db(string name, ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<RisksDbContext>().UseInMemoryDatabase(name).Options, clock, user);

    private static RisksDbContext NewDb(ICurrentUser user, IClock clock) => Db("risks-" + Guid.NewGuid(), user, clock);

    private static ICurrentUser User(string sub = "kc-sec", string name = "Sam Secretary")
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
        public Task PublishAsync(NotificationMessage message, CancellationToken ct = default) { Sent.Add(message); return Task.CompletedTask; }
    }

    private sealed class FakeCommittee : ICommitteeDirectory
    {
        private readonly Dictionary<string, CommitteeRecipient[]> _byRole;
        public FakeCommittee(Dictionary<string, CommitteeRecipient[]> byRole) => _byRole = byRole;
        public Task<IReadOnlyCollection<CommitteeRecipient>> GetActiveMembersAsync(CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyCollection<CommitteeRecipient>)_byRole.Values.SelectMany(x => x).ToList());
        public Task<IReadOnlyCollection<CommitteeRecipient>> GetActiveMembersInRoleAsync(string role, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyCollection<CommitteeRecipient>)(_byRole.TryGetValue(role, out var r) ? r : Array.Empty<CommitteeRecipient>()));
    }

    private static RaiseRiskCommand RaiseCmd(string owner = "kc-owner", RiskLevel l = RiskLevel.Medium,
        RiskLevel i = RiskLevel.High, LocalizedString? initial = null) =>
        new(Title, null, l, i, owner, "Owner", RiskSubjectType.Topic, Guid.NewGuid(), "TOP-2026-014", initial);

    private static async Task<(string Name, Guid Id, string Key)> RaisedAsync(string owner = "kc-owner",
        RiskLevel l = RiskLevel.Medium, RiskLevel i = RiskLevel.High)
    {
        var name = "flow-" + Guid.NewGuid();
        await using var db = Db(name, User(), Clock(Now));
        var summary = await new RaiseRiskHandler(db, new RiskKeyGenerator(db), User(), Clock(Now),
            Substitute.For<IAuditSink>(), Substitute.For<INotificationChannel>())
            .Handle(RaiseCmd(owner, l, i), CancellationToken.None);
        return (name, summary.Id, summary.Key);
    }

    [Fact]
    public async Task Raise_opens_a_risk_with_a_key_audits_and_notifies_the_owner()
    {
        var user = User("kc-sec"); var clock = Clock(Now);
        await using var db = NewDb(user, clock);
        var audit = Substitute.For<IAuditSink>();
        var channel = new RecordingChannel();

        var summary = await new RaiseRiskHandler(db, new RiskKeyGenerator(db), user, clock, audit, channel)
            .Handle(RaiseCmd(owner: "kc-owner"), CancellationToken.None);

        summary.Key.Should().Be("RSK-2026-001");
        summary.Status.Should().Be("Open");
        summary.Exposure.Should().Be("High"); // Medium × High = 6
        await audit.Received(1).EmitAsync("Risks.RiskRaised", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());
        channel.Sent.Should().ContainSingle()
            .Which.Should().Match<NotificationMessage>(m => m.RecipientUserId == "kc-owner" && m.DeepLink == "/risks/RSK-2026-001");
    }

    [Fact]
    public async Task Raise_self_owned_does_not_notify_and_can_seed_a_mitigation()
    {
        var user = User("kc-sec"); var clock = Clock(Now);
        var name = "seed-" + Guid.NewGuid();
        var channel = new RecordingChannel();
        string key;
        await using (var db = Db(name, user, clock))
        {
            var s = await new RaiseRiskHandler(db, new RiskKeyGenerator(db), user, clock, Substitute.For<IAuditSink>(), channel)
                .Handle(RaiseCmd(owner: "kc-sec", initial: Plan), CancellationToken.None);
            key = s.Key;
        }
        channel.Sent.Should().BeEmpty();

        await using var read = Db(name, User(), Clock(Now));
        var detail = await new GetRiskByKeyHandler(read).Handle(new GetRiskByKeyQuery(key), default);
        detail!.Mitigations.Should().ContainSingle().Which.Type.Should().Be("Reduce");
    }

    [Fact]
    public async Task Mitigation_add_and_status_change_are_audited()
    {
        var (name, id, key) = await RaisedAsync();
        var audit = Substitute.For<IAuditSink>();

        Guid mitigationId;
        await using (var db = Db(name, User(), Clock(Now)))
        {
            await new AddMitigationHandler(db, Clock(Now), audit, User())
                .Handle(new AddMitigationCommand(id, Plan, MitigationType.Reduce, null, null, null), default);
        }
        await using (var read = Db(name, User(), Clock(Now)))
            mitigationId = (await new GetRiskByKeyHandler(read).Handle(new GetRiskByKeyQuery(key), default))!.Mitigations[0].Id;

        await using (var db = Db(name, User(), Clock(Now)))
            await new SetMitigationStatusHandler(db, Clock(Now), audit, User())
                .Handle(new SetMitigationStatusCommand(id, mitigationId, MitigationStatus.InProgress), default);

        await audit.Received(1).EmitAsync("Risks.MitigationAdded", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());
        await audit.Received(1).EmitAsync("Risks.MitigationStatusChanged", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Begin_then_close_advances_and_audits()
    {
        var (name, id, key) = await RaisedAsync();
        var audit = Substitute.For<IAuditSink>();

        await using (var db = Db(name, User(), Clock(Now)))
            await new AddMitigationHandler(db, Clock(Now), Substitute.For<IAuditSink>(), User())
                .Handle(new AddMitigationCommand(id, Plan, MitigationType.Reduce, null, null, null), default);
        await using (var db = Db(name, User(), Clock(Now)))
            await new BeginMitigationHandler(db, Clock(Now), audit, User()).Handle(new BeginMitigationCommand(id), default);
        await using (var db = Db(name, User(), Clock(Now)))
            await new CloseRiskHandler(db, Clock(Now), audit, User())
                .Handle(new CloseRiskCommand(id, LocalizedString.Create("done", "تم")), default);

        await using var read = Db(name, User(), Clock(Now));
        (await new GetRiskByKeyHandler(read).Handle(new GetRiskByKeyQuery(key), default))!.Status.Should().Be("Closed");
        await audit.Received(1).EmitAsync("Risks.RiskMitigating", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());
        await audit.Received(1).EmitAsync("Risks.RiskClosed", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact] // BL-135: escalation notifies the Secretary + Chairman, skipping the actor, de-duplicated
    public async Task Escalate_audits_and_fans_out_to_secretary_and_chairman()
    {
        var (name, id, _) = await RaisedAsync();
        var audit = Substitute.For<IAuditSink>();
        var channel = new RecordingChannel();
        var committee = new FakeCommittee(new()
        {
            ["Secretary"] = new[] { new CommitteeRecipient("kc-sec", "Sam"), new CommitteeRecipient("kc-actor", "Actor") },
            ["Chairman"] = new[] { new CommitteeRecipient("kc-chair", "Chair") },
        });
        var actor = User("kc-actor", "Actor");

        await using (var db = Db(name, actor, Clock(Now)))
            await new EscalateRiskHandler(db, Clock(Now), audit, actor, committee, channel)
                .Handle(new EscalateRiskCommand(id, Plan, "Steering Board"), default);

        await audit.Received(1).EmitAsync("Risks.RiskEscalated", "kc-actor", Arg.Any<object>(), Arg.Any<CancellationToken>());
        channel.Sent.Select(m => m.RecipientUserId).Should().BeEquivalentTo(new[] { "kc-sec", "kc-chair" }); // actor skipped
    }

    [Fact]
    public async Task Escalate_unknown_risk_throws_not_found()
    {
        await using var db = NewDb(User(), Clock(Now));
        var act = () => new EscalateRiskHandler(db, Clock(Now), Substitute.For<IAuditSink>(), User(),
            new FakeCommittee(new()), new RecordingChannel()).Handle(new EscalateRiskCommand(Guid.NewGuid(), Plan, "Board"), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Accept_records_the_authority_and_audits()
    {
        var (name, id, key) = await RaisedAsync();
        var audit = Substitute.For<IAuditSink>();
        await using (var db = Db(name, User("kc-chair"), Clock(Now)))
            await new AcceptRiskHandler(db, Clock(Now), audit, User("kc-chair"))
                .Handle(new AcceptRiskCommand(id, Plan, "Chairman"), default);

        await using var read = Db(name, User(), Clock(Now));
        var detail = await new GetRiskByKeyHandler(read).Handle(new GetRiskByKeyQuery(key), default);
        detail!.Status.Should().Be("Accepted");
        detail.AcceptingAuthority.Should().Be("Chairman");
        await audit.Received(1).EmitAsync("Risks.RiskAccepted", "kc-chair", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Accept_unknown_risk_throws_not_found()
    {
        await using var db = NewDb(User(), Clock(Now));
        var act = () => new AcceptRiskHandler(db, Clock(Now), Substitute.For<IAuditSink>(), User())
            .Handle(new AcceptRiskCommand(Guid.NewGuid(), Plan, "Chairman"), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByKey_returns_null_for_an_unknown_key()
    {
        await using var db = NewDb(User(), Clock(Now));
        (await new GetRiskByKeyHandler(db).Handle(new GetRiskByKeyQuery("RSK-2026-999"), default)).Should().BeNull();
    }

    [Fact] // register: status/owner/exposure filters, search, and every sort arm
    public async Task Register_filters_sorts_and_searches()
    {
        var name = "reg-" + Guid.NewGuid();
        async Task Add(string owner, RiskLevel l, RiskLevel i)
        {
            await using var db = Db(name, User(), Clock(Now));
            await new RaiseRiskHandler(db, new RiskKeyGenerator(db), User(), Clock(Now),
                Substitute.For<IAuditSink>(), Substitute.For<INotificationChannel>()).Handle(RaiseCmd(owner, l, i), default);
        }
        await Add("kc-a", RiskLevel.Low, RiskLevel.Low);    // Low
        await Add("kc-a", RiskLevel.High, RiskLevel.High);  // Critical
        await Add("kc-b", RiskLevel.Medium, RiskLevel.High); // High

        await using var read = Db(name, User(), Clock(Now));
        var h = new GetRisksRegisterHandler(read);

        (await h.Handle(new GetRisksRegisterQuery(), default)).Total.Should().Be(3);
        (await h.Handle(new GetRisksRegisterQuery(OwnerUserId: "kc-a"), default)).Total.Should().Be(2);
        (await h.Handle(new GetRisksRegisterQuery(Statuses: new[] { RiskStatus.Open }), default)).Total.Should().Be(3);
        (await h.Handle(new GetRisksRegisterQuery(Exposures: new[] { RiskExposure.Critical }), default)).Total.Should().Be(1);
        (await h.Handle(new GetRisksRegisterQuery(Search: "Auth migration risk"), default)).Total.Should().Be(3);
        (await h.Handle(new GetRisksRegisterQuery(Search: "RSK-2026-001"), default)).Total.Should().Be(1);

        foreach (var by in new[] { "status", "created", "key", "exposure" })
            (await h.Handle(new GetRisksRegisterQuery(SortBy: by, SortDir: "asc"), default)).Total.Should().Be(3);

        // top-of-register by exposure (desc default) = the Critical one
        (await h.Handle(new GetRisksRegisterQuery(PageSize: 1), default)).Items[0].Exposure.Should().Be("Critical");
    }

    // ── validators ──────────────────────────────────────────────────────────
    [Fact]
    public void Raise_validator_checks_languages_owner_subject_and_levels()
    {
        var v = new RaiseRiskValidator();
        v.Validate(RaiseCmd()).IsValid.Should().BeTrue();
        v.Validate(RaiseCmd() with { Title = new LocalizedString("EN", "") }).IsValid.Should().BeFalse();
        v.Validate(RaiseCmd() with { OwnerUserId = "" }).IsValid.Should().BeFalse();
        v.Validate(RaiseCmd() with { SubjectId = Guid.Empty }).IsValid.Should().BeFalse();
        v.Validate(RaiseCmd() with { Likelihood = (RiskLevel)0 }).IsValid.Should().BeFalse();
        v.Validate(RaiseCmd(initial: new LocalizedString("only en", ""))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Mitigation_and_transition_validators_enforce_their_fields()
    {
        new AddMitigationValidator().Validate(new AddMitigationCommand(Guid.NewGuid(), Plan, MitigationType.Reduce, null, null, null)).IsValid.Should().BeTrue();
        new AddMitigationValidator().Validate(new AddMitigationCommand(Guid.NewGuid(), Plan, (MitigationType)0, null, null, null)).IsValid.Should().BeFalse();
        new SetMitigationStatusValidator().Validate(new SetMitigationStatusCommand(Guid.NewGuid(), Guid.NewGuid(), (MitigationStatus)9)).IsValid.Should().BeFalse();
        new CloseRiskValidator().Validate(new CloseRiskCommand(Guid.NewGuid(), new LocalizedString("en", ""))).IsValid.Should().BeFalse();
        new CloseRiskValidator().Validate(new CloseRiskCommand(Guid.NewGuid(), null)).IsValid.Should().BeTrue();
        new EscalateRiskValidator().Validate(new EscalateRiskCommand(Guid.NewGuid(), Plan, "")).IsValid.Should().BeFalse();
        new EscalateRiskValidator().Validate(new EscalateRiskCommand(Guid.NewGuid(), Plan, "Board")).IsValid.Should().BeTrue();
        new AcceptRiskValidator().Validate(new AcceptRiskCommand(Guid.NewGuid(), Plan, "")).IsValid.Should().BeFalse();
        new AcceptRiskValidator().Validate(new AcceptRiskCommand(Guid.NewGuid(), Plan, "Chairman")).IsValid.Should().BeTrue();
    }

    // ── authorization pipeline (guardrail 4) ────────────────────────────────
    [Fact]
    public async Task Pipeline_forbids_a_member_from_accepting_a_risk()
    {
        var member = Substitute.For<ICurrentUser>();
        member.IsAuthenticated.Returns(true);
        member.UserId.Returns("kc-mem");
        member.IsInRole("Member").Returns(true);
        var behavior = new AuthorizationBehavior<AcceptRiskCommand, Unit>(member, Substitute.For<IAuditSink>());

        var act = () => behavior.Handle(new AcceptRiskCommand(Guid.NewGuid(), Plan, "Chairman"),
            () => Task.FromResult(Unit.Value), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
