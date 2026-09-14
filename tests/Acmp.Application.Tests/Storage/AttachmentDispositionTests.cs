using Acmp.Shared.Infrastructure.FileStorage;
using FluentAssertions;

namespace Acmp.Application.Tests.Storage;

// AC-164 / AC-165: the Content-Disposition a download link is signed with. `filename*` carries the real name in
// UTF-8 (RFC 5987) so an Arabic name survives; the quoted `filename` is an ASCII fallback that cannot break the
// header's quoting.
public class AttachmentDispositionTests
{
    [Fact]
    public void An_ascii_name_is_carried_in_both_forms()
    {
        AttachmentDisposition.For("minutes 2026.pdf")
            .Should().Be("attachment; filename=\"minutes 2026.pdf\"; filename*=UTF-8''minutes%202026.pdf");
    }

    [Fact]
    public void An_arabic_name_is_percent_encoded_in_utf8_with_an_ascii_fallback()
    {
        var header = AttachmentDisposition.For("تقرير.pdf");

        header.Should().StartWith("attachment; filename=\"_____.pdf\"; filename*=UTF-8''");
        Uri.UnescapeDataString(header[(header.IndexOf("UTF-8''", StringComparison.Ordinal) + 7)..]).Should().Be("تقرير.pdf");
    }

    [Fact]
    public void A_quote_or_backslash_cannot_break_the_quoted_fallback()
    {
        var header = AttachmentDisposition.For("a\"b\\c.pdf");

        header.Should().Contain("filename=\"a_b_c.pdf\"");
        header.Should().Contain("filename*=UTF-8''a%22b%5Cc.pdf");
    }
}
