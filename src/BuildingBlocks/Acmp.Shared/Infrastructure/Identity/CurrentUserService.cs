using System.Security.Claims;
using Acmp.Shared.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Acmp.Shared.Infrastructure.Identity;

// Maps the current HttpContext principal (a Keycloak OIDC token, ADR-0004) onto ICurrentUser.
// Role-claim -> canonical-role mapping is finalized in P4; here we read role claims as-is.
public sealed class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public string? UserId => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;

    public string? UserName => User?.FindFirst("preferred_username")?.Value ?? User?.Identity?.Name;

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
