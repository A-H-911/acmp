using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Acmp.Api.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// P16-B4 request-pipeline hardening: proportional rate limiting (C-API-03) + read-only-FS-safe
// DataProtection key-ring (C-CON-003). Limits are lowered via config so the tests need only a few requests.
public sealed class HardeningApiTests
{
    // Layers a small rate-limit override onto the standard test host so a policy trips after 2 permits.
    // UseSetting writes into the host configuration, which minimal-hosting `builder.Configuration` reads at
    // service-registration time (more reliable here than ConfigureAppConfiguration's ordering).
    private static WebApplicationFactory<Program> WithLimit(AcmpWebApplicationFactory factory, string key, int permit) =>
        factory.WithWebHostBuilder(b => b.UseSetting($"RateLimiting:{key}", permit.ToString()));

    [Fact] // C-API-03 — the per-user search policy returns 429 + Retry-After past the window.
    public async Task Search_over_the_per_user_limit_returns_429_with_retry_after()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var client = WithLimit(factory, "SearchPermitPerMinute", 2).CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Member");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, "rate-user");

        (await client.GetAsync("/api/search?q=x")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/search?q=x")).StatusCode.Should().Be(HttpStatusCode.OK);

        var throttled = await client.GetAsync("/api/search?q=x");
        throttled.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        throttled.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Fact] // C-API-03 — the anonymous Webex webhook has ONE global bucket (no per-user sub to partition on).
    public async Task Webhook_over_the_global_limit_returns_429()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var client = WithLimit(factory, "WebhookPermitPerMinute", 2).CreateClient();

        // No valid HMAC signature: the first two are rejected by the signature filter (not 429), but the
        // rate limiter still counts them (it runs before the endpoint filter), so the third is throttled.
        async Task<HttpStatusCode> Post() =>
            (await client.PostAsync("/api/webex/webhook", new StringContent("{}"))).StatusCode;

        (await Post()).Should().NotBe(HttpStatusCode.TooManyRequests);
        (await Post()).Should().NotBe(HttpStatusCode.TooManyRequests);
        (await Post()).Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact] // C-CON-003 — no KeysPath => framework default; provider still round-trips.
    public void DataProtection_without_a_path_still_round_trips()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAcmpDataProtection(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");
        protector.Unprotect(protector.Protect("secret")).Should().Be("secret");
    }

    [Fact] // C-CON-003 — a configured KeysPath persists the key ring there (the writable tmpfs mount in prod).
    public void DataProtection_with_a_path_persists_the_key_ring_there()
    {
        var dir = Path.Combine(Path.GetTempPath(), "acmp-dp-" + Guid.NewGuid());
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["DataProtection:KeysPath"] = dir })
                .Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAcmpDataProtection(config);

            using var provider = services.BuildServiceProvider();
            // First Protect forces key generation, which persists an XML key file to the configured directory.
            provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("x");

            Directory.Exists(dir).Should().BeTrue();
            Directory.GetFiles(dir, "*.xml").Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
