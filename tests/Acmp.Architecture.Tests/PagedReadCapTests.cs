using System.Reflection;
using Acmp.Shared.Application.Pagination;
using FluentAssertions;
using MediatR;

namespace Acmp.Architecture.Tests;

/*
 * DEF-104 — EVERY PAGED READ CAPS THE CALLER-SUPPLIED PAGE SIZE.
 *
 * THE DEFECT THIS GUARDS. Eleven register reads guarded only the LOWER bound —
 * `pageSize = request.PageSize <= 0 ? 25 : request.PageSize` — and then called `.Take(pageSize)`.
 * Nothing rejected or clamped a large value, so an authenticated caller could ask for any page size
 * and the query would attempt to materialize it. Two reads already clamped (GetNotifications, and
 * search via MaxTakePerType), so the codebase knew the answer in two places and had not applied it in
 * eleven. The fix put the bound in one shared place, PageSize.Clamp; this test is what stops the
 * TWELFTH paged read from forgetting.
 *
 * ⚠ DISCOVERY IS BY REFLECTION, ACKNOWLEDGEMENT IS BY LIST, and the split is deliberate — the same
 * shape AggregateReachabilityTests uses, and for the same reason. A hard-coded list of what EXISTS
 * would have to be updated by the same person who forgot to clamp, which is precisely the failure
 * mode. So the SET is discovered from the assemblies: any request type returning a PagedResult<T> and
 * carrying an `int PageSize` is found the moment it compiles. What the list holds is only the
 * acknowledgement that each discovered type's handler has been checked. A new paged read therefore
 * turns this test RED until someone clamps it and says so here.
 *
 * ⚠⚠ WHAT THIS TEST DOES NOT PROVE, stated rather than left to be discovered: it does not execute the
 * handlers, so it cannot prove any given handler actually calls PageSize.Clamp. That is proven
 * behaviourally in PageSizeTests, which drives a real handler with int.MaxValue and asserts the page
 * comes back bounded. This test guards the SET; that one guards the MECHANISM. Neither is sufficient
 * alone and they share no mechanism, which is the point (LL-009).
 */
public class PagedReadCapTests
{
    private static readonly Assembly[] ApplicationAssemblies =
    [
        typeof(Acmp.Modules.Topics.Application.TopicsApplicationExtensions).Assembly,
        typeof(Acmp.Modules.Actions.Application.ActionsApplicationExtensions).Assembly,
        typeof(Acmp.Modules.Decisions.Application.DecisionsApplicationExtensions).Assembly,
        typeof(Acmp.Modules.Notifications.Application.NotificationsApplicationExtensions).Assembly,
        typeof(Acmp.Modules.Risks.Application.RisksApplicationExtensions).Assembly,
        typeof(Acmp.Modules.Dependencies.Application.DependenciesApplicationExtensions).Assembly,
        typeof(Acmp.Modules.Governance.Application.GovernanceApplicationExtensions).Assembly,
        typeof(Acmp.Modules.Knowledge.Application.KnowledgeApplicationExtensions).Assembly,
        typeof(Acmp.Modules.Research.Application.ResearchApplicationExtensions).Assembly,
    ];

    // Acknowledged: each of these has been read and its handler clamps through PageSize.Clamp.
    // ⚠ Adding a name here without clamping the handler defeats the test — the acknowledgement is a
    // statement that someone looked, and it is worth exactly as much as that looking.
    private static readonly HashSet<string> Clamped =
    [
        "GetBacklogQuery",
        "GetActionsRegisterQuery",
        "GetRisksRegisterQuery",
        "GetDependenciesRegisterQuery",
        "GetAdrsRegisterQuery",
        "GetInvariantsRegisterQuery",
        "GetDocumentsRegisterQuery",
        "GetTemplatesRegisterQuery",
        "GetMissionsRegisterQuery",
        "GetNotificationsQuery",
    ];

    // ⚠ DISCOVERY IS KEYED ON THE INPUT SHAPE, NOT THE OUTPUT, AND THE FIRST VERSION GOT THIS WRONG.
    // It required the request to return PagedResult<T>, which structurally EXCLUDED GetNotificationsQuery
    // - the one read that was already clamped, because it returns a bespoke NotificationListDto. A guard
    // whose discovery cannot see a correctly-shaped read also cannot see a future BROKEN one shaped the
    // same way, so the narrowing was the whole failure. What defines the risk is a caller-supplied page
    // size arriving on a request, so that is what this matches (LL-015: a scan's scope is part of its
    // answer, and the discovery test below is what caught it).
    private static IEnumerable<Type> PagedRequests() =>
        ApplicationAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetInterfaces().Any(i => i == typeof(IBaseRequest)))
            .Where(t => t.GetProperty("PageSize")?.PropertyType == typeof(int));

    /*
     * ⚠ TWO PAGED READS ARE OUTSIDE THIS TEST'S REACH AND ARE NAMED RATHER THAN SILENTLY MISSED.
     * Both are clamped in the fix, neither is a request type this reflection can see:
     *   - the audit list endpoint pages with a LOCAL named `size` inside a minimal-API delegate, so
     *     there is no request type to discover at all;
     *   - GetDecisionsQuery carries `int? Limit`, not an `int PageSize`.
     * Widening the predicate to catch them would mean matching any int-ish property on any request,
     * which would sweep in unrelated commands. They are covered by PageSize.Clamp at the call site and
     * by this comment; if a third such shape appears, this list is where it belongs.
     */

    [Fact]
    public void The_discovery_finds_something_so_a_clean_result_is_not_a_clean_scan_over_nothing()
    {
        // Trap 31 / LL-013: a gate with no subject must fail rather than pass. If a refactor renames
        // PagedResult or moves the query types, the reflection above quietly returns EMPTY and the
        // real assertion below would pass over nothing. This is the guard on the guard.
        PagedRequests().Should().HaveCountGreaterThanOrEqualTo(10,
            "the paged-read discovery must actually find the paged reads, or the next test is vacuous");
    }

    [Fact]
    public void Every_paged_request_type_has_had_its_page_size_cap_acknowledged()
    {
        var found = PagedRequests().Select(t => t.Name).ToHashSet();

        found.Except(Clamped).Should().BeEmpty(
            "a paged read whose caller-supplied PageSize is not clamped lets an authenticated caller "
            + "make the server attempt an arbitrary materialization (DEF-104). Clamp it with "
            + "PageSize.Clamp and then add it to the acknowledged set in this test.");

        // The list must not rot in the other direction either: a name here that no longer exists is a
        // stale entry that would silently excuse a future type with the same name.
        Clamped.Except(found).Should().BeEmpty(
            "this acknowledgement list names a request type that no longer exists — remove it rather "
            + "than leaving an entry that would pre-approve anything later given the same name");
    }
}
