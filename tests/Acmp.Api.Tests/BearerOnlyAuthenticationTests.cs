using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acmp.Api.Tests;

/// <summary>
/// NFR-022 / AC-120 — CSRF protection for all state-changing requests.
/// </summary>
/// <remarks>
/// ⚠ THE REQUIREMENT NAMES TWO MECHANISMS AND ACMP USES NEITHER, BECAUSE IT NEEDS NEITHER. NFR-022
/// asks for "the ASP.NET Core anti-forgery token or SameSite cookie policy". Both defend the same
/// thing: an AMBIENT credential the browser attaches automatically, which is what lets a third-party
/// page forge an authenticated request. ACMP authenticates with a bearer token that JavaScript must
/// attach deliberately on every call, so there is no ambient credential to ride and classic CSRF does
/// not apply. HardeningExtensions says as much in passing ("bearer auth — no antiforgery/session
/// payloads"); this suite turns that remark into a control.
/// <para>
/// ⚠ WHICH MEANS THE REAL RISK IS DRIFT, NOT TODAY'S POSTURE. The argument above collapses the moment
/// a cookie-based scheme is added — someone wiring cookie auth for convenience would silently
/// reintroduce the exact attack NFR-022 exists to prevent, and no other test would notice. So the
/// assertion is not "we are safe"; it is "the premise that makes us safe still holds".
/// </para>
/// <para>
/// ⚠⚠ AND IT MUST NOT RUN ON <see cref="AcmpWebApplicationFactory"/>. That factory calls
/// <c>AddAuthentication(TestAuthHandler.SchemeName)</c>, REPLACING the real schemes — so a cookie
/// assertion made against it would inspect the harness and pass no matter what production registers.
/// The first draft of this suite did exactly that and was caught only because the default-scheme case
/// failed loudly with "Test" instead of "Bearer". The factory below deliberately swaps only the two
/// DbContexts and leaves authentication alone, mirroring RealJwtAuthTests.
/// </para>
/// </remarks>
[Trait("Category", "Security")]
public class BearerOnlyAuthenticationTests
{
    // Swaps persistence ONLY. Authentication is intentionally left as production wires it.
    private sealed class RealAuthFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = "acmp-bearer-only-" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<MembershipDbContext>>();
                services.RemoveAll<MembershipDbContext>();
                services.AddDbContext<MembershipDbContext>(o => o.UseInMemoryDatabase(_dbName));
                services.RemoveAll<DbContextOptions<AuditDbContext>>();
                services.RemoveAll<AuditDbContext>();
                services.AddDbContext<AuditDbContext>(o => o.UseInMemoryDatabase(_dbName + "-audit"));
                // Authentication is NOT swapped — that is the entire point of this suite.
            });
        }
    }

    private static async Task<IReadOnlyList<AuthenticationScheme>> SchemesAsync(RealAuthFactory factory)
    {
        var provider = factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        return (await provider.GetAllSchemesAsync()).ToList();
    }

    [Fact]
    public async Task The_enumeration_sees_the_production_authentication_setup()
    {
        await using var factory = new RealAuthFactory();
        var schemes = await SchemesAsync(factory);

        // THE CONTROL ON THE CONTROL. An empty scheme list would make the cookie assertion below pass
        // while proving nothing, and a list containing the TEST scheme would mean the harness leaked in
        // and the suite is inspecting itself.
        schemes.Should().NotBeEmpty("the cookie assertion is vacuous unless schemes were actually enumerated");
        schemes.Select(s => s.Name).Should().NotContain(TestAuthHandler.SchemeName,
            "this suite must inspect PRODUCTION authentication; seeing the test scheme means the factory swapped it");
    }

    [Fact]
    public async Task No_cookie_based_authentication_scheme_is_registered()
    {
        await using var factory = new RealAuthFactory();
        var cookieSchemes = (await SchemesAsync(factory))
            .Where(s => typeof(CookieAuthenticationHandler).IsAssignableFrom(s.HandlerType))
            .Select(s => s.Name)
            .ToList();

        // Typed on the HANDLER, not on the scheme NAME. A cookie scheme registered under a custom name
        // ("Acmp", "Legacy", anything) is still cookie auth, and a name-based check would miss it —
        // which is precisely how this guard would rot into decoration.
        cookieSchemes.Should().BeEmpty(
            "ACMP's CSRF posture (NFR-022) rests on there being no ambient browser credential; a cookie "
            + "scheme reintroduces one and the anti-forgery token NFR-022 names would then be required: {0}",
            string.Join(", ", cookieSchemes));
    }

    [Fact]
    public async Task The_default_scheme_is_JWT_bearer()
    {
        await using var factory = new RealAuthFactory();
        var provider = factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        // The positive half. "No cookie scheme" is an absence; on its own it is also satisfied by an app
        // with no authentication at all. This pins what IS configured, so the two together say
        // "bearer, and only bearer" rather than merely "not cookies".
        var defaultScheme = await provider.GetDefaultAuthenticateSchemeAsync();
        defaultScheme.Should().NotBeNull("an app with no default scheme is not 'bearer-only', it is unauthenticated");
        defaultScheme!.Name.Should().Be(JwtBearerDefaults.AuthenticationScheme);
    }
}
