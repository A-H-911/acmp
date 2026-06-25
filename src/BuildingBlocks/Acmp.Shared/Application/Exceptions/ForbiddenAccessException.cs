namespace Acmp.Shared.Application.Exceptions;

// The principal is authenticated but lacks the role/scope for this action -> HTTP 403.
// Distinct from UnauthorizedAccessException (no/invalid token -> 401). Splitting the two is the
// P4 fix for the carried 401-vs-403 defect (docs/10 §F: "never 401 post-login").
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message) { }
}
