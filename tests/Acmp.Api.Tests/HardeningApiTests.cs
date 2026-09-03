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
public sealed class HardeningApiTests : IClassFixture<AcmpWebApplicationFactory>
{
    // WBS-27.2 / DEC-124 d1 - ONE host per class instead of one per test method. Every method
    // below now shares this host's fourteen InMemory databases, so a test that asserts over a
    // global count sees what its siblings wrote. SharedHostOrderGuard is the control for that.
    private readonly AcmpWebApplicationFactory _factory;

    public HardeningApiTests(AcmpWebApplicationFactory factory) => _factory = factory.Reset();

    // Layers a small rate-limit override onto the standard test host so a policy trips after 2 permits.
    // UseSetting writes into the host configuration, which minimal-hosting `builder.Configuration` reads at
    // service-registration time (more reliable here than ConfigureAppConfiguration's ordering).
    private static WebApplicationFactory<Program> WithLimit(AcmpWebApplicationFactory factory, string key, int permit) =>
        factory.WithWebHostBuilder(b => b.UseSetting($"RateLimiting:{key}", permit.ToString()));

    // DEF-122: the requests are issued CONCURRENTLY and the assertion is on the COUNT of throttled
    // responses, never on WHICH one is throttled. Both halves are load-bearing, and both were measured.
    //
    // Concurrent, because a FixedWindowRateLimiter's window opens when its partition is first ACQUIRED —
    // middleware, at the start of request 1, before the endpoint filter. Sequential requests whose spacing
    // nothing bounds can straddle the boundary, and then the limit under test is never reached inside one
    // window. Isolated: a 65s gap between request 1 and request 2 makes the last response 200, while the
    // SAME delay placed BEFORE request 1 leaves it 429 — identical wall clock, opposite outcomes, so the
    // variable is elapsed time inside the window and not slowness. Issued together, every permit is taken
    // within microseconds and the window cannot roll mid-sequence however slow the host is.
    //
    // Count, not position, because under concurrency the rejected request is NOT deterministically the
    // last: over five runs the 429 landed at index 2, 0, 0, 2, 0.
    private static Task<HttpResponseMessage[]> IssueConcurrently(int count, Func<Task<HttpResponseMessage>> send) =>
        Task.WhenAll(Enumerable.Range(0, count).Select(_ => send()));

    [Fact] // C-API-03 — the per-user search policy returns 429 + Retry-After past the limit.
    public async Task Search_over_the_per_user_limit_returns_429_with_retry_after()
    {
        var factory = _factory;
        var client = WithLimit(factory, "SearchPermitPerMinute", 2).CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Member");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, "rate-user");

        var responses = await IssueConcurrently(3, () => client.GetAsync("/api/search?q=x"));

        responses.Where(r => r.StatusCode == HttpStatusCode.OK).Should().HaveCount(2);
        var throttled = responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests).ToArray();
        throttled.Should().ContainSingle("2 permits against 3 concurrent requests leaves exactly one rejected");
        throttled[0].Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Fact] // C-API-03 — the anonymous Webex webhook has ONE global bucket (no per-user sub to partition on).
    public async Task Webhook_over_the_global_limit_returns_429()
    {
        var factory = _factory;
        var client = WithLimit(factory, "WebhookPermitPerMinute", 2).CreateClient();

        // These carry no valid HMAC signature, and the Webex adapter is OFF in the test host — so
        // WebexSignatureFilter's `if (!_options.Enabled) return Results.Ok()` arm answers 200 and ignores
        // the body. An un-throttled POST here is therefore 200, not 401. The limiter counts every one of
        // them regardless: it is middleware and takes the permit before the endpoint filter ever runs,
        // which is the property this test exists to prove. See IssueConcurrently for why these are
        // concurrent and why the assertion counts rather than positions (DEF-122).
        var responses = await IssueConcurrently(
            3, () => client.PostAsync("/api/webex/webhook", new StringContent("{}")));

        responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests).Should()
            .ContainSingle("2 permits against 3 concurrent requests leaves exactly one rejected");
        responses.Where(r => r.StatusCode != HttpStatusCode.TooManyRequests).Should().HaveCount(2);
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
