using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acmp.Integration.Tests;

/*
 * The real API host, over the real SQL Server that NfrPerfFixture seeded.
 *
 * ⭐⭐ THE INTERESTING PART IS WHAT THIS FILE DOES **NOT** DO. AcmpWebApplicationFactory swaps all
 * fifteen DbContexts onto the InMemory provider, and its own comment records why that is delicate:
 * EF 10 refuses when two providers register, so removing DbContextOptions&lt;T&gt; without also removing
 * IDbContextOptionsConfiguration&lt;T&gt; leaves BOTH providers wired. Going the other way needs none of
 * that machinery — Program.cs reads ConnectionStrings:Acmp and already calls UseSqlServer, so
 * supplying that ONE setting is the whole change. The provider-collision trap exists only in the
 * direction the existing suite travels.
 *
 * ⚠ WHY THIS LIVES HERE AND NOT IN Acmp.Api.Tests, WHERE THE SIBLING FACTORY LIVES. That project has
 * no Testcontainers dependency at all, and putting a container fixture in it would make a fast,
 * ~390-test, InMemory suite Docker-dependent — a far larger blast radius than adding one Acmp.Api
 * reference here. Acmp.Integration.Tests is already Docker-gated and is already the declared home for
 * real-SQL-Server proofs (ADR-0016 §3).
 */
public sealed class NfrPerfWebFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public NfrPerfWebFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // The single seam. No context is swapped, so every module keeps the production SqlServer
        // wiring and the measurement runs against the schema NfrPerfFixture migrated.
        builder.UseSetting("ConnectionStrings:Acmp", _connectionString);

        builder.ConfigureTestServices(services =>
            services.AddAuthentication(PerfAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, PerfAuthHandler>(PerfAuthHandler.SchemeName, _ => { }));
    }
}

/*
 * A deliberate 30-line twin of Acmp.Api.Tests' TestAuthHandler rather than a reference to it.
 * Referencing one test project from another to borrow a helper couples two suites' lifecycles for no
 * gain; this carries only what a performance measurement needs — an authenticated principal with a
 * role — and omits the iat/ADR-0039 revalidation support that exists there for a different purpose.
 */
internal sealed class PerfAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "PerfTest";
    public const string RolesHeader = "X-Test-Roles";
    public const string SubHeader = "X-Test-Sub";

    public PerfAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out var rolesHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var sub = Request.Headers.TryGetValue(SubHeader, out var s) ? s.ToString() : "perf-user";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sub),
            new("name", sub),
            new("email", $"{sub}@acmp.gov"),
        };
        foreach (var role in rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
