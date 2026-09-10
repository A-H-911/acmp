using Acmp.Api.Infrastructure;
using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Domain;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Api.Tests;

// Unit-covers GlobalExceptionHandler's exception→status mapping. The optimistic-concurrency arm
// (DbUpdateConcurrencyException → 409, ADR-0018 / docs/15 §7.4) is the new branch; the others are
// asserted alongside so the mapping table stays pinned.
public sealed class GlobalExceptionHandlerTests
{
    private static async Task<int> StatusForAsync(Exception ex)
    {
        var handler = new GlobalExceptionHandler(new NoopProblemDetailsService());
        var context = new DefaultHttpContext();
        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);
        handled.Should().BeTrue();
        return context.Response.StatusCode;
    }

    [Fact]
    public async Task DbUpdateConcurrencyException_MapsTo409() =>
        (await StatusForAsync(new DbUpdateConcurrencyException("stale write")))
            .Should().Be(StatusCodes.Status409Conflict);

    [Fact]
    public async Task ValidationException_MapsTo400() =>
        (await StatusForAsync(new ValidationException(new[] { new ValidationFailure("Field", "bad") })))
            .Should().Be(StatusCodes.Status400BadRequest);

    [Fact]
    public async Task ForbiddenAccessException_MapsTo403() =>
        (await StatusForAsync(new ForbiddenAccessException("denied"))).Should().Be(StatusCodes.Status403Forbidden);

    [Fact]
    public async Task UnauthorizedAccessException_MapsTo401() =>
        (await StatusForAsync(new UnauthorizedAccessException())).Should().Be(StatusCodes.Status401Unauthorized);

    [Fact]
    public async Task KeyNotFoundException_MapsTo404() =>
        (await StatusForAsync(new KeyNotFoundException())).Should().Be(StatusCodes.Status404NotFound);

    [Fact]
    public async Task DomainRuleException_MapsTo409() =>
        (await StatusForAsync(new DomainRuleException("This operation is not allowed while the meeting is Scheduled")))
            .Should().Be(StatusCodes.Status409Conflict);

    // DEF-156: a BARE InvalidOperationException is a framework or programming fault, not a conflict the client
    // can resolve. It used to map to 409 "Conflict" — which hid every such fault from a 5xx alert and sent
    // NFR-006's measurement chasing a stale write for two rounds. It now falls through to 500.
    [Fact]
    public async Task BareInvalidOperationException_MapsTo500() =>
        (await StatusForAsync(new InvalidOperationException("Sequence contains no elements")))
            .Should().Be(StatusCodes.Status500InternalServerError);

    [Fact]
    public async Task UnknownException_MapsTo500() =>
        (await StatusForAsync(new Exception("boom"))).Should().Be(StatusCodes.Status500InternalServerError);

    // Minimal IProblemDetailsService stub — the handler only needs a truthy write result; we assert on
    // the status code it set, not on the serialized body.
    private sealed class NoopProblemDetailsService : IProblemDetailsService
    {
        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context) => ValueTask.FromResult(true);

        public ValueTask WriteAsync(ProblemDetailsContext context) => ValueTask.CompletedTask;
    }
}
