using Acmp.Shared.Application.Abstractions;
using MediatR;

namespace Acmp.Shared.Application.Behaviors;

// Enforces role authorization for requests that opt in via IAuthorizedRequest. Guardrail 4: no
// command ships without an authorization decision. Full ABAC (stream scope, SoD) is layered on in P4.
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUser _currentUser;

    public AuthorizationBehavior(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is IAuthorizedRequest authorized)
        {
            if (!_currentUser.IsAuthenticated)
                throw new UnauthorizedAccessException("Authentication required.");

            if (authorized.AllowedRoles.Count != 0 &&
                !authorized.AllowedRoles.Any(_currentUser.IsInRole))
            {
                throw new UnauthorizedAccessException(
                    "Requires one of: " + string.Join(", ", authorized.AllowedRoles) + ".");
            }
        }

        return next();
    }
}
