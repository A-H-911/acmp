using Acmp.Modules.Research.Application.Abstractions;
using Acmp.Modules.Research.Application.Contracts;
using Acmp.Modules.Research.Application.Internal;
using Acmp.Modules.Research.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Research.Application.Features.CreateMission;

// FR-111: author a new research mission in Proposed. RBAC = Research.Manage (Chairman/Secretary; Member/
// Reviewer allow-if-owner — see ResearchRoles). Content is entered in one UI language and MIRRORED to both
// LocalizedString columns, so both EN+AR are required. Title + Question are required; the Keystone package
// reference (FR-112 deferred, stored only) and the source topic (FR-115 deferred, field only) are optional.
public sealed record CreateMissionCommand(
    LocalizedString Title,
    LocalizedString Question,
    string? KeystonePackageRef,
    Guid? SourceTopicId) : IRequest<ResearchMissionSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ResearchRoles.Manage;
}

public sealed class CreateMissionValidator : AbstractValidator<CreateMissionCommand>
{
    public CreateMissionValidator()
    {
        RuleFor(x => x.Title).NotNull().WithMessage("A title is required.");
        RuleFor(x => x.Title!.En).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (EN) is required (max 512).");
        RuleFor(x => x.Title!.Ar).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (AR) is required (max 512).");

        RuleFor(x => x.Question).NotNull().WithMessage("A research question is required.");
        RuleFor(x => x.Question!.En).NotEmpty().When(x => x.Question is not null).WithMessage("Question (EN) is required.");
        RuleFor(x => x.Question!.Ar).NotEmpty().When(x => x.Question is not null).WithMessage("Question (AR) is required.");
    }
}

public sealed class CreateMissionHandler : IRequestHandler<CreateMissionCommand, ResearchMissionSummaryDto>
{
    private readonly IResearchDbContext _db;
    private readonly IResearchKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly Acmp.Shared.Contracts.Topics.ITopicReader _topics;
    private readonly Acmp.Shared.Contracts.Traceability.ITraceabilityWriter _trace;

    public CreateMissionHandler(IResearchDbContext db, IResearchKeyGenerator keys, ICurrentUser user,
        IClock clock, IAuditSink audit, Acmp.Shared.Contracts.Topics.ITopicReader topics,
        Acmp.Shared.Contracts.Traceability.ITraceabilityWriter trace)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
        _topics = topics;
        _trace = trace;
    }

    public async Task<ResearchMissionSummaryDto> Handle(CreateMissionCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, name) = CurrentActor.Of(_user);

        var key = await _keys.NextResearchKeyAsync(now.Year, ct);
        var mission = ResearchMission.Propose(key, request.Title, request.Question, sub, name,
            request.KeystonePackageRef, request.SourceTopicId, now);

        _db.Missions.Add(mission);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Research.MissionProposed", nameof(ResearchMission), mission.PublicId.ToString(), ct: ct);

        // FR-115: when the mission was prompted by a source topic, record a Topic → Mission traceability edge so
        // the link shows in the graph + as a navigable xref. Idempotent; a since-deleted/unknown topic → no edge.
        if (request.SourceTopicId is { } sourceTopicId)
        {
            var topic = await _topics.GetSummaryAsync(sourceTopicId, ct);
            if (topic is not null)
                await _trace.RecordEdgeAsync(
                    "Topic", topic.Id, topic.Key, topic.Title,
                    "ResearchMission", mission.PublicId, mission.Key, mission.Title.En,
                    relTypeName: "References", ct);
        }

        return ResearchMapping.ToSummary(mission);
    }
}
