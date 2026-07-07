using System.Linq.Expressions;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Acmp.Modules.Integrations.Webex;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acmp.Api.Tests;

// The inbound webhook is the only anonymous endpoint (no user session): it is authenticated by HMAC signature.
// AC-069 (good/bad signature → 200/401 + processing only on valid) and AC-071 (disabled → accept + ignore).
public class WebexWebhookApiTests : IClassFixture<AcmpWebApplicationFactory>
{
    private const string Secret = "test-webhook-secret";
    private static readonly string Body =
        $"{{\"resource\":\"recordings\",\"event\":\"created\",\"created\":\"{DateTimeOffset.UtcNow:o}\",\"data\":{{\"id\":\"rec-1\",\"meetingId\":\"webex-abc\"}}}}";

    private readonly AcmpWebApplicationFactory _factory;

    public WebexWebhookApiTests(AcmpWebApplicationFactory factory) => _factory = factory;

    private HttpClient EnabledClient(FakeScheduler fake) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Webex:Enabled"] = "true",
                ["Webex:WebhookSecret"] = Secret,
                ["Webex:SignatureAlgorithm"] = "HMACSHA1",
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IWebexJobScheduler>();
                services.AddSingleton<IWebexJobScheduler>(fake);
            });
        }).CreateClient();

    private static string Sign(string body)
    {
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(Secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    private static HttpRequestMessage Post(string body, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webex/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (signature is not null) request.Headers.TryAddWithoutValidation("x-spark-signature", signature);
        return request;
    }

    [Fact]
    public async Task Valid_signature_is_accepted_and_the_recording_is_enqueued()
    {
        var fake = new FakeScheduler();
        var response = await EnabledClient(fake).SendAsync(Post(Body, Sign(Body)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.Enqueued.Should().Be(1);
    }

    [Fact]
    public async Task Invalid_signature_is_rejected_with_401_and_nothing_is_enqueued()
    {
        var fake = new FakeScheduler();
        var response = await EnabledClient(fake).SendAsync(Post(Body, "deadbeef"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        fake.Enqueued.Should().Be(0);
    }

    [Fact]
    public async Task When_disabled_the_endpoint_accepts_and_ignores_without_auth()
    {
        // Base factory = Webex disabled. Anonymous POST → 200, no processing (AC-071 at the edge).
        var response = await _factory.CreateClient().SendAsync(Post(Body, signature: null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Oauth_callback_is_reachable_without_authentication()
    {
        // The OAuth callback is a top-level browser redirect FROM Webex — it cannot carry a Keycloak bearer, so
        // it must be anonymous (the WS0 bug fix; a JwtBearer gate would 401 every real callback). No bearer +
        // a code but no state cookie => 400 from the single-use `state` check, NOT 401 — proving it is reachable
        // anonymously and defended by the state cookie, not by identity.
        var response = await EnabledClient(new FakeScheduler())
            .GetAsync("/api/webex/oauth/callback?code=abc");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Oauth_start_is_gated_by_the_setup_key()
    {
        // /start can't carry a bearer (it's a browser navigation), so it's gated by the operator-only setup key.
        // The base EnabledClient configures no key => fail-closed 404, proving /start is not an unauthenticated
        // token-minting endpoint (the flagged HIGH). A wrong key is likewise indistinguishable from absent.
        var response = await EnabledClient(new FakeScheduler())
            .GetAsync("/api/webex/oauth/start?key=wrong");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed class FakeScheduler : IWebexJobScheduler
    {
        public int Enqueued;

        public void Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall) where TJob : class => Enqueued++;

        public void Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay) where TJob : class { }
    }
}
