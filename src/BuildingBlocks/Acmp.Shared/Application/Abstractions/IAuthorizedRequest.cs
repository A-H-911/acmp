namespace Acmp.Shared.Application.Abstractions;

// Opt-in marker for MediatR requests that require one of a set of global roles. The
// AuthorizationBehavior enforces it. Full policy + ABAC (stream scope, SoD) lands in P4; this is
// the day-one hook so no command ships without an authorization decision (guardrail 4).
public interface IAuthorizedRequest
{
    IReadOnlyCollection<string> AllowedRoles { get; }
}
