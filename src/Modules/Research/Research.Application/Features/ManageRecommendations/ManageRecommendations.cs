using Acmp.Modules.Research.Application.Abstractions;
using Acmp.Modules.Research.Application.Internal;
using Acmp.Modules.Research.Domain;
using Acmp.Modules.Research.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Research.Application.Features.ManageRecommendations;

// FR-113: capture / revise recommendations and set their disposition on an Active mission. RBAC =
// Research.Manage. Each op loads the mission, drives the aggregate (which 409s if the mission is not Active,
// the recommendation is unknown, or the disposition is illegal), saves, and audits. LinkedTopicId is stored
// only — the graph edge is P15c (ADR-0001); no client key/title snapshot is accepted.

public sealed record AddRecommendationCommand(
    Guid MissionId, LocalizedString Statement, LocalizedString? Rationale, RecommendationPriority Priority, Guid? LinkedTopicId)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ResearchRoles.Manage;
}

public sealed record UpdateRecommendationCommand(
    Guid MissionId, Guid RecommendationId, LocalizedString Statement, LocalizedString? Rationale,
    RecommendationPriority Priority, Guid? LinkedTopicId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ResearchRoles.Manage;
}

public sealed record SetRecommendationStatusCommand(
    Guid MissionId, Guid RecommendationId, RecommendationStatus Status) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ResearchRoles.Manage;
}

// P15c / W16: record that a recommendation has been converted into an execution Topic (TOP-). Called after the
// Topics-side convert succeeds (POST /api/topics/from-research); the authoritative one-per-recommendation guard
// is the Topic.SourceRecommendationId unique index, so this is a best-effort display disposition.
public sealed record MarkRecommendationConvertedCommand(
    Guid MissionId, Guid RecommendationId, Guid TopicId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ResearchRoles.Manage;
}

public sealed class AddRecommendationValidator : AbstractValidator<AddRecommendationCommand>
{
    public AddRecommendationValidator()
    {
        RuleFor(x => x.Statement).NotNull().WithMessage("A statement is required.");
        RuleFor(x => x.Statement!.En).NotEmpty().When(x => x.Statement is not null).WithMessage("Statement (EN) is required.");
        RuleFor(x => x.Statement!.Ar).NotEmpty().When(x => x.Statement is not null).WithMessage("Statement (AR) is required.");
        RuleFor(x => x.Rationale!.En).NotEmpty().When(x => x.Rationale is not null).WithMessage("Rationale (EN) is required when a rationale is given.");
        RuleFor(x => x.Rationale!.Ar).NotEmpty().When(x => x.Rationale is not null).WithMessage("Rationale (AR) is required when a rationale is given.");
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class UpdateRecommendationValidator : AbstractValidator<UpdateRecommendationCommand>
{
    public UpdateRecommendationValidator()
    {
        RuleFor(x => x.MissionId).NotEmpty();
        RuleFor(x => x.RecommendationId).NotEmpty();
        RuleFor(x => x.Statement).NotNull().WithMessage("A statement is required.");
        RuleFor(x => x.Statement!.En).NotEmpty().When(x => x.Statement is not null).WithMessage("Statement (EN) is required.");
        RuleFor(x => x.Statement!.Ar).NotEmpty().When(x => x.Statement is not null).WithMessage("Statement (AR) is required.");
        RuleFor(x => x.Rationale!.En).NotEmpty().When(x => x.Rationale is not null).WithMessage("Rationale (EN) is required when a rationale is given.");
        RuleFor(x => x.Rationale!.Ar).NotEmpty().When(x => x.Rationale is not null).WithMessage("Rationale (AR) is required when a rationale is given.");
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class SetRecommendationStatusValidator : AbstractValidator<SetRecommendationStatusCommand>
{
    public SetRecommendationStatusValidator()
    {
        RuleFor(x => x.MissionId).NotEmpty();
        RuleFor(x => x.RecommendationId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class AddRecommendationHandler : IRequestHandler<AddRecommendationCommand>
{
    private readonly IResearchDbContext _db;
    private readonly IAuditSink _audit;

    public AddRecommendationHandler(IResearchDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task Handle(AddRecommendationCommand request, CancellationToken ct)
    {
        var mission = await Load(_db, request.MissionId, ct);
        mission.AddRecommendation(request.Statement, request.Rationale, request.Priority, request.LinkedTopicId);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Research.RecommendationAdded", nameof(ResearchMission), mission.PublicId.ToString(), ct: ct);
    }

    internal static async Task<ResearchMission> Load(IResearchDbContext db, Guid id, CancellationToken ct) =>
        await db.Missions.Include(m => m.Recommendations).FirstOrDefaultAsync(m => m.PublicId == id, ct)
        ?? throw new KeyNotFoundException("Research mission not found.");
}

public sealed class UpdateRecommendationHandler : IRequestHandler<UpdateRecommendationCommand>
{
    private readonly IResearchDbContext _db;
    private readonly IAuditSink _audit;

    public UpdateRecommendationHandler(IResearchDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task Handle(UpdateRecommendationCommand request, CancellationToken ct)
    {
        var mission = await AddRecommendationHandler.Load(_db, request.MissionId, ct);
        mission.UpdateRecommendation(request.RecommendationId, request.Statement, request.Rationale, request.Priority, request.LinkedTopicId);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Research.RecommendationUpdated", nameof(ResearchMission), mission.PublicId.ToString(), ct: ct);
    }
}

public sealed class SetRecommendationStatusHandler : IRequestHandler<SetRecommendationStatusCommand>
{
    private readonly IResearchDbContext _db;
    private readonly IAuditSink _audit;

    public SetRecommendationStatusHandler(IResearchDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task Handle(SetRecommendationStatusCommand request, CancellationToken ct)
    {
        var mission = await AddRecommendationHandler.Load(_db, request.MissionId, ct);
        mission.SetRecommendationStatus(request.RecommendationId, request.Status);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Research.RecommendationStatusChanged", nameof(ResearchMission), mission.PublicId.ToString(), ct: ct);
    }
}

public sealed class MarkRecommendationConvertedHandler : IRequestHandler<MarkRecommendationConvertedCommand>
{
    private readonly IResearchDbContext _db;
    private readonly IAuditSink _audit;

    public MarkRecommendationConvertedHandler(IResearchDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task Handle(MarkRecommendationConvertedCommand request, CancellationToken ct)
    {
        var mission = await AddRecommendationHandler.Load(_db, request.MissionId, ct);
        mission.ConvertRecommendation(request.RecommendationId, request.TopicId);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Research.RecommendationConverted", nameof(ResearchMission), mission.PublicId.ToString(), ct: ct);
    }
}
