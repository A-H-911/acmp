using Xunit.Abstractions;
using Xunit.Sdk;

// Assembly-wide, so the control reaches every converted class at once rather than needing an attribute
// per file — the "cheap enough to run on all forty" property DEC-124 d2 required of it.
[assembly: TestCaseOrderer(
    Acmp.Api.Tests.SharedHostOrderGuard.TypeName,
    Acmp.Api.Tests.SharedHostOrderGuard.AssemblyName)]

namespace Acmp.Api.Tests;

/// <summary>
/// The mechanical control <c>DEC-124</c> d2 obliges <c>WBS-27.2</c> to ship, and it is keyed on the
/// conversion's ACTUAL hazard rather than on its symptom.
/// </summary>
/// <remarks>
/// <para>
/// ⚠⚠ WHAT THE CONVERSION CHANGED, AND WHY A GREEN SUITE PROVES NOTHING ABOUT IT. Before
/// <c>WBS-27.2</c> every test method built its own <see cref="AcmpWebApplicationFactory"/> and
/// therefore its own private set of fourteen empty InMemory databases. That isolation was free and
/// accidental. Per-class <c>IClassFixture</c> sharing takes ~294 hosts to ~50, and the price is that
/// every method in a class now sees whatever its siblings wrote. <c>LL-032</c> is the standing warning
/// that <b>the dangerous outcome of getting this wrong is a PASS, not a failure</b> — a test that
/// silently starts asserting over another test's rows still goes green, and stays green, until the day
/// somebody reorders or deletes a sibling.
/// </para>
/// <para>
/// ⭐ THE CONTROL. Shared state can only change a result by making one test depend on ANOTHER TEST
/// HAVING RUN FIRST. So run the same tests in a different order and require the same verdict. Set
/// <c>ACMP_TEST_ORDER=reverse</c> and every class's methods run in descending name order instead of
/// ascending; anything that passes forward and fails reversed is coupled through the shared host, which
/// is precisely the defect the conversion risks introducing and precisely the one a normal run cannot
/// see. It is one attribute and one class, so it costs nothing to run over all forty converted files at
/// once — the property <c>DEC-124</c> d2 asked for.
/// </para>
/// <para>
/// ⛔ WHAT IT CANNOT DO, STATED HERE RATHER THAN DISCOVERED LATER (<c>LL-055</c>: the artefact carries
/// its own limits, so a reader can tell a silence that refutes from one that could not have spoken).
/// Reversal is ONE alternative order, not all of them. A pair of tests coupled in a way that survives
/// both ascending and descending order is invisible to this control. It proves the absence of
/// order-dependence <i>under one permutation</i>, which is strictly more than a green forward run
/// proves and strictly less than isolation gave away.
/// </para>
/// <para>
/// ⚠ Both orders are canonicalised by method name before use. xUnit does not promise a stable incoming
/// order, so sorting first is what makes "forward" and "reversed" two DETERMINISTIC and genuinely
/// different runs rather than two samples of an arbitrary one (<c>LL-044</c>).
/// </para>
/// <para>
/// Default behaviour is ascending name order — deterministic, but no reordering hazard of its own — so
/// an ordinary <c>dotnet test</c> is unaffected and CI runtime is unchanged.
/// </para>
/// </remarks>
public sealed class SharedHostOrderGuard : ITestCaseOrderer
{
    /// <summary>Assembly-qualified names for the <c>[assembly: TestCaseOrderer]</c> attribute.</summary>
    public const string TypeName = "Acmp.Api.Tests.SharedHostOrderGuard";

    /// <inheritdoc cref="TypeName"/>
    public const string AssemblyName = "Acmp.Api.Tests";

    /// <summary>The environment variable that flips the order, and the one value that does it.</summary>
    public const string OrderVariable = "ACMP_TEST_ORDER";

    /// <inheritdoc cref="OrderVariable"/>
    public const string ReverseValue = "reverse";

    /// <summary>
    /// True when this process was asked to run classes in reverse. Read per call rather than cached so
    /// the guard's own tests can exercise both branches without a process restart.
    /// </summary>
    internal static bool ReverseRequested =>
        string.Equals(
            Environment.GetEnvironmentVariable(OrderVariable),
            ReverseValue,
            StringComparison.OrdinalIgnoreCase);

    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
        => Order(testCases, ReverseRequested);

    /// <summary>
    /// The ordering itself, with the direction passed in so it is testable without touching the
    /// environment. Sorting by the display name — which carries the class, the method and any theory
    /// arguments — keeps theory cases stable relative to one another instead of leaving them in
    /// whatever order the discoverer produced.
    /// </summary>
    internal static IEnumerable<TTestCase> Order<TTestCase>(IEnumerable<TTestCase> testCases, bool reverse)
        where TTestCase : ITestCase
    {
        var ascending = testCases.OrderBy(c => c.DisplayName, StringComparer.Ordinal);
        return reverse ? ascending.Reverse() : ascending;
    }
}
