using Acmp.Shared.Contracts.Search;
using FluentAssertions;

namespace Acmp.Application.Tests;

// Unit coverage for the FR-144 matched-excerpt helper — every window branch.
public sealed class SearchExcerptTests
{
    [Fact]
    public void Text_shorter_than_the_window_is_returned_verbatim()
    {
        SearchExcerpt.Around("a short line", "x").Should().Be("a short line");
    }

    [Fact]
    public void Null_or_blank_text_returns_empty()
    {
        SearchExcerpt.Around(null, "x").Should().BeEmpty();
    }

    [Fact]
    public void Long_text_with_no_match_returns_a_leading_slice_with_trailing_ellipsis()
    {
        var text = new string('a', 100);
        var r = SearchExcerpt.Around(text, "zzz", 30);
        r.Should().StartWith(new string('a', 30)).And.EndWith("…");
    }

    [Fact]
    public void Match_in_the_middle_gets_leading_and_trailing_ellipses()
    {
        var text = new string('a', 40) + "TARGET" + new string('b', 40);
        var r = SearchExcerpt.Around(text, "target", 30);
        r.Should().StartWith("…").And.EndWith("…").And.Contain("TARGET");
    }

    [Fact]
    public void Match_at_the_start_has_no_leading_ellipsis()
    {
        var text = "TARGET" + new string('b', 100);
        var r = SearchExcerpt.Around(text, "TARGET", 30);
        r.Should().StartWith("TARGET").And.EndWith("…");
    }

    [Fact]
    public void Match_at_the_end_has_no_trailing_ellipsis()
    {
        var text = new string('a', 100) + "TARGET";
        var r = SearchExcerpt.Around(text, "TARGET", 30);
        r.Should().StartWith("…").And.EndWith("TARGET");
    }
}
