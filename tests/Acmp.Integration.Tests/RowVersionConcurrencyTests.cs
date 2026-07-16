using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Integration.Tests;

// ADR-0018 / docs/16 §1.5 — the optimistic-concurrency backstop. RowVersion is a SQL Server `rowversion`
// column, so only a REAL database enforces the stale-write check; EF InMemory ignores it entirely. Each
// test loads one aggregate in two contexts, commits the first edit (which bumps the rowversion), then
// commits the second (stale) edit. On SQL Server the second save throws DbUpdateConcurrencyException
// (the API maps this to 409, GlobalExceptionHandler); on InMemory the same stale save silently wins —
// the exact lost-update this slice closes.
[Collection(SqlBackstopCollection.Name)]
[Trait("Category", "Security")]
public sealed class RowVersionConcurrencyTests
{
    private readonly SqlBackstopFixture _fx;

    public RowVersionConcurrencyTests(SqlBackstopFixture fx) => _fx = fx;

    private static CommitteeMember NewMember() =>
        CommitteeMember.Provision(Guid.NewGuid().ToString("N"), "Member",
            $"{Guid.NewGuid():N}@acmp.gov", CommitteeRole.Member, DateTimeOffset.UtcNow);

    [Fact] // SQL Server: a stale write loses the optimistic-concurrency race → DbUpdateConcurrencyException
    public async Task StaleWrite_IsRejectedBySql()
    {
        long memberId;
        await using (var seed = _fx.NewMembershipSql())
        {
            var m = NewMember();
            seed.Members.Add(m);
            await seed.SaveChangesAsync();
            memberId = m.Id;
        }

        // Two independent readers load the same row at the same rowversion.
        await using var first = _fx.NewMembershipSql();
        await using var second = _fx.NewMembershipSql();
        var firstCopy = await first.Members.FirstAsync(m => m.Id == memberId);
        var secondCopy = await second.Members.FirstAsync(m => m.Id == memberId);
        var flipped = !firstCopy.IsVotingEligible; // flip relative to the loaded value so the edit is real

        // First write commits and bumps the rowversion.
        firstCopy.SetVotingEligibility(flipped);
        await first.SaveChangesAsync();

        // Second write carries the now-stale rowversion → rejected.
        secondCopy.SetVotingEligibility(flipped);
        await FluentActions.Awaiting(() => second.SaveChangesAsync())
            .Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact] // InMemory has no rowversion enforcement — the same stale write silently overwrites (false green)
    public async Task StaleWrite_IsSilentlyAcceptedByInMemory()
    {
        var dbName = nameof(StaleWrite_IsSilentlyAcceptedByInMemory);
        long memberId;
        await using (var seed = _fx.NewMembershipInMemory(dbName))
        {
            var m = NewMember();
            seed.Members.Add(m);
            await seed.SaveChangesAsync();
            memberId = m.Id;
        }

        await using var first = _fx.NewMembershipInMemory(dbName);
        await using var second = _fx.NewMembershipInMemory(dbName);
        var firstCopy = await first.Members.FirstAsync(m => m.Id == memberId);
        var secondCopy = await second.Members.FirstAsync(m => m.Id == memberId);
        var flipped = !firstCopy.IsVotingEligible;

        firstCopy.SetVotingEligibility(flipped);
        await first.SaveChangesAsync();

        secondCopy.SetVotingEligibility(flipped);
        await FluentActions.Awaiting(() => second.SaveChangesAsync()).Should().NotThrowAsync();
    }
}
