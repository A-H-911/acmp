using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Integrations.Webex.Oauth;

// Encrypts the persisted OAuth access/refresh tokens at rest (AES-GCM, authenticated). The 256-bit key is
// derived from the configured secret (Webex:TokenEncryptionKey) via SHA-256 so any-length config is accepted.
// Never log or expose the plaintext tokens (INV-005 / security-controls.md).
public sealed class WebexTokenProtector
{
    private readonly byte[] _key;

    public WebexTokenProtector(IOptions<WebexOptions> options) =>
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.TokenEncryptionKey ?? string.Empty));

    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);   // 12 bytes
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];                             // 16 bytes
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];

        using var gcm = new AesGcm(_key, tag.Length);
        gcm.Encrypt(nonce, plain, cipher, tag);

        var blob = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, blob, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, blob, nonce.Length + tag.Length, cipher.Length);
        return Convert.ToBase64String(blob);
    }

    public string Unprotect(string protectedValue)
    {
        var blob = Convert.FromBase64String(protectedValue);
        var nonce = blob.AsSpan(0, 12);
        var tag = blob.AsSpan(12, 16);
        var cipher = blob.AsSpan(28);
        var plain = new byte[cipher.Length];

        using var gcm = new AesGcm(_key, 16);
        gcm.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
