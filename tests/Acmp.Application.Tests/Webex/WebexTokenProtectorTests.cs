using Acmp.Modules.Integrations.Webex;
using Acmp.Modules.Integrations.Webex.Oauth;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Acmp.Application.Tests.Webex;

// OAuth tokens are encrypted at rest (AES-GCM). Round-trips exactly; the stored form is not the plaintext.
public class WebexTokenProtectorTests
{
    private static WebexTokenProtector Protector() =>
        new(Options.Create(new WebexOptions { TokenEncryptionKey = "unit-test-key-material" }));

    [Fact]
    public void Round_trips_a_token()
    {
        var protector = Protector();
        const string token = "N2Y...secret-refresh-token";

        var cipher = protector.Protect(token);
        cipher.Should().NotContain(token);
        protector.Unprotect(cipher).Should().Be(token);
    }

    [Fact]
    public void Produces_distinct_ciphertexts_for_the_same_input()
    {
        var protector = Protector();
        // Random nonce per encryption → two ciphertexts differ but both decrypt back.
        protector.Protect("x").Should().NotBe(protector.Protect("x"));
    }
}
