using System.Security.Claims;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;

namespace Acmp.Api.Infrastructure.Authentication;

/// <summary>
/// ADR-0039 — the per-request veto over an otherwise-valid token. A signature check answers "was
/// this issued to you"; it cannot answer "are you still allowed", because a token is a snapshot and
/// ACMP-side state moves underneath it. Runs after UseAuthentication, before UseAuthorization, so a
/// refused principal never reaches a policy, a handler, or an endpoint.
///
/// ⚠ THIS IS A MIDDLEWARE AND NOT JwtBearerEvents.OnTokenValidated, WHICH IS WHAT ADR-0039 APPROVED.
/// The deviation is recorded as SC-005 rather than made quietly, and the reason is evidence, not
/// taste: the API integration host authenticates with TestAuthHandler, a separate scheme that issues
/// no JWT at all, so a JwtBearer event CANNOT RUN IN ANY API TEST. AC-090's bar is that the refusal
/// is FORCED through the real pipeline — "asserting the user's subsequent request no longer
/// exercises the removed role" — and an enforcement point only reachable by unit-testing the
/// collaborator is precisely the "a handler was called" evidence this project refuses to accept.
/// A middleware after UseAuthentication is scheme-agnostic, so it covers every authenticated
/// request including the ones tests can drive, and it strictly subsumes the approved seam.
/// </summary>
public sealed class PrincipalRevalidationMiddleware
{
    private readonly RequestDelegate _next;

    public PrincipalRevalidationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IPrincipalRevalidator revalidator, IClock clock)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            await _next(context); // anonymous — RequireAuthorization already decides these (AC-008)
            return;
        }

        var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;

        // `iat` is seconds since the epoch. A token WITHOUT one is treated as issued NOW — the
        // permissive reading, deliberately: every Keycloak token carries `iat`, so its absence means
        // a different issuer, which is a configuration problem this is not the place to diagnose.
        var issuedAt = long.TryParse(user.FindFirst("iat")?.Value, out var epoch)
            ? DateTimeOffset.FromUnixTimeSeconds(epoch)
            : clock.UtcNow;

        var verdict = await revalidator.RevalidateAsync(subject ?? string.Empty, issuedAt, context.RequestAborted);
        if (verdict == PrincipalVerdict.Allowed)
        {
            await _next(context);
            return;
        }

        // 401, not 403, and the distinction is the whole point of returning a REASON: `roles_changed`
        // is fixed by getting a new token, which the SPA already does automatically
        // (automaticSilentRenew), whereas an ended window or a disabled account must NOT be retried.
        // Answering both the same way would put an expired guest into a renewal loop against a
        // session that no longer exists.
        var reason = verdict switch
        {
            PrincipalVerdict.Stale => "roles_changed",
            PrincipalVerdict.Expired => "access_expired",
            _ => "account_disabled",
        };

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate =
            $"Bearer error=\"invalid_token\", error_description=\"{reason}\"";
        // Also as a header the SPA can read without parsing the challenge, and echoed in the body so
        // a human reading a failing request sees why rather than a bare 401.
        context.Response.Headers["X-Acmp-Auth-Reason"] = reason;
        await context.Response.WriteAsJsonAsync(new { error = "invalid_token", reason });
    }
}

public static class PrincipalRevalidationMiddlewareExtensions
{
    /// <summary>Place between UseAuthentication and UseAuthorization (ADR-0039).</summary>
    public static IApplicationBuilder UseAcmpPrincipalRevalidation(this IApplicationBuilder app) =>
        app.UseMiddleware<PrincipalRevalidationMiddleware>();
}
