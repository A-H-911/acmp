using System.Security.Cryptography;
using System.Text;
using Acmp.Modules.Integrations.Webex;
using FluentAssertions;

namespace Acmp.Application.Tests.Webex;

// The inbound-webhook security control: HMAC over the raw body (SHA1 default, SHA256 accepted), constant-time.
// A tampered body, wrong secret, or missing signature must all be rejected.
public class WebexSignatureTests
{
    private const string Secret = "s3cr3t-webhook-key";
    private const string Body = "{\"resource\":\"recordings\",\"event\":\"created\",\"data\":{\"id\":\"rec-1\"}}";

    private static string Sign(string algorithm, string secret, string body)
    {
        using HMAC hmac = algorithm switch
        {
            "HMACSHA256" => new HMACSHA256(Encoding.UTF8.GetBytes(secret)),
            _ => new HMACSHA1(Encoding.UTF8.GetBytes(secret)),
        };
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    [Theory]
    [InlineData("HMACSHA1")]
    [InlineData("HMACSHA256")]
    public void Accepts_a_correct_signature(string algorithm)
    {
        var signature = Sign(algorithm, Secret, Body);
        WebexSignature.IsValid(algorithm, Secret, Body, signature).Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_tampered_body()
    {
        var signature = Sign("HMACSHA1", Secret, Body);
        WebexSignature.IsValid("HMACSHA1", Secret, Body + " ", signature).Should().BeFalse();
    }

    [Fact]
    public void Rejects_a_wrong_secret()
    {
        var signature = Sign("HMACSHA1", "other-secret", Body);
        WebexSignature.IsValid("HMACSHA1", Secret, Body, signature).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Rejects_a_missing_signature(string? provided)
    {
        WebexSignature.IsValid("HMACSHA1", Secret, Body, provided).Should().BeFalse();
    }
}
