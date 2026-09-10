using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Api.Infrastructure;

// Maps domain/validation exceptions to RFC 7807 Problem Details responses.
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService)
        => _problemDetailsService = problemDetailsService;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            // Optimistic-concurrency stale write (RowVersion mismatch) → 409 (docs/domain/data-architecture.md §1.5, docs/domain/architecture-detail.md §7.4, ADR-0018).
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The record was modified by another user; reload and try again."),
            // A deliberate rule refusal — illegal transition, duplicate, frozen vote/decision → 409
            // (docs/domain/architecture-detail.md §7.4, NFR-041 / ADR-0009). DEF-156: ONLY the domain-rule type
            // maps here. A bare InvalidOperationException (.Single() on empty, an EF tracking conflict, a
            // framework fault) deliberately has NO arm: it falls through to 500, where a 5xx alert can see it
            // and the client is not told to resolve a conflict that is not theirs.
            DomainRuleException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        context.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title };
        // BL-016: carry a stable ErrorCode per failure so the SPA localizes the message (EN/AR) via its i18n
        // catalog, falling back to the English ErrorMessage for any unmapped code. FluentValidation sets a
        // default ErrorCode per validator (NotEmptyValidator, …); the file rules set explicit codes.
        if (exception is ValidationException ve)
            problem.Extensions["errors"] = ve.Errors.Select(e => new { e.PropertyName, e.ErrorMessage, e.ErrorCode });

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
