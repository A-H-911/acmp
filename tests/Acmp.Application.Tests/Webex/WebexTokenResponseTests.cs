using Acmp.Modules.Integrations.Webex.Oauth;
using FluentAssertions;

namespace Acmp.Application.Tests.Webex;

// The OAuth token response record just carries the snake_case token fields off the wire.
public class WebexTokenResponseTests
{
    [Fact]
    public void Carries_the_access_refresh_and_expiry_fields()
    {
        var response = new WebexTokenResponse { AccessToken = "acc-1", RefreshToken = "ref-1", ExpiresIn = 3600 };

        response.AccessToken.Should().Be("acc-1");
        response.RefreshToken.Should().Be("ref-1");
        response.ExpiresIn.Should().Be(3600);

        // Exercise the synthesized copy-constructor (the record declaration line) too.
        var copy = response with { ExpiresIn = 60 };
        copy.AccessToken.Should().Be("acc-1");
        copy.ExpiresIn.Should().Be(60);
    }
}
