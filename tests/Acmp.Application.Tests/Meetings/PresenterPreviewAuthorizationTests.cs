using Acmp.Modules.Meetings.Application.Features.GetMySession;
using Acmp.Modules.Meetings.Application.Features.GetPresenterSessionPreview;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Behaviors;
using Acmp.Shared.Application.Exceptions;
using FluentAssertions;
using NSubstitute;

namespace Acmp.Application.Tests.Meetings;

// FR-165 / DEC-086 d1 — LAYER 3 OF THE PREVIEW'S THREE REFUSALS, TESTED WHERE LAYER 2 CANNOT MASK IT.
//
// ⚠⚠ THIS FILE EXISTS BECAUSE THE OBVIOUS TEST IS BLIND, AND THE BLINDNESS WAS FOUND BY WRITING DOWN A
// CLAIM AND THEN CHECKING IT. The query's own comment first said that adding Guest to AllowedRoles was
// "the single mutation" that would let a guest read somebody else's slot. That is FALSE: at the HTTP
// boundary GuestSurfaceMiddleware refuses a guest-only principal AT THE PATH, before any handler runs,
// so the mutation could be applied and every API test would stay green. The guest population is exactly
// the one layer 2 always intercepts, which means layer 3's guest exclusion is the one part of this
// design that HTTP tests structurally cannot see.
//
// So it is asserted HERE, against AuthorizationBehavior directly, with no pipeline and no middleware —
// the only place the question "does this query admit a Guest" can actually be put. SessionPreviewApiTests
// covers the other roles at the HTTP boundary, where layer 2 lets them through and layer 3 is genuinely
// the thing that refuses; the two files divide the population between them and neither is redundant.
//
// This is ADR-0040 decision 3's method reused: encode the expectation INDEPENDENTLY of the implementation
// rather than sharing a constant with it.
public class PresenterPreviewAuthorizationTests
{
    private static ICurrentUser Principal(string role)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns($"kc-{role.ToLowerInvariant()}");
        user.IsInRole(role).Returns(true);
        return user;
    }

    private static AuthorizationBehavior<GetPresenterSessionPreviewQuery, PresenterSessionDto?> Behavior(ICurrentUser user) =>
        new(user, Substitute.For<IAuditSink>());

    private static Task<PresenterSessionDto?> Next(CancellationToken ct) => Task.FromResult<PresenterSessionDto?>(null);

    // THE ONE THAT MATTERS. A guest is bounded by a TIME WINDOW, not by scope
    // (permission-role-matrix E.3), so a targeting parameter must never become the way a guest reads
    // somebody else's slot — and the path gate happening to stop them today is not a reason for this
    // query to admit them.
    [Fact]
    public async Task A_guest_is_forbidden_by_the_query_itself_where_the_path_gate_cannot_mask_it()
    {
        var act = () => Behavior(Principal("Guest"))
            .Handle(new GetPresenterSessionPreviewQuery(Guid.NewGuid(), Guid.NewGuid()), Next, default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    // The same question for every other role that the path gate DOES let through, asserted here as well
    // as at the HTTP boundary because a unit-level refusal is what survives a future change to the gate.
    [Theory]
    [InlineData("Member")]
    [InlineData("Reviewer")]
    [InlineData("Auditor")]
    [InlineData("Administrator")]
    [InlineData("Submitter")]
    public async Task Every_role_outside_Chairman_and_Secretary_is_forbidden(string role)
    {
        var act = () => Behavior(Principal(role))
            .Handle(new GetPresenterSessionPreviewQuery(Guid.NewGuid(), Guid.NewGuid()), Next, default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    // THE POSITIVE CONTROL, and it is not ceremony: without it every refusal above would pass just as
    // happily against a query that forbids EVERYONE, which is a broken feature that looks like a
    // well-secured one. A refusal test with no admission test cannot tell the two apart.
    [Theory]
    [InlineData("Chairman")]
    [InlineData("Secretary")]
    public async Task The_two_roles_that_run_the_meeting_are_admitted(string role)
    {
        var act = () => Behavior(Principal(role))
            .Handle(new GetPresenterSessionPreviewQuery(Guid.NewGuid(), Guid.NewGuid()), Next, default);

        await act.Should().NotThrowAsync();
    }

    // ⚠ AND THE GUEST SURFACE ITSELF MUST NOT HAVE MOVED. GetMySessionQuery admits Guest and must keep
    // doing so — the preview was added by ISOLATING it, not by narrowing the page a presenter actually
    // uses, and a change that quietly locked guests out of their own slot would otherwise look like this
    // work succeeding.
    [Fact]
    public async Task The_caller_scoped_session_still_admits_a_guest()
    {
        var behavior = new AuthorizationBehavior<GetMySessionQuery, PresenterSessionDto?>(
            Principal("Guest"), Substitute.For<IAuditSink>());

        var act = () => behavior.Handle(new GetMySessionQuery(), Next, default);

        await act.Should().NotThrowAsync();
    }
}
