namespace Acmp.Modules.Integrations.Webex.Oauth;

// The single persisted OAuth token row for the designated secretary integration (one committee, one bot/user
// integration). Tokens are stored encrypted (WebexTokenProtector). Id is fixed to 1 — there is only ever one.
public sealed class WebexToken
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string AccessTokenCipher { get; set; } = string.Empty;
    public string RefreshTokenCipher { get; set; } = string.Empty;
    public DateTimeOffset AccessTokenExpiresAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
