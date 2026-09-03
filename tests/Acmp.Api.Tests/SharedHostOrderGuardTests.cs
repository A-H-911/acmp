using FluentAssertions;
using NSubstitute;
using Xunit.Abstractions;

namespace Acmp.Api.Tests;

/// <summary>
/// The positive control for <see cref="SharedHostOrderGuard"/> — <c>DEF-131</c>'s precedent, applied
/// before the control is trusted rather than after it reports something.
/// </summary>
/// <remarks>
/// ⛔⛔ <c>LL-041</c> IS WHY THESE ASSERT ON A DIFFERENCE AND NOT ON AN OUTCOME. Showing that an
/// orderer RAN answers a different question from whether it can change anything, and a guard that
/// quietly returns its input unchanged passes every "did it execute" check while making the reversed
/// run byte-identical to the forward one. So the load-bearing assertion here is that the two directions
/// produce <b>different</b> sequences over the same input; everything else is detail.
/// </remarks>
public sealed class SharedHostOrderGuardTests
{
    private static ITestCase Case(string displayName)
    {
        var testCase = Substitute.For<ITestCase>();
        testCase.DisplayName.Returns(displayName);
        return testCase;
    }

    private static readonly string[] Names = ["C_third", "A_first", "B_second"];

    private static string[] Ordered(bool reverse) =>
        SharedHostOrderGuard.Order(Names.Select(Case).ToArray(), reverse)
            .Select(c => c.DisplayName)
            .ToArray();

    [Fact]
    public void Forward_order_is_ascending_by_display_name_regardless_of_discovery_order()
    {
        // Canonicalising first is what makes "reversed" a second DETERMINISTIC order rather than a
        // second sample of whatever the discoverer happened to emit (LL-044).
        Ordered(reverse: false).Should().Equal("A_first", "B_second", "C_third");
    }

    [Fact]
    public void Reverse_order_is_descending_by_display_name()
    {
        Ordered(reverse: true).Should().Equal("C_third", "B_second", "A_first");
    }

    [Fact]
    public void The_two_directions_actually_differ__which_is_the_only_property_that_makes_this_a_control()
    {
        // THE MUTATION THIS CATCHES: an Order() that ignores its `reverse` argument. Such a guard runs,
        // returns a plausible sequence, and reports a clean reversed suite forever — the shape of a
        // green control with no subject (DEF-078). Both directions being non-empty is asserted too, so
        // "differ" cannot be satisfied by one of them collapsing to nothing.
        var forward = Ordered(reverse: false);
        var reversed = Ordered(reverse: true);

        forward.Should().NotBeEmpty();
        reversed.Should().NotBeEmpty();
        reversed.Should().NotEqual(forward);
        reversed.Should().BeEquivalentTo(forward, "reversing must permute the set, never change it");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("forward", false)]
    [InlineData("reverse", true)]
    [InlineData("REVERSE", true)]
    public void The_environment_variable_is_read_case_insensitively_and_defaults_to_forward(
        string? value, bool expected)
    {
        var previous = Environment.GetEnvironmentVariable(SharedHostOrderGuard.OrderVariable);
        try
        {
            Environment.SetEnvironmentVariable(SharedHostOrderGuard.OrderVariable, value);
            SharedHostOrderGuard.ReverseRequested.Should().Be(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SharedHostOrderGuard.OrderVariable, previous);
        }
    }
}
