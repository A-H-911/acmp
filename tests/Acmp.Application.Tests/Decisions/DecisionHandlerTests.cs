using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Features.GetDecisionByKey;
using Acmp.Modules.Decisions.Application.Features.GetDecisionsByTopic;
using Acmp.Modules.Decisions.Application.Features.IssueDecision;
using Acmp.Modules.Decisions.Application.Features.RecordDecision;
using Acmp.Modules.Decisions.Application.Features.SupersedeDecision;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Behaviors;
using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Contracts.Topics;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Decisions;

// Round-trips through the real DecisionsDbContext (InMemory): EF mapping incl. the owned condition table
// and nullable LocalizedString columns; the key generator; and the W12/W21 command flow. The cross-module
// ITopicDecisionRecorder is faked so we assert Decisions asks Topics to advance state (it never touches
// Topics' tables — ADR-0001).
public class DecisionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Topic = Guid.NewGuid();
    private static readonly LocalizedString Title = LocalizedString.Create("Adopt Keycloak", "اعتماد كيكلوك");
    private static readonly LocalizedString Rationale = LocalizedString.Create("Sound choice", "اختيار سليم");

    private sealed class FakeRecorder : ITopicDecisionRecorder
    {
        public List<Guid> Decided { get; } = new();
        public Task MarkDecidedAsync(Guid topicId, CancellationToken ct = default) { Decided.Add(topicId); return Task.CompletedTask; }
    }

    private sealed class RecordingChannel : INotificationChannel
    {
        public List<NotificationMessage> Sent { get; } = new();
        public Task PublishAsync(NotificationMessage message, CancellationToken ct = default) { Sent.Add(message); return Task.CompletedTask; }
    }

    private static DecisionsDbContext NewDb(ICurrentUser user, IClock clock) => Db("decisions-" + Guid.NewGuid(), user, clock);

    // A context bound to a NAMED in-memory store, so several short-lived contexts can share data — this
    // mirrors production (one scoped DbContext per request) and avoids the EF InMemory owned-type
    // change-tracking quirk that fires when two aggregates with owned values are tracked in one context.
    private static DecisionsDbContext Db(string name, ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<DecisionsDbContext>().UseInMemoryDatabase(name).Options, clock, user);

    private static ICurrentUser User(string sub = "kc-chair", string name = "Sara Chair")
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

    private static ICommitteeDirectory Dir(params string[] subs)
    {
        var members = (subs.Length == 0 ? new[] { "kc-a", "kc-b" } : subs).Select(s => new CommitteeRecipient(s, s)).ToList();
        var d = Substitute.For<ICommitteeDirectory>();
        d.GetActiveMembersAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyCollection<CommitteeRecipient>)members);
        return d;
    }

    private static INotificationChannel NoNotify() => Substitute.For<INotificationChannel>();

    private static RecordDecisionCommand RecordCmd(
        DecisionOutcome outcome = DecisionOutcome.Approved, LocalizedString? alternatives = null,
        IReadOnlyList<DecisionConditionRequest>? conditions = null) =>
        new(Topic, MeetingId: null, outcome, Title, Rationale, alternatives, VoteId: null,
            conditions ?? Array.Empty<DecisionConditionRequest>());

    private static async Task<(DecisionsDbContext Db, Guid DecisionId)> DraftedAsync(ICurrentUser user, IClock clock,
        DecisionOutcome outcome = DecisionOutcome.Approved, LocalizedString? alternatives = null)
    {
        var db = NewDb(user, clock);
        var summary = await new RecordDecisionHandler(db, new DecisionKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
            .Handle(RecordCmd(outcome, alternatives), CancellationToken.None);
        return (db, summary.Id);
    }

    [Fact]
    public async Task Record_creates_draft_with_key_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = NewDb(user, clock);
        var audit = Substitute.For<IAuditSink>();

        var summary = await new RecordDecisionHandler(db, new DecisionKeyGenerator(db), user, clock, audit)
            .Handle(RecordCmd(), CancellationToken.None);

        summary.Key.Should().Be("DECN-2026-001");
        summary.Status.Should().Be("Draft");
        summary.Outcome.Should().Be("Approved");
        await audit.Received(1).EmitAsync("Decisions.DecisionDrafted", "kc-chair", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact] // nullable owned LocalizedString round-trips: null reads back as null, a value reads back intact
    public async Task Alternatives_nullable_owned_value_round_trips()
    {
        var user = User(); var clock = Clock(Now);
        var name = "rt-" + Guid.NewGuid();
        var alt = LocalizedString.Create("Use option B", "استخدم الخيار ب");

        string nullKey, valueKey;
        await using (var db = Db(name, user, clock))
            nullKey = (await new RecordDecisionHandler(db, new DecisionKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
                .Handle(RecordCmd(alternatives: null), default)).Key;
        await using (var db = Db(name, user, clock))
            valueKey = (await new RecordDecisionHandler(db, new DecisionKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
                .Handle(RecordCmd(alternatives: alt), default)).Key;

        await using var read = Db(name, user, clock);
        var nullDetail = await new GetDecisionByKeyHandler(read).Handle(new GetDecisionByKeyQuery(nullKey), default);
        var valueDetail = await new GetDecisionByKeyHandler(read).Handle(new GetDecisionByKeyQuery(valueKey), default);

        nullDetail!.Alternatives.Should().BeNull();
        valueDetail!.Alternatives.Should().Be(alt);
    }

    [Fact]
    public async Task Record_conditionally_approved_persists_conditions()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = NewDb(user, clock);
        var conditions = new[] { new DecisionConditionRequest(LocalizedString.Create("Add tests", "أضف اختبارات"), Now.AddDays(7)) };

        var summary = await new RecordDecisionHandler(db, new DecisionKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
            .Handle(RecordCmd(DecisionOutcome.ConditionallyApproved, conditions: conditions), default);

        var detail = await new GetDecisionByKeyHandler(db).Handle(new GetDecisionByKeyQuery(summary.Key), default);
        detail!.Conditions.Should().ContainSingle();
        detail.Conditions[0].Text.En.Should().Be("Add tests");
        detail.Conditions[0].Status.Should().Be("Open");
    }

    [Fact]
    public async Task Issue_moves_to_issued_marks_topic_decided_notifies_and_audits()
    {
        var user = User(); var clock = Clock(Now);
        var (db, id) = await DraftedAsync(user, clock);
        await using var _ = db;
        var recorder = new FakeRecorder();
        var channel = new RecordingChannel();
        var audit = Substitute.For<IAuditSink>();

        await new IssueDecisionHandler(db, recorder, user, clock, audit, Dir("kc-a", "kc-b"), channel)
            .Handle(new IssueDecisionCommand(id, ChairOverride: false, OverrideJustification: null), default);

        var detail = await new GetDecisionByKeyHandler(db).Handle(new GetDecisionByKeyQuery("DECN-2026-001"), default);
        detail!.Status.Should().Be("Issued");
        detail.ChairApprovedByName.Should().Be("Sara Chair");
        recorder.Decided.Should().ContainSingle().Which.Should().Be(Topic);
        channel.Sent.Should().HaveCount(2);
        channel.Sent.Should().OnlyContain(m => m.Category == "DecisionIssued" && m.DeepLink == "/decisions/DECN-2026-001");
        await audit.Received(1).EmitAsync("Decisions.DecisionIssued", "kc-chair", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Issue_throws_not_found_for_an_unknown_decision()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = NewDb(user, clock);

        var act = () => new IssueDecisionHandler(db, new FakeRecorder(), user, clock, Substitute.For<IAuditSink>(), Dir(), NoNotify())
            .Handle(new IssueDecisionCommand(Guid.NewGuid(), false, null), default);

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("Decision not found.");
    }

    [Fact] // AC-028: supersede issues a successor first, then flips the prior to Superseded with a back-link
    public async Task Supersede_issues_successor_then_supersedes_prior_with_backlink()
    {
        var user = User(); var clock = Clock(Now);
        var name = "sup-" + Guid.NewGuid();
        var recorder = new FakeRecorder();
        var channel = new RecordingChannel();
        var audit = Substitute.For<IAuditSink>();
        var reason = LocalizedString.Create("Corrected scope", "نطاق مصحح");

        Guid priorId;
        await using (var db = Db(name, user, clock))
            priorId = (await new RecordDecisionHandler(db, new DecisionKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
                .Handle(RecordCmd(), default)).Id;
        await using (var db = Db(name, user, clock)) // the prior must be Issued before it can be superseded (W21)
            await new IssueDecisionHandler(db, new FakeRecorder(), user, clock, Substitute.For<IAuditSink>(), Dir(), NoNotify())
                .Handle(new IssueDecisionCommand(priorId, false, null), default);

        DecisionSummaryDto successor;
        await using (var db = Db(name, user, clock))
            successor = await new SupersedeDecisionHandler(db, new DecisionKeyGenerator(db), recorder, user, clock, audit, Dir(), channel)
                .Handle(new SupersedeDecisionCommand(priorId, DecisionOutcome.Approved, Title, Rationale, null,
                    Array.Empty<DecisionConditionRequest>(), reason), default);

        successor.Key.Should().Be("DECN-2026-002");
        successor.Status.Should().Be("Issued");

        await using var read = Db(name, user, clock);
        var priorDetail = await new GetDecisionByKeyHandler(read).Handle(new GetDecisionByKeyQuery("DECN-2026-001"), default);
        priorDetail!.Status.Should().Be("Superseded");
        priorDetail.SupersededByDecisionId.Should().Be(successor.Id);
        priorDetail.SupersessionReason.Should().Be(reason);

        await audit.Received(1).EmitAsync("Decisions.DecisionSuperseded", "kc-chair", Arg.Any<object>(), Arg.Any<CancellationToken>());
        await audit.Received(1).EmitAsync("Decisions.DecisionIssued", "kc-chair", Arg.Any<object>(), Arg.Any<CancellationToken>());
        recorder.Decided.Should().Contain(Topic); // successor's issue still drives the topic (idempotent seam)
    }

    [Fact]
    public async Task Supersede_throws_not_found_for_an_unknown_prior()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = NewDb(user, clock);

        var act = () => new SupersedeDecisionHandler(db, new DecisionKeyGenerator(db), new FakeRecorder(), user, clock,
                Substitute.For<IAuditSink>(), Dir(), NoNotify())
            .Handle(new SupersedeDecisionCommand(Guid.NewGuid(), DecisionOutcome.Approved, Title, Rationale, null,
                Array.Empty<DecisionConditionRequest>(), LocalizedString.Create("r", "ر")), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Supersede_on_a_draft_prior_trips_the_domain_guard()
    {
        var user = User(); var clock = Clock(Now);
        var (db, priorId) = await DraftedAsync(user, clock);   // still Draft — not Issued
        await using var _ = db;

        var act = () => new SupersedeDecisionHandler(db, new DecisionKeyGenerator(db), new FakeRecorder(), user, clock,
                Substitute.For<IAuditSink>(), Dir(), NoNotify())
            .Handle(new SupersedeDecisionCommand(priorId, DecisionOutcome.Approved, Title, Rationale, null,
                Array.Empty<DecisionConditionRequest>(), LocalizedString.Create("r", "ر")), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetDecisionsByTopic_lists_history_newest_first()
    {
        var user = User(); var clock = Clock(Now);
        var name = "list-" + Guid.NewGuid();
        await using (var db = Db(name, user, clock))
            await new RecordDecisionHandler(db, new DecisionKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
                .Handle(RecordCmd(), default);
        await using (var db = Db(name, user, clock))
            await new RecordDecisionHandler(db, new DecisionKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
                .Handle(RecordCmd(), default);   // second decision for the same topic

        await using var read = Db(name, user, clock);
        var list = await new GetDecisionsByTopicHandler(read).Handle(new GetDecisionsByTopicQuery(Topic), default);

        list.Should().HaveCount(2);
        list[0].Key.Should().Be("DECN-2026-002");   // newest first
        list.Should().OnlyContain(d => d.TopicId == Topic);
    }

    // ── validators ──────────────────────────────────────────────────────────
    [Fact]
    public void Record_validator_rejects_empty_rationale_and_missing_topic()
    {
        var v = new RecordDecisionValidator();
        var bad = new RecordDecisionCommand(Guid.Empty, null, DecisionOutcome.Approved,
            Title, new LocalizedString("", ""), null, null, Array.Empty<DecisionConditionRequest>());
        v.Validate(bad).IsValid.Should().BeFalse();
    }

    [Fact] // content is mirrored to both columns, so a field missing one language is rejected (clean 400)
    public void Record_validator_requires_both_languages()
    {
        var v = new RecordDecisionValidator();
        var mirrored = new RecordDecisionCommand(Topic, null, DecisionOutcome.Approved,
            Title, Rationale, null, null, Array.Empty<DecisionConditionRequest>());
        v.Validate(mirrored).IsValid.Should().BeTrue();

        var missingAr = mirrored with { Rationale = new LocalizedString("English only", "") };
        v.Validate(missingAr).IsValid.Should().BeFalse();

        var missingEn = mirrored with { Title = new LocalizedString("", "عربي فقط") };
        v.Validate(missingEn).IsValid.Should().BeFalse();
    }

    [Fact] // a title over the nvarchar(512) column bound is a clean 400, not a SaveChanges 500
    public void Record_validator_rejects_a_title_over_512_chars()
    {
        var v = new RecordDecisionValidator();
        var longTitle = new LocalizedString(new string('x', 513), new string('ي', 513));
        var bad = new RecordDecisionCommand(Topic, null, DecisionOutcome.Approved,
            longTitle, Rationale, null, null, Array.Empty<DecisionConditionRequest>());
        v.Validate(bad).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Record_validator_requires_a_condition_for_conditionally_approved()
    {
        var v = new RecordDecisionValidator();
        var bad = new RecordDecisionCommand(Topic, null, DecisionOutcome.ConditionallyApproved,
            Title, Rationale, null, null, Array.Empty<DecisionConditionRequest>());
        v.Validate(bad).IsValid.Should().BeFalse();
    }

    [Fact] // a condition with null/empty bilingual text is a clean 400, not the domain 409
    public void Record_validator_rejects_a_condition_with_blank_text()
    {
        var v = new RecordDecisionValidator();

        var nullText = new RecordDecisionCommand(Topic, null, DecisionOutcome.ConditionallyApproved,
            Title, Rationale, null, null, new[] { new DecisionConditionRequest(null!, null) });
        v.Validate(nullText).IsValid.Should().BeFalse();

        var blankText = new RecordDecisionCommand(Topic, null, DecisionOutcome.ConditionallyApproved,
            Title, Rationale, null, null, new[] { new DecisionConditionRequest(new LocalizedString("", ""), null) });
        v.Validate(blankText).IsValid.Should().BeFalse();

        var ok = new RecordDecisionCommand(Topic, null, DecisionOutcome.ConditionallyApproved,
            Title, Rationale, null, null, new[] { new DecisionConditionRequest(LocalizedString.Create("Do X", "افعل"), null) });
        v.Validate(ok).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Issue_validator_requires_justification_when_overriding()
    {
        var v = new IssueDecisionValidator();
        v.Validate(new IssueDecisionCommand(Guid.NewGuid(), ChairOverride: true, OverrideJustification: null))
            .IsValid.Should().BeFalse();
        v.Validate(new IssueDecisionCommand(Guid.NewGuid(), ChairOverride: false, OverrideJustification: null))
            .IsValid.Should().BeTrue();
    }

    // ── authorization pipeline (guardrail 4) ────────────────────────────────
    [Fact]
    public async Task Pipeline_forbids_a_member_from_recording_a_decision()
    {
        var member = Substitute.For<ICurrentUser>();
        member.IsAuthenticated.Returns(true);
        member.UserId.Returns("kc-member");
        member.IsInRole("Member").Returns(true);
        var behavior = new AuthorizationBehavior<RecordDecisionCommand, DecisionSummaryDto>(member, Substitute.For<IAuditSink>());

        var act = () => behavior.Handle(RecordCmd(), () => Task.FromResult<DecisionSummaryDto>(null!), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
