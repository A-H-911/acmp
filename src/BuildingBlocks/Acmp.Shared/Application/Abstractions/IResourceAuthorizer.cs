namespace Acmp.Shared.Application.Abstractions;

// Resource-based (ABAC) authorization seam for application handlers. Endpoint-level RBAC
// (RequireAuthorization(policy)) and the MediatR AuthorizationBehavior cover role checks, but
// per-resource decisions — owner-of-this-topic, stream scope — need the loaded aggregate. A handler
// loads the entity, then calls EnsureAsync(entity, policy); the implementation evaluates the
// registered policy (CapabilityRequirement etc.) against the current principal via ASP.NET's
// IAuthorizationService. This is the P4→P5 deferral ("live ABAC HTTP 403") made concrete (docs/domain/permission-role-matrix.md §E).
public interface IResourceAuthorizer
{
    Task<bool> IsAuthorizedAsync(object resource, string policy, CancellationToken ct = default);

    // Throws ForbiddenAccessException (→ HTTP 403) when the principal is not authorized for the policy
    // on the given resource.
    Task EnsureAsync(object resource, string policy, CancellationToken ct = default);
}
