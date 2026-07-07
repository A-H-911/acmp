using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Integrations.Webex.Oauth;

// Speaks the Webex OAuth token endpoint (POST /access_token): exchanges an authorization code for tokens and
// refreshes an expired access token. Seam so the token service is testable against a fake.
public interface IWebexOAuthClient
{
    Task<WebexTokenResponse?> ExchangeCodeAsync(string code, CancellationToken ct = default);

    Task<WebexTokenResponse?> RefreshAsync(string refreshToken, CancellationToken ct = default);
}

public sealed class WebexOAuthClient : IWebexOAuthClient
{
    private readonly HttpClient _http;
    private readonly WebexOptions _options;

    public WebexOAuthClient(HttpClient http, IOptions<WebexOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public Task<WebexTokenResponse?> ExchangeCodeAsync(string code, CancellationToken ct = default) =>
        PostAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.OAuthClientId,
            ["client_secret"] = _options.OAuthClientSecret,
            ["code"] = code,
            ["redirect_uri"] = _options.OAuthRedirectUri,
        }, ct);

    public Task<WebexTokenResponse?> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
        PostAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _options.OAuthClientId,
            ["client_secret"] = _options.OAuthClientSecret,
            ["refresh_token"] = refreshToken,
        }, ct);

    private async Task<WebexTokenResponse?> PostAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await _http.PostAsync("access_token", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new WebexApiException((int)response.StatusCode, body.Length > 300 ? body[..300] : body);
        }
        return await response.Content.ReadFromJsonAsync<WebexTokenResponse>(ct);
    }
}
