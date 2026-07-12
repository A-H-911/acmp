using Acmp.Modules.Research.Application.Abstractions;
using Acmp.Modules.Research.Application.Contracts;
using Acmp.Modules.Research.Application.Internal;
using Acmp.Modules.Research.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Research.Application.Features.UpdateMissionDraft;

// FR-111: revise a mission's own fields while it is still Proposed (once Active the question is locked, once
// terminal it is immutable). RBAC = Research.Manage. Both bilingual fields are required (mirrored EN+AR).
public sealed record UpdateMissionDraftCommand(
    Guid MissionId,
    LocalizedString Title,
    LocalizedString Question,
    string? KeystonePackageRef,
    Guid? SourceTopicId) : IRequest<ResearchMissionSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ResearchRoles.Manage;
}

public sealed class UpdateMissionDraftValidator : AbstractValidator<UpdateMissionDraftCommand>
{
    public UpdateMissionDraftValidator()
    {
        RuleFor(x => x.MissionId).NotEmpty();
        RuleFor(x => x.Title).NotNull().WithMessage("A title is required.");
        RuleFor(x => x.Title!.En).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (EN) is required (max 512).");
        RuleFor(x => x.Title!.Ar).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (AR) is required (max 512).");
        RuleFor(x => x.Question).NotNull().WithMessage("A research question is required.");
        RuleFor(x => x.Question!.En).NotEmpty().When(x => x.Question is not null).WithMessage("Question (EN) is required.");
        RuleFor(x => x.Question!.Ar).NotEmpty().When(x => x.Question is not null).WithMessage("Question (AR) is required.");
    }
}

public sealed class UpdateMissionDraftHandler : IRequestHandler<UpdateMissionDraftCommand, ResearchMissionSummaryDto>
{
    private readonly IResearchDbContext _db;
    private readonly IAuditSink _audit;

    public UpdateMissionDraftHandler(IResearchDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ResearchMissionSummaryDto> Handle(UpdateMissionDraftCommand request, CancellationToken ct)
    {
        var mission = await _db.Missions.FirstOrDefaultAsync(m => m.PublicId == request.MissionId, ct)
            ?? throw new KeyNotFoundException("Research mission not found.");

        mission.UpdateDraft(request.Title, request.Question, request.KeystonePackageRef, request.SourceTopicId);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Research.MissionDraftUpdated", nameof(ResearchMission), mission.PublicId.ToString(), ct: ct);

        return ResearchMapping.ToSummary(mission);
    }
}
