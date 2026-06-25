namespace Acmp.Shared.Application.Abstractions;

// The authenticated principal for the current request. Identity and roles come from Keycloak OIDC
// token claims (ADR-0004); ACMP never stores or assigns roles itself.
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    string? DisplayName { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsInRole(string role);
}
