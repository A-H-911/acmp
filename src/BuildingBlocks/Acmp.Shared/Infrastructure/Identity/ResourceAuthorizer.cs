using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Acmp.Shared.Infrastructure.Identity;

// Evaluates a registered authorization policy against the loaded resource for the current request
// principal (the mapped Keycloak token). Reuses ASP.NET's IAuthorizationService so the same
// CapabilityRequirement / StreamScopeRequirement handlers used by endpoints also gate handler-level
// resource decisions — no parallel authorization logic.
public sealed class ResourceAuthorizer : IResourceAuthorizer
{
    private readonly IAuthorizationService _authorization;
    private readonly IHttpContextAccessor _http;

    public ResourceAuthorizer(IAuthorizationService authorization, IHttpContextAccessor http)
    {
        _authorization = authorization;
        _http = http;
    }

    public async Task<bool> IsAuthorizedAsync(object resource, string policy, CancellationToken ct = default)
    {
        var user = _http.HttpContext?.User
            ?? throw new UnauthorizedAccessException("No authenticated principal for the current request.");
        var result = await _authorization.AuthorizeAsync(user, resource, policy);
        return result.Succeeded;
    }

    public async Task EnsureAsync(object resource, string policy, CancellationToken ct = default)
    {
        if (!await IsAuthorizedAsync(resource, policy, ct))
            throw new ForbiddenAccessException($"Not authorized for {policy} on this resource.");
    }
}
