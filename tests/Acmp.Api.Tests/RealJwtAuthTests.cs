using System.Net;
using System.Net.Http.Headers;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FluentAssertions;

namespace Acmp.Api.Tests;

// Boots the host with the REAL Keycloak JwtBearer scheme (only the DbContext is swapped to in-memory),
// so the actual authentication path is exercised — not the TestAuthHandler. With no Authority
// configured the scheme is fail-closed: anonymous and bogus-token requests both get 401 (AC-008),
// confirming the production auth wiring boots and challenges correctly.
public class RealJwtAuthTests
{
    private sealed class RealAuthFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = "acmp-realauth-" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<MembershipDbContext>>();
                services.RemoveAll<MembershipDbContext>();
                services.AddDbContext<MembershipDbContext>(o => o.UseInMemoryDatabase(_dbName));
                // Authentication is intentionally NOT swapped — the real JwtBearer scheme stays.
            });
        }
    }

    [Fact]
    public async Task Anonymous_request_is_401_through_the_real_jwt_scheme()
    {
        await using var factory = new RealAuthFactory();
        var response = await factory.CreateClient().GetAsync("/api/members");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Bogus_bearer_token_is_401_not_500()
    {
        await using var factory = new RealAuthFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-jwt");

        var response = await client.GetAsync("/api/members");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_endpoint_stays_anonymous()
    {
        await using var factory = new RealAuthFactory();
        var response = await factory.CreateClient().GetAsync("/healthz");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
