using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Features.SubmitTopic;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Research;
using Acmp.Shared.Contracts.Traceability;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.ConvertResearchToTopic;

// W16 / FR-113: convert a research mission — or one of its recommendations — into a new execution Topic, linked
// back to the source by an Informs traceability edge (Research → Topic). Target-owns (P11e / ADR-0021): the
// Topic is created natively here; the source is read through IResearchReader; the reverse edge is written via
// ITraceabilityWriter. A recommendation-seeded convert stamps Topic.SourceRecommendationId, whose filtered
// unique index enforces one-topic-per-recommendation (a re-convert → 409, self-healing the edge first). RBAC =
// Chairman/Secretary (the effective Research.Manage writers). Topic fields come pre-filled from the FE.
public sealed record ConvertResearchToTopicCommand(
    Guid MissionId, Guid? RecommendationId,
    string Title, string Description, string Justification,
    TopicType Type, TopicUrgency Urgency,
    IReadOnlyList<string> Streams, IReadOnlyList<string> Systems, IReadOnlyList<string> Tags)
    : IRequest<SubmitTopicResult>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class ConvertResearchToTopicValidator : AbstractValidator<ConvertResearchToTopicCommand>
{
    public ConvertResearchToTopicValidator()
    {
        RuleFor(x => x.MissionId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.Justification).NotEmpty();
        RuleFor(x => x.Streams).NotEmpty().WithMessage("At least one affected stream is required.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Urgency).IsInEnum();
    }
}

public sealed class ConvertResearchToTopicHandler : IRequestHandler<ConvertResearchToTopicCommand, SubmitTopicResult>
{
    private const string MissionCompleted = "Completed";
    private const string RecommendationAccepted = "Accepted";

    private readonly ITopicsDbContext _db;
    private readonly ITopicKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly IResearchReader _research;
    private readonly ITraceabilityWriter _trace;

    public ConvertResearchToTopicHandler(ITopicsDbContext db, ITopicKeyGenerator keys, ICurrentUser user,
        IClock clock, IAuditSink audit, IResearchReader research, ITraceabilityWriter trace)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
        _research = research;
        _trace = trace;
    }

    public async Task<SubmitTopicResult> Handle(ConvertResearchToTopicCommand request, CancellationToken ct)
    {
        var mission = await _research.GetMissionForConvertAsync(request.MissionId, ct)
            ?? throw new KeyNotFoundException("Research mission not found.");

        // The edge source is either the whole mission or a specific recommendation seeded from it.
        string sourceType, sourceKey, sourceTitle;
        Guid sourceId;

        if (request.RecommendationId is { } recId)
        {
            var rec = await _research.GetRecommendationForConvertAsync(request.MissionId, recId, ct)
                ?? throw new KeyNotFoundException("Recommendation not found on this mission.");
            if (!string.Equals(rec.Status, RecommendationAccepted, StringComparison.Ordinal))
                throw new InvalidOperationException("Only an accepted recommendation can be converted to a topic.");

            // One topic per recommendation — a second convert is blocked (409) naming the existing topic. The
            // retry HEALS a missing reverse edge first (idempotent). A same-instant concurrent double-convert that
            // races past this app guard is caught by the filtered unique index on Topic.SourceRecommendationId.
            var existing = await _db.Topics.AsNoTracking()
                .FirstOrDefaultAsync(t => t.SourceRecommendationId == recId, ct);
            if (existing is not null)
            {
                await _trace.RecordEdgeAsync(
                    "Recommendation", recId, rec.Key, rec.StatementEn,
                    "Topic", existing.PublicId, existing.Key, existing.Title, relTypeName: "Informs", ct);
                throw new InvalidOperationException($"This recommendation has already been converted to topic {existing.Key}.");
            }

            sourceType = "Recommendation";
            sourceId = recId;
            sourceKey = rec.Key;
            sourceTitle = rec.StatementEn;
        }
        else
        {
            if (!string.Equals(mission.Status, MissionCompleted, StringComparison.Ordinal))
                throw new InvalidOperationException("Only a completed mission can be converted to a topic.");
            sourceType = "ResearchMission";
            sourceId = mission.Id;
            sourceKey = mission.Key;
            sourceTitle = mission.TitleEn;
        }

        var now = _clock.UtcNow;
        var (sub, name) = CurrentActor.Of(_user);
        var key = await _keys.NextAsync(now.Year, ct);

        var topic = Topic.Draft(key, request.Title, request.Description, request.Justification,
            request.Type, request.Urgency, TopicSource.CommitteeMember, sub, name,
            request.Streams, request.Systems, request.Tags,
            sourceRecommendationId: request.RecommendationId);
        topic.Submit(now);

        _db.Topics.Add(topic);
        await _db.SaveChangesAsync(ct);

        // Reverse Informs edge (source Research artifact → Topic) so both traceability panels cross-link. Title
        // snapshots are single strings; the SPA localizes labels, not snapshots.
        await _trace.RecordEdgeAsync(
            sourceType, sourceId, sourceKey, sourceTitle,
            "Topic", topic.PublicId, topic.Key, topic.Title, relTypeName: "Informs", ct);

        await _audit.EmitEnrichedAsync("Topics.TopicConvertedFromResearch", nameof(Topic), topic.PublicId.ToString(), ct: ct);
        return new SubmitTopicResult(topic.PublicId, topic.Key);
    }
}
