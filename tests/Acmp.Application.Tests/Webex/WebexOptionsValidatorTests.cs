using Acmp.Modules.Integrations.Webex;
using FluentAssertions;

namespace Acmp.Application.Tests.Webex;

// M1 (fail-closed): when the adapter is ENABLED, the token-encryption key must be a real secret — present,
// >= 16 chars, and not the CHANGE_ME placeholder — else the persisted OAuth tokens would be encrypted under a
// publicly derivable key. When disabled, no key is required (the adapter isn't wired).
public class WebexOptionsValidatorTests
{
    private static readonly WebexOptionsValidator Validator = new();

    [Theory]
    [InlineData("")]                        // missing
    [InlineData("short-key")]               // < 16 chars
    [InlineData("CHANGE_ME_IN_ENV_please")] // shipped placeholder (>= 16 chars but still a placeholder)
    public void Enabled_with_a_weak_or_placeholder_key_fails(string key)
    {
        var result = Validator.Validate(null, new WebexOptions { Enabled = true, TokenEncryptionKey = key });

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Enabled_with_a_strong_key_succeeds()
    {
        var result = Validator.Validate(null,
            new WebexOptions { Enabled = true, TokenEncryptionKey = "a-strong-random-32-char-secret-value" });

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Disabled_needs_no_key()
    {
        var result = Validator.Validate(null, new WebexOptions { Enabled = false, TokenEncryptionKey = "" });

        result.Succeeded.Should().BeTrue();
    }
}
