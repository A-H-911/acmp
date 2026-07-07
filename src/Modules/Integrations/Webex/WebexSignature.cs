using System.Security.Cryptography;
using System.Text;

namespace Acmp.Modules.Integrations.Webex;

// Verifies the Webex x-spark-signature: an HMAC hex digest of the RAW request body keyed with the webhook
// secret (webex-feasibility.md §3.1). Default algorithm HMAC-SHA1 (Webex default); SHA256/512 accepted if the
// webhook was created with them. Constant-time comparison. Pure + static so the security logic is unit-tested
// directly, independent of ASP.NET plumbing.
public static class WebexSignature
{
    public static bool IsValid(string algorithm, string secret, string body, string? providedHex)
    {
        if (string.IsNullOrWhiteSpace(providedHex) || string.IsNullOrEmpty(secret))
            return false;

        using var hmac = Create(algorithm, Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(providedHex.Trim().ToLowerInvariant()));
    }

    private static HMAC Create(string algorithm, byte[] key) => algorithm.ToUpperInvariant() switch
    {
        "HMACSHA256" => new HMACSHA256(key),
        "HMACSHA512" => new HMACSHA512(key),
        _ => new HMACSHA1(key),
    };
}
