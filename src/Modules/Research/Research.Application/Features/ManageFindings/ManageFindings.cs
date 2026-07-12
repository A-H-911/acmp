using Acmp.Modules.Research.Application.Abstractions;
using Acmp.Modules.Research.Application.Internal;
using Acmp.Modules.Research.Domain;
using Acmp.Modules.Research.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Research.Application.Features.ManageFindings;

// FR-113: capture / revise / verify findings on an Active mission. RBAC = Research.Manage. Each op loads the
// mission, drives the aggregate (which 409s if the mission is not Active or the finding is unknown), saves,
// and audits. The mission's PublicId is the audit subject (findings are owned, not aggregate roots).

public sealed record AddFindingCommand(
    Guid MissionId, LocalizedString Summary, LocalizedString? Detail, Confidence Confidence)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ResearchRoles.Manage;
}

public sealed record UpdateFindingCommand(
    Guid MissionId, Guid FindingId, LocalizedString Summary, LocalizedString? Detail, Confidence Confidence)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ResearchRoles.Manage;
}

public sealed record VerifyFindingCommand(Guid MissionId, Guid FindingId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ResearchRoles.Manage;
}

public sealed class AddFindingValidator : AbstractValidator<AddFindingCommand>
{
    public AddFindingValidator()
    {
        RuleFor(x => x.Summary).NotNull().WithMessage("A summary is required.");
        RuleFor(x => x.Summary!.En).NotEmpty().When(x => x.Summary is not null).WithMessage("Summary (EN) is required.");
        RuleFor(x => x.Summary!.Ar).NotEmpty().When(x => x.Summary is not null).WithMessage("Summary (AR) is required.");
        RuleFor(x => x.Detail!.En).NotEmpty().When(x => x.Detail is not null).WithMessage("Detail (EN) is required when a detail is given.");
        RuleFor(x => x.Detail!.Ar).NotEmpty().When(x => x.Detail is not null).WithMessage("Detail (AR) is required when a detail is given.");
        RuleFor(x => x.Confidence).IsInEnum();
    }
}

public sealed class UpdateFindingValidator : AbstractValidator<UpdateFindingCommand>
{
    public UpdateFindingValidator()
    {
        RuleFor(x => x.MissionId).NotEmpty();
        RuleFor(x => x.FindingId).NotEmpty();
        RuleFor(x => x.Summary).NotNull().WithMessage("A summary is required.");
        RuleFor(x => x.Summary!.En).NotEmpty().When(x => x.Summary is not null).WithMessage("Summary (EN) is required.");
        RuleFor(x => x.Summary!.Ar).NotEmpty().When(x => x.Summary is not null).WithMessage("Summary (AR) is required.");
        RuleFor(x => x.Detail!.En).NotEmpty().When(x => x.Detail is not null).WithMessage("Detail (EN) is required when a detail is given.");
        RuleFor(x => x.Detail!.Ar).NotEmpty().When(x => x.Detail is not null).WithMessage("Detail (AR) is required when a detail is given.");
        RuleFor(x => x.Confidence).IsInEnum();
    }
}

public sealed class AddFindingHandler : IRequestHandler<AddFindingCommand>
{
    private readonly IResearchDbContext _db;
    private readonly IAuditSink _audit;

    public AddFindingHandler(IResearchDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task Handle(AddFindingCommand request, CancellationToken ct)
    {
        var mission = await Load(_db, request.MissionId, ct);
        mission.AddFinding(request.Summary, request.Detail, request.Confidence);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Research.FindingAdded", nameof(ResearchMission), mission.PublicId.ToString(), ct: ct);
    }

    internal static async Task<ResearchMission> Load(IResearchDbContext db, Guid id, CancellationToken ct) =>
        await db.Missions.Include(m => m.Findings).FirstOrDefaultAsync(m => m.PublicId == id, ct)
        ?? throw new KeyNotFoundException("Research mission not found.");
}

public sealed class UpdateFindingHandler : IRequestHandler<UpdateFindingCommand>
{
    private readonly IResearchDbContext _db;
    private readonly IAuditSink _audit;

    public UpdateFindingHandler(IResearchDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task Handle(UpdateFindingCommand request, CancellationToken ct)
    {
        var mission = await AddFindingHandler.Load(_db, request.MissionId, ct);
        mission.UpdateFinding(request.FindingId, request.Summary, request.Detail, request.Confidence);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Research.FindingUpdated", nameof(ResearchMission), mission.PublicId.ToString(), ct: ct);
    }
}

public sealed class VerifyFindingHandler : IRequestHandler<VerifyFindingCommand>
{
    private readonly IResearchDbContext _db;
    private readonly IAuditSink _audit;

    public VerifyFindingHandler(IResearchDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task Handle(VerifyFindingCommand request, CancellationToken ct)
    {
        var mission = await AddFindingHandler.Load(_db, request.MissionId, ct);
        mission.VerifyFinding(request.FindingId);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Research.FindingVerified", nameof(ResearchMission), mission.PublicId.ToString(), ct: ct);
    }
}
