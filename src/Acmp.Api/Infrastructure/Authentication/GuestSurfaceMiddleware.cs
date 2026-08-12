using Acmp.Shared.Authorization;

namespace Acmp.Api.Infrastructure.Authentication;

/// <summary>
/// ADR-0040 decision 3 / DEF-052 — a Guest reaches the guest surface and NOTHING ELSE.
///
/// WHY THIS EXISTS AT ALL: ACMP has no read-side role gate. Every content group is
/// <c>RequireAuthorization()</c> with no policy, and every named policy in the matrix is a WRITE
/// capability, so any authenticated principal can read the entire governance record. That was
/// harmless while the only principals were committee members. FR-159 creates the first EXTERNAL one,
/// which is what turns a dormant gap into unpublished topic material, minutes, decisions and ADRs
/// being readable by someone outside the organisation.
///
/// WHY A MIDDLEWARE AND NOT A POLICY ON EVERY GROUP: an opt-in policy protects only the endpoints
/// somebody remembered to decorate, so every route added later is open by default — the same shape
/// as SC-005's JwtBearer hook, which silently exempted every future scheme. Deny-by-default is
/// closed for code that does not exist yet, which is the only version of this that stays true.
///
/// THE ALLOWLIST IS NOT A NEW JUDGEMENT. The design's own role matrix gives Guest session (full) and
/// agenda (view) and nothing else that ACMP has built, and the reference goes further: it renders a
/// dedicated denial screen for a Guest reaching a topic outside their session ("Your guest session is
/// scoped to the topic you are presenting"). This is that screen's server half.
/// </summary>
public sealed class GuestSurfaceMiddleware
{
    private readonly RequestDelegate _next;

    public GuestSurfaceMiddleware(RequestDelegate next) => _next = next;

    // Exact paths, matched case-insensitively. Prefix entries end without a trailing slash and match
    // the segment boundary, so "/api/meetings" covers "/api/meetings/{id}" but never "/api/meetingsx".
    private static readonly string[] AlwaysAllowed =
    {
        // JIT provisioning of the caller's own profile (ADR-0004). Without this a guest cannot
        // complete a single sign-in, so it is the one entry whose absence breaks the whole surface.
        "/api/members/me",
        // The guest surface itself (DEC-037). Its own handler scopes the answer to the caller.
        "/api/session",
        // The notification bell is rendered in the shell for every signed-in user, and the register
        // is already scoped to the recipient — denying it would put a console error on every page.
        "/api/notifications",
    };

    // Agenda is 'view' for Guest in the design's role matrix — READ only. The mutating meeting routes
    // carry Chairman/Secretary policies of their own; this keeps the outer gate from depending on
    // that, so a route that ever loses its policy is still not writable by a guest.
    private const string ReadOnlyMeetings = "/api/meetings";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsGuestOnly(context))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path;
        var allowed = AlwaysAllowed.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase))
                      || (HttpMethods.IsGet(context.Request.Method)
                          && path.StartsWithSegments(ReadOnlyMeetings, StringComparison.OrdinalIgnoreCase));

        if (allowed)
        {
            await _next(context);
            return;
        }

        // 403 and not 404: the guest IS authenticated and the resource does exist; pretending
        // otherwise would send the SPA down a "not found" path for what is an authorization answer.
        // 401 would be worse still — the SPA renews the token on a 401, and no new token fixes this.
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.Headers["X-Acmp-Auth-Reason"] = "guest_scope";
        await context.Response.WriteAsJsonAsync(new { error = "forbidden", reason = "guest_scope" });
    }

    // Guest AND NOTHING ELSE. A principal who also holds a committee role is an insider who happens
    // to be listed as a guest somewhere, and must not be locked out of their own committee; roles are
    // claims from Keycloak and cannot be self-assigned, so this cannot be used to escape the gate.
    private static bool IsGuestOnly(HttpContext context)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true || !user.IsInRole(AcmpRoles.Guest))
            return false;

        return !AcmpRoles.All.Any(role => role != AcmpRoles.Guest && user.IsInRole(role));
    }
}

public static class GuestSurfaceMiddlewareExtensions
{
    /// <summary>Place after UseAuthentication, beside the principal revalidation (ADR-0040).</summary>
    public static IApplicationBuilder UseAcmpGuestSurface(this IApplicationBuilder app) =>
        app.UseMiddleware<GuestSurfaceMiddleware>();
}
