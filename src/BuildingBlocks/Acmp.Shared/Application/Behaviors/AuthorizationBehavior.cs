using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using MediatR;

namespace Acmp.Shared.Application.Behaviors;

// Defense-in-depth role check for requests that opt in via IAuthorizedRequest (guardrail 4: no
// command ships without an authorization decision). The PRIMARY gate is ASP.NET policy-based
// authorization at the endpoint (docs/domain/permission-role-matrix.md); this behavior backstops it inside the application layer.
// Denials emit an audit signal (AC-006). 401 vs 403 split: missing identity -> Unauthorized (401),
// authenticated-but-wrong-role -> Forbidden (403).
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUser _currentUser;
    private readonly IAuditSink _audit;

    public AuthorizationBehavior(ICurrentUser currentUser, IAuditSink audit)
    {
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is IAuthorizedRequest authorized)
        {
            var requestName = typeof(TRequest).Name;

            if (!_currentUser.IsAuthenticated)
            {
                await _audit.EmitAsync("Authorization.Unauthenticated", null, new { request = requestName }, ct);
                throw new UnauthorizedAccessException("Authentication required.");
            }

            if (authorized.AllowedRoles.Count != 0 &&
                !authorized.AllowedRoles.Any(_currentUser.IsInRole))
            {
                await _audit.EmitAsync("Authorization.Forbidden", _currentUser.UserId,
                    new { request = requestName, required = authorized.AllowedRoles, held = _currentUser.Roles }, ct);
                throw new ForbiddenAccessException(
                    "Requires one of: " + string.Join(", ", authorized.AllowedRoles) + ".");
            }
        }

        return await next();
    }
}
