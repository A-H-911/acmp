using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Features.GetDecisionByKey;
using Acmp.Modules.Decisions.Application.Features.GetDecisionsByTopic;
using Acmp.Modules.Decisions.Application.Features.IssueDecision;
using Acmp.Modules.Decisions.Application.Features.RecordDecision;
using Acmp.Modules.Decisions.Application.Features.SupersedeDecision;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using MediatR;

namespace Acmp.Api.Endpoints;

// Thin endpoint layer over MediatR (CLAUDE.md). The group requires authentication (401 without a token,
// AC-008); each mutating route adds its docs/10 policy (403 for the wrong role). The MediatR
// AuthorizationBehavior re-checks roles at the application boundary (defence in depth, guardrail 4).
// Reads are committee-wide; record is Secretary/Chairman; issue/supersede are Chairman-only.
public static class DecisionsEndpoints
{
    public static IEndpointRouteBuilder MapDecisionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/decisions").WithTags("Decisions").RequireAuthorization();

        // Reads — any authenticated committee member. List filters by Topic.PublicId (a Guid).
        group.MapGet("/", async (Guid topic, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetDecisionsByTopicQuery(topic), ct)));

        group.MapGet("/{key}", async (string key, ISender sender, CancellationToken ct) =>
        {
            var decision = await sender.Send(new GetDecisionByKeyQuery(key), ct);
            return decision is null ? Results.NotFound() : Results.Ok(decision);
        });

        // W12: record (draft) a decision.
        group.MapPost("/", async (RecordDecisionBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RecordDecisionCommand(
                body.TopicId, body.MeetingId, body.Outcome, body.Title, body.Rationale, body.Alternatives, body.VoteId,
                body.Conditions ?? Array.Empty<DecisionConditionRequest>()), ct);
            return Results.Created($"/api/decisions/{result.Key}", result);
        }).RequireAuthorization(Policies.DecisionRecord);

        // W12: issue a drafted decision (Chairman).
        group.MapPost("/{id:guid}/issue", async (Guid id, IssueDecisionBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new IssueDecisionCommand(id, body.ChairOverride, body.OverrideJustification), ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.DecisionChairApprove);

        // W21: supersede an issued decision with a corrected one (returns the new decision).
        group.MapPost("/{id:guid}/supersede", async (Guid id, SupersedeDecisionBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SupersedeDecisionCommand(
                id, body.Outcome, body.Title, body.Rationale, body.Alternatives,
                body.Conditions ?? Array.Empty<DecisionConditionRequest>(), body.Reason), ct);
            return Results.Created($"/api/decisions/{result.Key}", result);
        }).RequireAuthorization(Policies.DecisionChairApprove);

        return app;
    }

    public sealed record RecordDecisionBody(
        Guid TopicId, Guid? MeetingId, DecisionOutcome Outcome, LocalizedString Title, LocalizedString Rationale,
        LocalizedString? Alternatives, Guid? VoteId, IReadOnlyList<DecisionConditionRequest>? Conditions);

    public sealed record IssueDecisionBody(bool ChairOverride, LocalizedString? OverrideJustification);

    public sealed record SupersedeDecisionBody(
        DecisionOutcome Outcome, LocalizedString Title, LocalizedString Rationale, LocalizedString? Alternatives,
        IReadOnlyList<DecisionConditionRequest>? Conditions, LocalizedString Reason);
}
