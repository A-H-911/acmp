using System.Net;
using Acmp.Modules.Integrations.Webex.Oauth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acmp.Api.Tests;

// The OAuth consent flow (WS3b): /start is a key-gated browser redirect that mints a single-use `state` cookie;
// /callback is anonymous but only completes a flow whose state cookie it echoes, then exchanges the code and
// audits the link (INV-005). Both are top-level navigations so neither carries a Keycloak bearer.
public class WebexOAuthApiTests : IClassFixture<AcmpWebApplicationFactory>
{
    private const string SetupKey = "op-setup-key";
    private const string StateCookie = "webex_oauth_state";

    private readonly AcmpWebApplicationFactory _factory;

    public WebexOAuthApiTests(AcmpWebApplicationFactory factory) => _factory = factory;

    // Enabled adapter with the operator setup key configured; the OAuth client + token store are faked so the
    // flow never touches the real SqlServer WebexDbContext or reaches out to Webex. AllowAutoRedirect is off so
    // the /start 302 (to the external Webex authorize URL) is observed rather than followed; cookies are handled
    // manually because the Secure state cookie would be dropped by a CookieContainer over the http test host.
    private HttpClient EnabledClient(IWebexOAuthClient oauth, IWebexTokenService tokens) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Webex:Enabled"] = "true",
                ["Webex:TokenEncryptionKey"] = "test-token-encryption-key-0123456789",
                ["Webex:OAuthSetupKey"] = SetupKey,
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IWebexOAuthClient>();
                services.AddSingleton(oauth);
                services.RemoveAll<IWebexTokenService>();
                services.AddSingleton(tokens);
            });
        }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task Start_when_disabled_returns_bad_request()
    {
        // Base factory = Webex disabled: /start short-circuits before the key gate.
        var response = await _factory.CreateClient().GetAsync("/api/webex/oauth/start?key=anything");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_with_the_setup_key_redirects_and_sets_the_state_cookie()
    {
        var response = await EnabledClient(new FakeOAuthClient(null), new FakeTokens())
            .GetAsync($"/api/webex/oauth/start?key={SetupKey}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/authorize?client_id=");
        response.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith(StateCookie + "="));
    }

    [Fact]
    public async Task Start_with_the_wrong_key_is_not_found()
    {
        // Fail-closed: a mismatched key is indistinguishable from an absent one (don't reveal the endpoint).
        var response = await EnabledClient(new FakeOAuthClient(null), new FakeTokens())
            .GetAsync("/api/webex/oauth/start?key=wrong");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Callback_when_disabled_returns_bad_request()
    {
        var response = await _factory.CreateClient().GetAsync("/api/webex/oauth/callback?code=abc&state=x");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_without_a_code_returns_bad_request()
    {
        var response = await EnabledClient(new FakeOAuthClient(null), new FakeTokens())
            .GetAsync("/api/webex/oauth/callback?state=x");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_without_the_state_cookie_returns_bad_request()
    {
        // A code with no matching single-use state cookie is rejected before any token exchange (OAuth CSRF guard).
        var response = await EnabledClient(new FakeOAuthClient(null), new FakeTokens())
            .GetAsync("/api/webex/oauth/callback?code=abc&state=x");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_completes_the_flow_stores_the_token_and_returns_content()
    {
        var token = new WebexTokenResponse { AccessToken = "at", RefreshToken = "rt", ExpiresIn = 3600 };
        var tokens = new FakeTokens();
        var client = EnabledClient(new FakeOAuthClient(token), tokens);

        var (cookie, state) = await StartAndCaptureStateAsync(client);
        var response = await SendCallbackAsync(client, code: "auth-code", state: state, cookie: cookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("authorized");
        tokens.Stored.Should().BeTrue();
    }

    [Fact]
    public async Task Callback_returns_bad_request_when_the_token_exchange_yields_nothing()
    {
        var tokens = new FakeTokens();
        var client = EnabledClient(new FakeOAuthClient(null), tokens);

        var (cookie, state) = await StartAndCaptureStateAsync(client);
        var response = await SendCallbackAsync(client, code: "auth-code", state: state, cookie: cookie);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        tokens.Stored.Should().BeFalse();
    }

    // Drive /start with the setup key to mint a state cookie, then return the raw cookie pair + its value so the
    // callback can echo both the Cookie header and the matching `state` query param (same browser, same flow).
    private static async Task<(string CookiePair, string State)> StartAndCaptureStateAsync(HttpClient client)
    {
        var start = await client.GetAsync($"/api/webex/oauth/start?key={SetupKey}");
        start.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var cookiePair = start.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith(StateCookie + "="))
            .Split(';')[0];
        var state = cookiePair.Split('=', 2)[1];
        return (cookiePair, state);
    }

    private static Task<HttpResponseMessage> SendCallbackAsync(HttpClient client, string code, string state, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/webex/oauth/callback?code={code}&state={state}");
        request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request);
    }

    private sealed class FakeOAuthClient : IWebexOAuthClient
    {
        private readonly WebexTokenResponse? _token;
        public FakeOAuthClient(WebexTokenResponse? token) => _token = token;

        public Task<WebexTokenResponse?> ExchangeCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(_token);

        public Task<WebexTokenResponse?> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
            Task.FromResult<WebexTokenResponse?>(null);
    }

    private sealed class FakeTokens : IWebexTokenService
    {
        public bool Stored;

        public Task StoreFromExchangeAsync(WebexTokenResponse token, CancellationToken ct = default)
        {
            Stored = true;
            return Task.CompletedTask;
        }

        public Task<string?> GetValidAccessTokenAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<bool> HasTokenAsync(CancellationToken ct = default) => Task.FromResult(false);
    }
}
