using System.Text.Json.Serialization;

namespace Acmp.Modules.Integrations.Webex.Oauth;

// The Webex OAuth token endpoint response (developer.webex.com — integrations). snake_case on the wire.
public sealed record WebexTokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
}
