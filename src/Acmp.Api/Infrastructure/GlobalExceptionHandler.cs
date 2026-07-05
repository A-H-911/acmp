using Acmp.Shared.Application.Exceptions;
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
            InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        context.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title };
        if (exception is ValidationException ve)
            problem.Extensions["errors"] = ve.Errors.Select(e => new { e.PropertyName, e.ErrorMessage });

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
