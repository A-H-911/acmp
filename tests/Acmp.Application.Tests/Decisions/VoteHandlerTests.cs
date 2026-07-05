using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Features.CastBallot;
using Acmp.Modules.Decisions.Application.Features.ChangeBallot;
using Acmp.Modules.Decisions.Application.Features.CloseVote;
using Acmp.Modules.Decisions.Application.Features.ConfigureVote;
using Acmp.Modules.Decisions.Application.Features.GetVoteByKey;
using Acmp.Modules.Decisions.Application.Features.GetVotes;
using Acmp.Modules.Decisions.Application.Features.GetVotesForTopic;
using Acmp.Modules.Decisions.Application.Features.IssueDecision;
using Acmp.Modules.Decisions.Application.Features.OpenVote;
using Acmp.Modules.Decisions.Application.Features.RecordDecision;
using Acmp.Modules.Decisions.Application.Features.RecuseVote;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Behaviors;
using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Contracts.Actions;
using Acmp.Shared.Contracts.Meetings;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Contracts.Topics;
using Acmp.Shared.Contracts.Traceability;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Decisions;

// Round-trips through the real DecisionsDbContext (InMemory) for the W11 Vote flow: configure → open → cast →
// close, incl. the present-quorum seam (mocked IMeetingQuorumSource), the AC-022 double-vote audited denial,
// notification fan-out, and the SoD-3 gate on the decision-issue path (AC-015/016). Short-lived contexts on a
// shared NAMED store mirror production (one scoped context per request) and dodge the InMemory owned-type quirk.
public class VoteHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Topic = Guid.NewGuid();

    private static DecisionsDbContext Db(string name, ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<DecisionsDbContext>().UseInMemoryDatabase(name).Options, clock, user);

    private static ICurrentUser User(string sub, string name = "User")
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

    private static IMeetingQuorumSource Quorum(int present)
    {
        var q = Substitute.For<IMeetingQuorumSource>();
        q.GetPresentEligibleCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(present);
        return q;
    }

    private sealed class RecordingChannel : INotificationChannel
    {
        public List<NotificationMessage> Sent { get; } = new();
        public Task PublishAsync(NotificationMessage message, CancellationToken ct = default) { Sent.Add(message); return Task.CompletedTask; }
    }

    private static readonly IReadOnlyList<VoteEligibleVoterRequest> ThreeVoters = new[]
    {
        new VoteEligibleVoterRequest("kc-alice", "Alice"),
        new VoteEligibleVoterRequest("kc-bob", "Bob"),
        new VoteEligibleVoterRequest("kc-carol", "Carol"),
    };

    private static ConfigureVoteCommand ConfigCmd(Guid? meetingId = null, int minPresent = 0, int minCast = 2,
        bool allowAbstain = true, IReadOnlyList<VoteEligibleVoterRequest>? voters = null) =>
        new(Topic, meetingId, new[] { "Approve", "Reject" }, allowAbstain, minPresent, minCast, voters ?? ThreeVoters);

    private static async Task<(Guid Id, string Key)> ConfigureAsync(string name, IClock clock, ConfigureVoteCommand cmd)
    {
        var sec = User("kc-sec", "Sec");
        await using var db = Db(name, sec, clock);
        var summary = await new ConfigureVoteHandler(db, new DecisionKeyGenerator(db), sec, clock, Substitute.For<IAuditSink>())
            .Handle(cmd, default);
        return (summary.Id, summary.Key);
    }

    // ── committee-wide register (P12, feeds AC-066 chairman "votes awaiting approval") ──
    [Fact]
    public async Task GetVotes_register_returns_all_and_status_filter_isolates_a_state()
    {
        var clock = Clock(Now);
        var name = "vreg-" + Guid.NewGuid();
        await ConfigureAsync(name, clock, ConfigCmd());
        await ConfigureAsync(name, clock, ConfigCmd());   // two Configured votes; register is not topic-scoped

        await using var read = Db(name, User("kc-any"), clock);
        (await new GetVotesHandler(read).Handle(new GetVotesQuery(null), default)).Should().HaveCount(2);
        // The filter mechanic is enum-name equality; a live Closed transition is exercised by the
        // lifecycle tests — the register only re-filters, so proving isolation on Configured suffices.
        (await new GetVotesHandler(read).Handle(new GetVotesQuery("Configured"), default)).Should().HaveCount(2);
        (await new GetVotesHandler(read).Handle(new GetVotesQuery("Closed"), default)).Should().BeEmpty();
        (await new GetVotesHandler(read).Handle(new GetVotesQuery("NotAStatus"), default)).Should().HaveCount(2);
    }

    // ── configure ───────────────────────────────────────────────────────────
    [Fact]
    public async Task Configure_creates_configured_vote_with_key_and_audits()
    {
        var clock = Clock(Now); var sec = User("kc-sec", "Sec"); var audit = Substitute.For<IAuditSink>();
        await using var db = Db("cfg-" + Guid.NewGuid(), sec, clock);

        var summary = await new ConfigureVoteHandler(db, new DecisionKeyGenerator(db), sec, clock, audit)
            .Handle(ConfigCmd(), default);

        summary.Key.Should().Be("VOTE-2026-001");
        summary.Status.Should().Be("Configured");
        summary.Options.Should().Equal("Approve", "Reject");
        await audit.Received(1).EmitAsync("Decisions.VoteConfigured", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    // ── open + present quorum via the seam ────────────────────────────────────
    [Fact]
    public async Task Open_uses_the_meeting_quorum_seam_and_fans_out_to_eligible_voters()
    {
        var clock = Clock(Now); var name = "open-" + Guid.NewGuid();
        var (id, key) = await ConfigureAsync(name, clock, ConfigCmd(meetingId: Guid.NewGuid(), minPresent: 2));
        var sec = User("kc-sec", "Sec"); var channel = new RecordingChannel(); var audit = Substitute.For<IAuditSink>();

        await using (var db = Db(name, sec, clock))
            await new OpenVoteHandler(db, Quorum(3), sec, clock, audit, channel).Handle(new OpenVoteCommand(id), default);

        channel.Sent.Should().HaveCount(3);
        channel.Sent.Should().OnlyContain(m => m.Category == "VoteOpened" && m.DeepLink == $"/votes/{key}");
        await audit.Received(1).EmitAsync("Decisions.VoteOpened", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());

        await using var read = Db(name, sec, clock);
        (await new GetVoteByKeyHandler(read).Handle(new GetVoteByKeyQuery(key), default))!.Status.Should().Be("Open");
    }

    [Fact] // present quorum not met → Open rejected, vote stays Configured
    public async Task Open_rejects_when_present_quorum_is_not_met()
    {
        var clock = Clock(Now); var name = "openx-" + Guid.NewGuid();
        var (id, key) = await ConfigureAsync(name, clock, ConfigCmd(meetingId: Guid.NewGuid(), minPresent: 3));
        var sec = User("kc-sec", "Sec");

        await using (var db = Db(name, sec, clock))
        {
            var act = () => new OpenVoteHandler(db, Quorum(1), sec, clock, Substitute.For<IAuditSink>(), new RecordingChannel())
                .Handle(new OpenVoteCommand(id), default);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Present quorum not met*");
        }

        await using var read = Db(name, sec, clock);
        (await new GetVoteByKeyHandler(read).Handle(new GetVoteByKeyQuery(key), default))!.Status.Should().Be("Configured");
    }

    [Fact] // no linked meeting → present check skipped (seam not consulted)
    public async Task Open_without_a_meeting_skips_the_present_check()
    {
        var clock = Clock(Now); var name = "openn-" + Guid.NewGuid();
        var (id, key) = await ConfigureAsync(name, clock, ConfigCmd(meetingId: null, minPresent: 5));
        var sec = User("kc-sec", "Sec"); var quorum = Quorum(0);

        await using (var db = Db(name, sec, clock))
            await new OpenVoteHandler(db, quorum, sec, clock, Substitute.For<IAuditSink>(), new RecordingChannel())
                .Handle(new OpenVoteCommand(id), default);

        await quorum.DidNotReceive().GetPresentEligibleCountAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await using var read = Db(name, sec, clock);
        (await new GetVoteByKeyHandler(read).Handle(new GetVoteByKeyQuery(key), default))!.Status.Should().Be("Open");
    }

    [Fact]
    public async Task Open_throws_not_found_for_an_unknown_vote()
    {
        var clock = Clock(Now); var sec = User("kc-sec", "Sec");
        await using var db = Db("nf-" + Guid.NewGuid(), sec, clock);
        var act = () => new OpenVoteHandler(db, Quorum(3), sec, clock, Substitute.For<IAuditSink>(), new RecordingChannel())
            .Handle(new OpenVoteCommand(Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── cast / change / recuse ────────────────────────────────────────────────
    private static async Task OpenAsync(string name, IClock clock, Guid id)
    {
        var sec = User("kc-sec", "Sec");
        await using var db = Db(name, sec, clock);
        await new OpenVoteHandler(db, Quorum(3), sec, clock, Substitute.For<IAuditSink>(), new RecordingChannel())
            .Handle(new OpenVoteCommand(id), default);
    }

    [Fact]
    public async Task Cast_records_a_ballot_and_audits()
    {
        var clock = Clock(Now); var name = "cast-" + Guid.NewGuid();
        var (id, key) = await ConfigureAsync(name, clock, ConfigCmd(minCast: 1));
        await OpenAsync(name, clock, id);
        var alice = User("kc-alice", "Alice"); var audit = Substitute.For<IAuditSink>();

        await using (var db = Db(name, alice, clock))
            await new CastBallotHandler(db, alice, clock, audit).Handle(new CastBallotCommand(id, "Approve", null), default);

        await audit.Received(1).EmitAsync("Decisions.BallotCast", "kc-alice", Arg.Any<object>(), Arg.Any<CancellationToken>());
        await using var read = Db(name, alice, clock);
        var detail = await new GetVoteByKeyHandler(read).Handle(new GetVoteByKeyQuery(key), default);
        detail!.Ballots.Single(b => b.VoterUserId == "kc-alice").Choice.Should().Be("Approve");
    }

    [Fact] // AC-022: the double-vote is audited as a denial, then refused
    public async Task Cast_twice_audits_the_denial_and_throws()
    {
        var clock = Clock(Now); var name = "cast2-" + Guid.NewGuid();
        var (id, _) = await ConfigureAsync(name, clock, ConfigCmd(minCast: 1));
        await OpenAsync(name, clock, id);
        var alice = User("kc-alice", "Alice");

        await using (var db = Db(name, alice, clock))
            await new CastBallotHandler(db, alice, clock, Substitute.For<IAuditSink>()).Handle(new CastBallotCommand(id, "Approve", null), default);

        var audit = Substitute.For<IAuditSink>();
        await using (var db = Db(name, alice, clock))
        {
            var act = () => new CastBallotHandler(db, alice, clock, audit).Handle(new CastBallotCommand(id, "Reject", null), default);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already voted*");
        }
        await audit.Received(1).EmitAsync("Decisions.BallotDenied", "kc-alice", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeBallot_overwrites_and_Recuse_excludes_the_voter()
    {
        var clock = Clock(Now); var name = "chg-" + Guid.NewGuid();
        var (id, key) = await ConfigureAsync(name, clock, ConfigCmd(minCast: 1));
        await OpenAsync(name, clock, id);
        var alice = User("kc-alice", "Alice"); var bob = User("kc-bob", "Bob");

        await using (var db = Db(name, alice, clock))
            await new CastBallotHandler(db, alice, clock, Substitute.For<IAuditSink>()).Handle(new CastBallotCommand(id, "Approve", null), default);
        await using (var db = Db(name, alice, clock))
            await new ChangeBallotHandler(db, alice, clock, Substitute.For<IAuditSink>()).Handle(new ChangeBallotCommand(id, "Reject", null), default);
        await using (var db = Db(name, bob, clock))
            await new RecuseVoteHandler(db, bob, clock, Substitute.For<IAuditSink>()).Handle(new RecuseVoteCommand(id), default);

        await using var read = Db(name, alice, clock);
        var detail = await new GetVoteByKeyHandler(read).Handle(new GetVoteByKeyQuery(key), default);
        detail!.Ballots.Single(b => b.VoterUserId == "kc-alice").Choice.Should().Be("Reject");
        detail.Ballots.Single(b => b.VoterUserId == "kc-bob").Recused.Should().BeTrue();
    }

    // ── close ─────────────────────────────────────────────────────────────────
    [Fact] // AC-024: close below cast quorum is rejected; vote stays Open
    public async Task Close_below_cast_quorum_is_rejected()
    {
        var clock = Clock(Now); var name = "close-" + Guid.NewGuid();
        var (id, key) = await ConfigureAsync(name, clock, ConfigCmd(minCast: 2));
        await OpenAsync(name, clock, id);
        var alice = User("kc-alice", "Alice"); var sec = User("kc-sec", "Sec");

        await using (var db = Db(name, alice, clock))
            await new CastBallotHandler(db, alice, clock, Substitute.For<IAuditSink>()).Handle(new CastBallotCommand(id, "Approve", null), default);

        await using (var db = Db(name, sec, clock))
        {
            var act = () => new CloseVoteHandler(db, sec, clock, Substitute.For<IAuditSink>()).Handle(new CloseVoteCommand(id), default);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Quorum not met*");
        }
        await using var read = Db(name, sec, clock);
        (await new GetVoteByKeyHandler(read).Handle(new GetVoteByKeyQuery(key), default))!.Status.Should().Be("Open");
    }

    [Fact]
    public async Task Close_freezes_the_tally_records_counter_and_audits()
    {
        var clock = Clock(Now); var name = "closeok-" + Guid.NewGuid();
        var (id, key) = await ConfigureAsync(name, clock, ConfigCmd(minCast: 2));
        await OpenAsync(name, clock, id);
        var alice = User("kc-alice"); var bob = User("kc-bob"); var sec = User("kc-sec", "Sara Sec");
        var audit = Substitute.For<IAuditSink>();

        await using (var db = Db(name, alice, clock))
            await new CastBallotHandler(db, alice, clock, Substitute.For<IAuditSink>()).Handle(new CastBallotCommand(id, "Approve", null), default);
        await using (var db = Db(name, bob, clock))
            await new CastBallotHandler(db, bob, clock, Substitute.For<IAuditSink>()).Handle(new CastBallotCommand(id, "Reject", null), default);
        await using (var db = Db(name, sec, clock))
            await new CloseVoteHandler(db, sec, clock, audit).Handle(new CloseVoteCommand(id), default);

        await using var read = Db(name, sec, clock);
        var detail = await new GetVoteByKeyHandler(read).Handle(new GetVoteByKeyQuery(key), default);
        detail!.Status.Should().Be("Closed");
        detail.CounterUserId.Should().Be("kc-sec");
        detail.CounterName.Should().Be("Sara Sec");
        detail.Tally!.OptionCounts["Approve"].Should().Be(1);
        detail.Tally.CastCount.Should().Be(2);
        await audit.Received(1).EmitAsync("Decisions.VoteClosed", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetVotesForTopic_lists_newest_first()
    {
        var clock = Clock(Now); var name = "list-" + Guid.NewGuid();
        await ConfigureAsync(name, clock, ConfigCmd());
        await ConfigureAsync(name, clock, ConfigCmd());
        var sec = User("kc-sec", "Sec");

        await using var read = Db(name, sec, clock);
        var list = await new GetVotesForTopicHandler(read).Handle(new GetVotesForTopicQuery(Topic), default);
        list.Should().HaveCount(2);
        list[0].Key.Should().Be("VOTE-2026-002");
        list.Should().OnlyContain(v => v.TopicId == Topic);
    }

    // ── SoD-3 on the decision-issue path (AC-015/016) ─────────────────────────
    private static async Task<Guid> ClosedVoteAsync(string name, IClock clock, string counterSub, string counterName)
    {
        var (id, _) = await ConfigureAsync(name, clock, ConfigCmd(minCast: 1,
            voters: new[] { new VoteEligibleVoterRequest("kc-alice", "Alice") }));
        await OpenAsync(name, clock, id);
        var alice = User("kc-alice", "Alice");
        await using (var db = Db(name, alice, clock))
            await new CastBallotHandler(db, alice, clock, Substitute.For<IAuditSink>()).Handle(new CastBallotCommand(id, "Approve", null), default);
        var counter = User(counterSub, counterName);
        await using (var db = Db(name, counter, clock))
            await new CloseVoteHandler(db, counter, clock, Substitute.For<IAuditSink>()).Handle(new CloseVoteCommand(id), default);
        return id;
    }

    private sealed class FakeRecorder : ITopicDecisionRecorder
    {
        public Task MarkDecidedAsync(Guid topicId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static ICommitteeDirectory Dir()
    {
        var d = Substitute.For<ICommitteeDirectory>();
        d.GetActiveMembersAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyCollection<CommitteeRecipient>)new[] { new CommitteeRecipient("kc-a", "kc-a") });
        return d;
    }

    private static IActionLinkDirectory Links(bool hasLink = true)
    {
        var l = Substitute.For<IActionLinkDirectory>();
        l.DecisionHasLinkedActionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(hasLink);
        return l;
    }

    // P10c: the widened AC-029 arm defaults to no downstream edge, so these vote/SoD-3 tests stay governed by
    // the Action link exactly as before.
    private static ITraceabilityLinks TraceLinks(bool hasEdge = false)
    {
        var t = Substitute.For<ITraceabilityLinks>();
        t.DecisionHasDownstreamEdgeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(hasEdge);
        return t;
    }

    private static async Task<Guid> RecordVoteCoupledDecisionAsync(string name, IClock clock, Guid voteId)
    {
        var sec = User("kc-sec", "Sec");
        await using var db = Db(name, sec, clock);
        var title = LocalizedString.Create("Adopt", "اعتماد"); var rationale = LocalizedString.Create("Sound", "سليم");
        var statement = LocalizedString.Create("The committee adopts.", "تعتمد اللجنة.");
        var summary = await new RecordDecisionHandler(db, new DecisionKeyGenerator(db), sec, clock, Substitute.For<IAuditSink>())
            .Handle(new RecordDecisionCommand(Topic, null, DecisionOutcome.Approved, title, statement, rationale, null, voteId,
                Array.Empty<DecisionConditionRequest>()), default);
        return summary.Id;
    }

    [Fact] // AC-015: chair is the sole vote counter → 403 + audited denial; the vote stays Closed (not ratified)
    public async Task Issue_vote_coupled_decision_is_forbidden_when_chair_counted_the_vote()
    {
        var clock = Clock(Now); var name = "sod3a-" + Guid.NewGuid();
        var voteId = await ClosedVoteAsync(name, clock, counterSub: "kc-chair", counterName: "Dave Chair");
        var decisionId = await RecordVoteCoupledDecisionAsync(name, clock, voteId);
        var chair = User("kc-chair", "Dave Chair"); var audit = Substitute.For<IAuditSink>();

        await using (var db = Db(name, chair, clock))
        {
            var act = () => new IssueDecisionHandler(db, new FakeRecorder(), chair, clock, audit, Dir(),
                    Substitute.For<INotificationChannel>(), Links(), TraceLinks())
                .Handle(new IssueDecisionCommand(decisionId, false, null), default);
            await act.Should().ThrowAsync<ForbiddenAccessException>();
        }
        await audit.Received(1).EmitAsync("Decisions.DecisionIssueDenied", "kc-chair", Arg.Any<object>(), Arg.Any<CancellationToken>());

        await using var read = Db(name, chair, clock);
        var vote = await new GetVoteByKeyHandler(read).Handle(new GetVoteByKeyQuery("VOTE-2026-001"), default);
        vote!.Status.Should().Be("Closed");   // NOT ratified
    }

    [Fact] // AC-016: secretary counted → chair issue allowed + vote ratified
    public async Task Issue_vote_coupled_decision_is_allowed_and_ratifies_when_a_secretary_counted()
    {
        var clock = Clock(Now); var name = "sod3b-" + Guid.NewGuid();
        var voteId = await ClosedVoteAsync(name, clock, counterSub: "kc-sec", counterName: "Eva Sec");
        var decisionId = await RecordVoteCoupledDecisionAsync(name, clock, voteId);
        var chair = User("kc-chair", "Dave Chair");

        await using (var db = Db(name, chair, clock))
            await new IssueDecisionHandler(db, new FakeRecorder(), chair, clock, Substitute.For<IAuditSink>(), Dir(),
                    Substitute.For<INotificationChannel>(), Links(), TraceLinks())
                .Handle(new IssueDecisionCommand(decisionId, false, null), default);

        await using var read = Db(name, chair, clock);
        (await new GetVoteByKeyHandler(read).Handle(new GetVoteByKeyQuery("VOTE-2026-001"), default))!.Status.Should().Be("Ratified");
    }

    [Fact] // review HIGH: a decision coupled to a nonexistent vote is rejected at draft time (no dangling coupling)
    public async Task Record_rejects_a_decision_coupled_to_a_nonexistent_vote()
    {
        var clock = Clock(Now); var name = "coupx-" + Guid.NewGuid();
        var act = () => RecordVoteCoupledDecisionAsync(name, clock, Guid.NewGuid());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not exist*");
    }

    [Fact] // review HIGH: a vote on a different topic cannot back a decision (would defeat SoD-3 coupling)
    public async Task Record_rejects_a_decision_coupled_to_a_vote_on_a_different_topic()
    {
        var clock = Clock(Now); var name = "coupt-" + Guid.NewGuid();
        var voteId = await ClosedVoteAsync(name, clock, counterSub: "kc-sec", counterName: "Eva"); // vote is on Topic
        var sec = User("kc-sec", "Sec");
        await using var db = Db(name, sec, clock);
        var title = LocalizedString.Create("Adopt", "اعتماد"); var rationale = LocalizedString.Create("Sound", "سليم");
        var statement = LocalizedString.Create("The committee adopts.", "تعتمد اللجنة.");
        var act = () => new RecordDecisionHandler(db, new DecisionKeyGenerator(db), sec, clock, Substitute.For<IAuditSink>())
            .Handle(new RecordDecisionCommand(Guid.NewGuid(), null, DecisionOutcome.Approved, title, statement, rationale, null, voteId,
                Array.Empty<DecisionConditionRequest>()), default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*different topic*");
    }

    [Fact] // review MEDIUM: a vote-coupled decision cannot be issued while its vote is still Open (not the misleading 403)
    public async Task Issue_vote_coupled_decision_is_rejected_when_the_vote_is_not_closed()
    {
        var clock = Clock(Now); var name = "coupo-" + Guid.NewGuid();
        var (voteId, _) = await ConfigureAsync(name, clock, ConfigCmd(minCast: 1,
            voters: new[] { new VoteEligibleVoterRequest("kc-alice", "Alice") }));
        await OpenAsync(name, clock, voteId);   // Open, NOT closed
        var decisionId = await RecordVoteCoupledDecisionAsync(name, clock, voteId);
        var chair = User("kc-chair", "Dave Chair");

        await using (var db = Db(name, chair, clock))
        {
            var act = () => new IssueDecisionHandler(db, new FakeRecorder(), chair, clock, Substitute.For<IAuditSink>(), Dir(),
                    Substitute.For<INotificationChannel>(), Links(), TraceLinks())
                .Handle(new IssueDecisionCommand(decisionId, false, null), default);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*must be closed*");
        }

        await using var read = Db(name, chair, clock);
        (await new GetVoteByKeyHandler(read).Handle(new GetVoteByKeyQuery("VOTE-2026-001"), default))!.Status.Should().Be("Open");
    }

    // ── validators ────────────────────────────────────────────────────────────
    [Fact]
    public void Configure_validator_enforces_options_voters_and_quorum()
    {
        var v = new ConfigureVoteValidator();
        v.Validate(ConfigCmd()).IsValid.Should().BeTrue();
        v.Validate(ConfigCmd() with { Options = new[] { "OnlyOne" } }).IsValid.Should().BeFalse();
        v.Validate(ConfigCmd() with { MinCast = 0 }).IsValid.Should().BeFalse();
        v.Validate(ConfigCmd() with { EligibleVoters = Array.Empty<VoteEligibleVoterRequest>() }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Cast_validator_requires_a_choice()
    {
        var v = new CastBallotValidator();
        v.Validate(new CastBallotCommand(Guid.NewGuid(), "Approve", null)).IsValid.Should().BeTrue();
        v.Validate(new CastBallotCommand(Guid.NewGuid(), "", null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Change_and_Recuse_validators_enforce_id_and_choice()
    {
        new ChangeBallotValidator().Validate(new ChangeBallotCommand(Guid.NewGuid(), "Approve", null)).IsValid.Should().BeTrue();
        new ChangeBallotValidator().Validate(new ChangeBallotCommand(Guid.NewGuid(), "", null)).IsValid.Should().BeFalse();
        new RecuseVoteValidator().Validate(new RecuseVoteCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
        new RecuseVoteValidator().Validate(new RecuseVoteCommand(Guid.Empty)).IsValid.Should().BeFalse();
    }

    [Fact] // not-found path for the change/recuse handlers
    public async Task Change_and_Recuse_throw_when_the_vote_is_missing()
    {
        var clock = Clock(Now); var u = User("kc-alice", "Alice");
        await using var db = Db("nf-" + Guid.NewGuid(), u, clock);
        var change = () => new ChangeBallotHandler(db, u, clock, Substitute.For<IAuditSink>())
            .Handle(new ChangeBallotCommand(Guid.NewGuid(), "Approve", null), default);
        await change.Should().ThrowAsync<KeyNotFoundException>();
        var recuse = () => new RecuseVoteHandler(db, u, clock, Substitute.For<IAuditSink>())
            .Handle(new RecuseVoteCommand(Guid.NewGuid()), default);
        await recuse.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── authorization pipeline (guardrail 4) ──────────────────────────────────
    [Fact]
    public async Task Pipeline_forbids_a_member_from_configuring_a_vote()
    {
        var member = Substitute.For<ICurrentUser>();
        member.IsAuthenticated.Returns(true);
        member.UserId.Returns("kc-member");
        member.IsInRole("Member").Returns(true);
        var behavior = new AuthorizationBehavior<ConfigureVoteCommand, VoteSummaryDto>(member, Substitute.For<IAuditSink>());

        var act = () => behavior.Handle(ConfigCmd(), () => Task.FromResult<VoteSummaryDto>(null!), default);
        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
