using Acmp.Modules.Risks.Application.Features.GetRisksRegister;
using Acmp.Modules.Risks.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Pagination;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Shared;

/*
 * DEF-104 — the caller-supplied page-size cap.
 *
 * Two halves, deliberately sharing no mechanism with PagedReadCapTests (LL-009): that test guards the
 * SET of paged reads by reflection; this one guards the MECHANISM by executing a real handler. A
 * reflection test cannot prove a handler clamps, and a single behavioural test cannot prove the other
 * ten do — so neither is sufficient alone and both are needed.
 */
public class PageSizeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, PageSize.Default)]        // caller supplied nothing meaningful
    [InlineData(-1, PageSize.Default)]       // negative is not a page
    [InlineData(int.MinValue, PageSize.Default)]
    [InlineData(1, 1)]                       // the smallest real page survives
    [InlineData(25, 25)]                     // the common case is untouched
    [InlineData(500, 500)]                   // exactly the cap survives - an off-by-one here would
                                             // silently narrow every page the SPA actually asks for
    [InlineData(501, PageSize.Max)]          // one over is capped
    [InlineData(int.MaxValue, PageSize.Max)] // the case the defect is about
    public void Clamp_bounds_the_caller_supplied_page(int requested, int expected)
        => PageSize.Clamp(requested).Should().Be(expected);

    [Fact]
    public void A_null_limit_falls_back_rather_than_becoming_zero()
        => PageSize.Clamp((int?)null).Should().Be(PageSize.Default);

    [Fact]
    public void An_over_large_fallback_is_itself_capped()
        // The fallback is caller-chosen at the call site, so it is not automatically trustworthy: a
        // read that passed a fallback above the cap would otherwise reintroduce the defect through
        // the parameter meant to prevent it.
        => PageSize.Clamp(0, fallback: 10_000).Should().Be(PageSize.Max);

    [Fact]
    public async Task A_real_paged_read_refuses_to_return_more_than_the_cap()
    {
        // THE BEHAVIOURAL HALF. GetRisksRegister is used because its handler takes only a DbContext -
        // the cheapest real paged read to drive. NO ROWS ARE SEEDED ON PURPOSE: the handler echoes the
        // SAME `pageSize` variable it passes to .Take() into PagedResult, so the echoed value is the
        // clamped value, and seeding rows would test the register rather than the cap. Before the fix
        // this returned PageSize = int.MaxValue; removing PageSize.Clamp makes this assertion fail.
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-secretary");
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        await using var db = new RisksDbContext(
            new DbContextOptionsBuilder<RisksDbContext>().UseInMemoryDatabase("pgsz-" + Guid.NewGuid()).Options,
            clock, user);

        var page = await new GetRisksRegisterHandler(db).Handle(
            new GetRisksRegisterQuery(PageSize: int.MaxValue), CancellationToken.None);

        page.PageSize.Should().Be(PageSize.Max);
    }
}
