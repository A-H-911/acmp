using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acmp.Api.Tests;

// Stands in for Keycloak in integration tests: builds the principal from request headers so each
// test can choose its role(s). No "X-Test-Roles" header => unauthenticated => the endpoint's
// RequireAuthorization returns 401 (AC-008). Claims mirror what AuthenticationExtensions produces
// after mapping, so policies and ICurrentUser see canonical role claims.
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "Test";
    public const string RolesHeader = "X-Test-Roles";
    public const string SubHeader = "X-Test-Sub";

    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out var rolesHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var sub = Request.Headers.TryGetValue(SubHeader, out var s) ? s.ToString() : "test-user";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sub),
            new("name", sub),
            new("email", $"{sub}@acmp.gov"),
        };
        foreach (var role in rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, Scheme, ClaimTypes.Name, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
