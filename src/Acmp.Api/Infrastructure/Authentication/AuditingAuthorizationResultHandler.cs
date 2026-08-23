using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Acmp.Api.Infrastructure.Authentication;

/// <summary>
/// DEF-056 / AC-006 — records the refusals that never reach the application layer.
///
/// AC-006 says a denied mutation returns 403 "with an audit event emitted". For the COMMON case no
/// event existed, and it was a layering fact rather than a broken sink: every mutating endpoint carries
/// a per-endpoint <c>RequireAuthorization(Policies.X)</c>, ASP.NET evaluates that policy and
/// short-circuits with 403 BEFORE the request reaches MediatR, so <c>AuthorizationBehavior</c> — the
/// only thing in the codebase that emits <c>Authorization.Forbidden</c> — never ran. Measured on the
/// AC-005/006/007 live leg: an Auditor's refused POSTs all returned 403 and the audit read-back
/// returned ZERO rows.
///
/// The refusal itself was always correct. What was missing was the RECORD of it — and for a system
/// whose whole purpose is an auditable committee record, an unrecorded refused write is the
/// interesting half.
/// </summary>
/// <remarks>
/// ⚠ ONE SEAM RATHER THAN A POLICY PER ENDPOINT, for the reason DEF-052 chose the same shape: this
/// runs for every endpoint the authorization middleware forbids, INCLUDING ones added later. An
/// opt-in emit would cover only the routes somebody remembered to decorate.
///
/// ⚠ FORBIDDEN ONLY, NEVER CHALLENGED. Challenged means "no usable identity" and ends as a 401; it is
/// not a refused mutation, it is an unauthenticated request, and emitting a Forbidden row for it would
/// put entries in the register describing denials of an action nobody was ever identified to attempt.
/// <c>AuthorizationBehavior</c> already emits <c>Authorization.Unauthenticated</c> for its own case.
///
/// ⚠ NO DOUBLE EMISSION, and it is worth knowing WHY rather than trusting it: the two emitters are on
/// opposite sides of a short-circuit. If the policy forbids, the request never reaches MediatR and
/// only this runs; if the policy passes, this never sees a refusal and only the behaviour can emit.
/// </remarks>
public sealed class AuditingAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            // Resolved per request rather than injected: this handler is a singleton, and both the
            // sink and the principal are request-scoped.
            var audit = context.RequestServices.GetRequiredService<IAuditSink>();
            var user = context.RequestServices.GetRequiredService<ICurrentUser>();

            // WHICH capability was refused, not merely that something was. A row saying only "403 on
            // POST /api/topics" tells a reviewer what happened but not what the person was reaching
            // for, and the policy name is the vocabulary the permission matrix is written in.
            var refused = authorizeResult.AuthorizationFailure?.FailedRequirements
                .Select(r => r is CapabilityRequirement capability ? capability.PolicyName : r.GetType().Name)
                .Distinct()
                .ToArray() ?? Array.Empty<string>();

            // ⚠ CancellationToken.None DELIBERATELY, not context.RequestAborted. A client that hangs up
            // mid-refusal must still leave the record: the audit row is the committee's, not the
            // caller's, and a register that drops entries whenever the refused party disconnects is
            // exactly the one an attacker would prefer.
            await audit.EmitAsync(
                "Authorization.Forbidden",
                user.UserId,
                new
                {
                    method = context.Request.Method,
                    path = context.Request.Path.Value,
                    refused,
                    held = user.Roles,
                },
                CancellationToken.None);
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}

public static class AuditingAuthorizationResultHandlerExtensions
{
    /// <summary>Registers the DEF-056 refusal recorder. Mirrors <c>UseAcmpGuestSurface</c>'s shape.</summary>
    /// <remarks>
    /// The registration lives HERE rather than inline in Program.cs because the interface's namespace
    /// does not resolve from that file's top-level statements, while it resolves perfectly beside the
    /// implementation. An extension method next to the type it registers is the readable version of
    /// that anyway — Program.cs should say WHAT is being turned on, not spell out a framework
    /// interface's full name to do it.
    /// </remarks>
    public static IServiceCollection AddAcmpAuthorizationAuditing(this IServiceCollection services) =>
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditingAuthorizationResultHandler>();
}
