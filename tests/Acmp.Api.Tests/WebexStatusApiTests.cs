using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Integrations.Webex.Oauth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acmp.Api.Tests;

// GET /api/webex/status (P13, point 5): the SPA schedule form reads this to decide the join-URL field mode.
// Authenticated (RequireAuthorization — no FallbackPolicy exists); reports whether Webex is on and whether an
// online meeting will actually get an auto-created join URL (needs a stored OAuth token).
public class WebexStatusApiTests : IClassFixture<AcmpWebApplicationFactory>
{
    private sealed record StatusDto(bool Enabled, bool CanAutoCreate);

    private readonly AcmpWebApplicationFactory _factory;

    public WebexStatusApiTests(AcmpWebApplicationFactory factory) => _factory = factory;

    private static HttpClient Authed(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Secretary");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, "kc-secretary");
        return client;
    }

    private HttpClient EnabledClient(bool hasToken) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Webex:Enabled"] = "true",
                ["Webex:TokenEncryptionKey"] = "test-token-encryption-key-0123456789",
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IWebexTokenService>();
                services.AddSingleton<IWebexTokenService>(new FakeTokens(hasToken));
            });
        }).CreateClient();

    [Fact]
    public async Task Unauthenticated_is_unauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/webex/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task When_disabled_reports_not_enabled_and_no_auto_create()
    {
        // Base factory = Webex disabled: the handler short-circuits and never resolves the (unregistered) token service.
        var response = await Authed(_factory.CreateClient()).GetAsync("/api/webex/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<StatusDto>()).Should().Be(new StatusDto(false, false));
    }

    [Fact]
    public async Task When_enabled_with_a_stored_token_reports_can_auto_create()
    {
        var response = await Authed(EnabledClient(hasToken: true)).GetAsync("/api/webex/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<StatusDto>()).Should().Be(new StatusDto(true, true));
    }

    [Fact]
    public async Task When_enabled_without_a_token_cannot_auto_create()
    {
        var response = await Authed(EnabledClient(hasToken: false)).GetAsync("/api/webex/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<StatusDto>()).Should().Be(new StatusDto(true, false));
    }

    private sealed class FakeTokens : IWebexTokenService
    {
        private readonly bool _hasToken;
        public FakeTokens(bool hasToken) => _hasToken = hasToken;

        public Task StoreFromExchangeAsync(WebexTokenResponse token, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> GetValidAccessTokenAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<bool> HasTokenAsync(CancellationToken ct = default) => Task.FromResult(_hasToken);
    }
}
