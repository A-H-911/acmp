using System.Reflection;
using Acmp.Modules.Decisions.Domain;
using Acmp.Modules.Decisions.Infrastructure.Integrity;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Decisions;

// D-13 / C-INS-02 (ADR-0030) — the Decisions integrity check round-trips through the real DecisionsDbContext
// (InMemory, short-lived contexts on a shared named store, mirroring VoteHandlerTests). It must scan only
// sealed (closed) votes and flag a direct-SQL edit of a frozen ballot or a forged tally.
[Trait("Category", "Security")]
public class VoteChainIntegrityCheckTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Topic = Guid.NewGuid();

    private static readonly VoteEligibleVoter[] Voters =
    {
        new("kc-alice", "Alice"), new("kc-bob", "Bob"), new("kc-carol", "Carol"),
    };

    private static DecisionsDbContext Db(string name)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-sec");
        user.DisplayName.Returns("Sec");
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        return new DecisionsDbContext(
            new DbContextOptionsBuilder<DecisionsDbContext>().UseInMemoryDatabase(name).Options, clock, user);
    }

    private static Vote ClosedVote(string key)
    {
        var v = Vote.Configure(key, Topic, null, new[] { "Approve", "Reject" }, true,
            new QuorumRule(0, 2), Voters, "kc-sec", Now);
        v.Open("kc-sec", 3, Now);
        v.Cast("kc-alice", "Approve", null, Now);
        v.Cast("kc-bob", "Reject", null, Now);
        v.Cast("kc-carol", "Approve", null, Now);
        v.Close("kc-sec", "Sec", Now.AddHours(1));
        return v;
    }

    private static Vote OpenVote(string key)
    {
        var v = Vote.Configure(key, Topic, null, new[] { "Approve", "Reject" }, true,
            new QuorumRule(0, 1), Voters, "kc-sec", Now);
        v.Open("kc-sec", 3, Now);
        return v;
    }

    [Fact]
    public async Task Scans_only_sealed_votes_and_passes_when_intact()
    {
        var name = Guid.NewGuid().ToString();
        await using (var db = Db(name))
        {
            db.Votes.Add(ClosedVote("VOTE-2026-001"));
            db.Votes.Add(OpenVote("VOTE-2026-002")); // unsealed -> not scanned
            await db.SaveChangesAsync();
        }

        await using (var db = Db(name))
        {
            var result = await new VoteChainIntegrityCheck(db).RunAsync();
            result.IsValid.Should().BeTrue();
            result.Scanned.Should().Be(1); // only the sealed vote
        }
    }

    [Fact]
    public async Task Flags_a_direct_edit_of_a_frozen_ballot()
    {
        var name = Guid.NewGuid().ToString();
        await using (var db = Db(name))
        {
            db.Votes.Add(ClosedVote("VOTE-2026-003"));
            await db.SaveChangesAsync();
        }

        await using (var db = Db(name))
        {
            var vote = await db.Votes.FirstAsync(v => v.ChainSealedAt != null);
            var ballot = vote.Ballots.OrderBy(b => b.VoterUserId, StringComparer.Ordinal).First();
            typeof(Ballot).GetProperty("Choice",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(ballot, "Reject");
            await db.SaveChangesAsync();
        }

        await using (var db = Db(name))
        {
            var result = await new VoteChainIntegrityCheck(db).RunAsync();
            result.IsValid.Should().BeFalse();
            result.FirstFailure.Should().Contain("VOTE-2026-003");
        }
    }

    [Fact]
    public async Task Flags_a_forged_tally_that_no_longer_matches_the_ballots()
    {
        var name = Guid.NewGuid().ToString();
        await using (var db = Db(name))
        {
            db.Votes.Add(ClosedVote("VOTE-2026-004"));
            await db.SaveChangesAsync();
        }

        await using (var db = Db(name))
        {
            var vote = await db.Votes.FirstAsync(v => v.ChainSealedAt != null);
            typeof(Vote).GetProperty("Tally",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(vote, new VoteTally(new Dictionary<string, int> { ["Approve"] = 3, ["Reject"] = 0 }, 0, 3));
            await db.SaveChangesAsync();
        }

        await using (var db = Db(name))
        {
            var result = await new VoteChainIntegrityCheck(db).RunAsync();
            result.IsValid.Should().BeFalse();
            result.FirstFailure.Should().Contain("tally");
        }
    }
}
