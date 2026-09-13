using Acmp.Modules.Actions.Domain;
using Acmp.Modules.Actions.Domain.Enums;
using Acmp.Modules.Actions.Infrastructure.Directory;
using Acmp.Modules.Decisions.Domain;
using Acmp.Modules.Decisions.Infrastructure.Directory;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Infrastructure.Directory;
using Acmp.Shared.Contracts.Actions;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Integration.Tests;

/*
 * FR-134 / AC-161 (DEC-188) — the digest's three cross-module read contracts on REAL SQL Server.
 *
 * AC-161's "Verified by" clause names these: each count is a query another module's handler relies on without
 * seeing the tables (ADR-0001), and a count that translates differently on SQL Server than on InMemory would
 * put a wrong number in every member's digest with every unit test green. Each test isolates itself from the
 * shared container: actions and ballots by a fresh user id, meetings by a time window no other test uses.
 */
[Collection(SqlBackstopCollection.Name)]
public sealed class DigestSourceSqlTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    // A NEW instance per use: EF owns each LocalizedString, and one instance on two owners nulls a column on SQL Server.
    private static LocalizedString Text => LocalizedString.Create("Digest seed", "بذرة الملخص");

    private readonly SqlBackstopFixture _fx;

    public DigestSourceSqlTests(SqlBackstopFixture fx) => _fx = fx;

    private static string Key(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..32];

    private static ActionItem Action(string owner, DateTimeOffset? due) =>
        ActionItem.Create(Key("ACT"), Text, null, ActionPriority.Normal, owner, "Owner", due,
            ActionSourceType.Decision, Guid.NewGuid(), "DECN-2026-001", null, Now);

    [Fact] // Live = Open/InProgress/Blocked owned by the member; overdue = due date passed while live.
    public async Task Pending_actions_count_only_the_members_live_actions_and_their_overdue_subset()
    {
        var owner = $"kc-{Guid.NewGuid():N}";
        await using (var db = _fx.NewActionsSql())
        {
            db.Actions.Add(Action(owner, null));                      // open, no due date: pending
            db.Actions.Add(Action(owner, Now.AddDays(3)));            // open, due later: pending
            var started = Action(owner, Now.AddDays(-2));             // in progress, past due: pending + overdue
            started.Start(Now);
            db.Actions.Add(started);
            var cancelled = Action(owner, Now.AddDays(-2));           // cancelled: not live
            cancelled.Cancel(Text, Now);
            db.Actions.Add(cancelled);
            db.Actions.Add(Action($"kc-{Guid.NewGuid():N}", Now.AddDays(-2))); // someone else's
            await db.SaveChangesAsync();
        }

        await using var read = _fx.NewActionsSql();
        (await new ActionDigestSource(read).CountPendingAsync(owner, Now))
            .Should().Be(new PendingActionCount(3, 1));
    }

    [Fact] // Awaited = an Open vote where the member holds a ballot neither cast nor recused.
    public async Task Awaiting_ballots_count_only_open_votes_the_member_has_not_cast_or_recused()
    {
        var voter = $"kc-{Guid.NewGuid():N}";
        var other = $"kc-{Guid.NewGuid():N}";
        Vote Vote(params string[] voters) => Acmp.Modules.Decisions.Domain.Vote.Configure(Key("VOTE"), Guid.NewGuid(), null,
            new[] { "Approve", "Reject" }, false, new QuorumRule(0, 1),
            voters.Select(v => new VoteEligibleVoter(v, v)), "seed", Now);

        await using (var db = _fx.NewDecisionsSql())
        {
            var awaiting = Vote(voter, other); awaiting.Open("seed", 2, Now);                           // counted
            var cast = Vote(voter, other); cast.Open("seed", 2, Now); cast.Cast(voter, "Approve", null, Now);
            var recused = Vote(voter, other); recused.Open("seed", 2, Now); recused.Recuse(voter, Now);
            var notOpen = Vote(voter, other);                                                          // Configured
            var notEligible = Vote(other); notEligible.Open("seed", 1, Now);
            db.Votes.AddRange(awaiting, cast, recused, notOpen, notEligible);
            await db.SaveChangesAsync();
        }

        await using var read = _fx.NewDecisionsSql();
        (await new BallotDigestSource(read).CountAwaitingAsync(voter)).Should().Be(1);
    }

    [Fact] // Scheduled meetings whose start is in [from, to): the start bound is inclusive, the end exclusive.
    public async Task Meetings_count_only_scheduled_ones_starting_inside_the_window()
    {
        // A window years ahead with a random hour, so no other test's meeting can fall inside it.
        var from = new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero).AddHours(Random.Shared.Next(0, 24 * 300));
        var to = from.AddDays(1);
        Meeting At(DateTimeOffset start) => Meeting.Schedule(Key("MTG"), "Digest seed", Meeting.SingleCommitteeId,
            Guid.NewGuid(), "Chair", start, start.AddHours(1), MeetingType.Regular, MeetingMode.InPerson, null, null, Now);

        await using (var db = _fx.NewMeetingsSql())
        {
            var cancelled = At(from.AddHours(2)); cancelled.Cancel("moved", Now);
            db.Meetings.AddRange(At(from), At(to), At(from.AddMinutes(-1)), cancelled);
            await db.SaveChangesAsync();
        }

        await using var read = _fx.NewMeetingsSql();
        (await new MeetingDigestSource(read).CountStartingAsync(from, to)).Should().Be(1);
    }
}
