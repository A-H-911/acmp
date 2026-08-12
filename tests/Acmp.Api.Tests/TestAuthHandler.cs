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
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";
    public const string SubHeader = "X-Test-Sub";

    // ADR-0039: the revalidation middleware compares the token's `iat` against the member's
    // RolesChangedAt, so a test must be able to say "this token was issued BEFORE the change" —
    // otherwise the refusal can only be unit-tested on the collaborator, which is exactly the
    // evidence AC-090 rejects. Unix seconds; absent => issued now.
    public const string IssuedAtHeader = "X-Test-Iat";

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
        if (Request.Headers.TryGetValue(IssuedAtHeader, out var iat))
            claims.Add(new Claim("iat", iat.ToString()));
        foreach (var role in rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
