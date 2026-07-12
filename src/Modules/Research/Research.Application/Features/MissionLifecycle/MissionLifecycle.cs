using Acmp.Modules.Research.Application.Abstractions;
using Acmp.Modules.Research.Application.Internal;
using Acmp.Modules.Research.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Research.Application.Features.MissionLifecycle;

// FR-111 lifecycle transitions (Proposed → Active → Completed, side-exit → Cancelled). RBAC = Research.Manage
// for all three. Each loads the mission, applies the aggregate transition (which 409s a terminal-state
// re-transition via InvalidOperationException), saves, and audits. No notifications in P15a.

public sealed record ActivateMissionCommand(Guid MissionId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ResearchRoles.Manage;
}

public sealed record CompleteMissionCommand(Guid MissionId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ResearchRoles.Manage;
}

public sealed record CancelMissionCommand(Guid MissionId, LocalizedString Reason) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ResearchRoles.Manage;
}

public sealed class CancelMissionValidator : AbstractValidator<CancelMissionCommand>
{
    public CancelMissionValidator()
    {
        RuleFor(x => x.Reason).NotNull().WithMessage("A cancellation reason is required.");
        RuleFor(x => x.Reason!.En).NotEmpty().When(x => x.Reason is not null).WithMessage("Reason (EN) is required.");
        RuleFor(x => x.Reason!.Ar).NotEmpty().When(x => x.Reason is not null).WithMessage("Reason (AR) is required.");
    }
}

public sealed class ActivateMissionHandler : IRequestHandler<ActivateMissionCommand>
{
    private readonly IResearchDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public ActivateMissionHandler(IResearchDbContext db, IClock clock, IAuditSink audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(ActivateMissionCommand request, CancellationToken ct)
    {
        var mission = await Load(_db, request.MissionId, ct);
        mission.Activate(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Research.MissionActivated", nameof(ResearchMission), mission.PublicId.ToString(), ct: ct);
    }

    internal static async Task<ResearchMission> Load(IResearchDbContext db, Guid id, CancellationToken ct) =>
        await db.Missions.FirstOrDefaultAsync(m => m.PublicId == id, ct)
        ?? throw new KeyNotFoundException("Research mission not found.");
}

public sealed class CompleteMissionHandler : IRequestHandler<CompleteMissionCommand>
{
    private readonly IResearchDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public CompleteMissionHandler(IResearchDbContext db, IClock clock, IAuditSink audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(CompleteMissionCommand request, CancellationToken ct)
    {
        var mission = await ActivateMissionHandler.Load(_db, request.MissionId, ct);
        mission.Complete(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Research.MissionCompleted", nameof(ResearchMission), mission.PublicId.ToString(), ct: ct);
    }
}

public sealed class CancelMissionHandler : IRequestHandler<CancelMissionCommand>
{
    private readonly IResearchDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public CancelMissionHandler(IResearchDbContext db, IClock clock, IAuditSink audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(CancelMissionCommand request, CancellationToken ct)
    {
        var mission = await ActivateMissionHandler.Load(_db, request.MissionId, ct);
        mission.Cancel(request.Reason, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Research.MissionCancelled", nameof(ResearchMission), mission.PublicId.ToString(), ct: ct);
    }
}
