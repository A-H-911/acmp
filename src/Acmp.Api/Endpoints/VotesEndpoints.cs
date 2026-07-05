using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Features.CastBallot;
using Acmp.Modules.Decisions.Application.Features.ChangeBallot;
using Acmp.Modules.Decisions.Application.Features.CloseVote;
using Acmp.Modules.Decisions.Application.Features.ConfigureVote;
using Acmp.Modules.Decisions.Application.Features.GetVoteByKey;
using Acmp.Modules.Decisions.Application.Features.GetVotes;
using Acmp.Modules.Decisions.Application.Features.GetVotesForTopic;
using Acmp.Modules.Decisions.Application.Features.OpenVote;
using Acmp.Modules.Decisions.Application.Features.RecuseVote;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md). The group requires authentication (401 without a token,
// AC-008); each mutating route adds its docs/domain/permission-role-matrix.md policy (403 for the wrong role). Reads are committee-wide;
// configure/open/close are Vote.Manage (Chairman/Secretary); cast/change/recuse are Vote.Cast (Chairman/Member).
public static class VotesEndpoints
{
    public static IEndpointRouteBuilder MapVoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/votes").WithTags("Votes").RequireAuthorization();

        // Reads — any authenticated committee member. With ?topic={guid} → that topic's ballot
        // history; without it → the committee-wide register (optional ?status=, e.g. the chairman
        // queue's Closed-not-Ratified votes awaiting approval, AC-066).
        group.MapGet("/", async (Guid? topic, string? status, ISender sender, CancellationToken ct) =>
            topic is Guid topicId
                ? Results.Ok(await sender.Send(new GetVotesForTopicQuery(topicId), ct))
                : Results.Ok(await sender.Send(new GetVotesQuery(status), ct)));

        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var vote = await sender.Send(new GetVoteByKeyQuery(key), ct);
            return vote is null ? Results.NotFound() : Results.Ok(vote);
        });

        // W11: configure a ballot.
        group.MapPost("/", async (ConfigureVoteBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ConfigureVoteCommand(
                body.TopicId, body.MeetingId, body.Options ?? Array.Empty<string>(), body.AllowAbstain,
                body.MinPresent, body.MinCast,
                body.EligibleVoters ?? Array.Empty<VoteEligibleVoterRequest>()), ct);
            return Results.Created($"/api/votes/{result.Key}", result);
        }).RequireAuthorization(Policies.VoteManage);

        // W11: open voting.
        group.MapPost("/{id:guid}/open", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new OpenVoteCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.VoteManage);

        // W11: cast a first ballot (the voter is the current user).
        group.MapPost("/{id:guid}/cast", async (Guid id, BallotBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CastBallotCommand(id, body.Choice, body.Comment), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.VoteCast);

        // W11: change a ballot while the vote is still open.
        group.MapPost("/{id:guid}/change", async (Guid id, BallotBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ChangeBallotCommand(id, body.Choice, body.Comment), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.VoteCast);

        // W11: recuse from the vote.
        group.MapPost("/{id:guid}/recuse", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RecuseVoteCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.VoteCast);

        // W11: close voting (quorum enforced; tally frozen).
        group.MapPost("/{id:guid}/close", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CloseVoteCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.VoteManage);

        return app;
    }

    public sealed record ConfigureVoteBody(
        Guid TopicId, Guid? MeetingId, IReadOnlyList<string>? Options, bool AllowAbstain,
        int MinPresent, int MinCast, IReadOnlyList<VoteEligibleVoterRequest>? EligibleVoters);

    public sealed record BallotBody(string Choice, LocalizedString? Comment);
}
