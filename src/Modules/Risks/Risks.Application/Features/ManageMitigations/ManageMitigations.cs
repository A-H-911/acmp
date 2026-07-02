using Acmp.Modules.Risks.Application.Abstractions;
using Acmp.Modules.Risks.Application.Internal;
using Acmp.Modules.Risks.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Risks.Application.Features.ManageMitigations;

// W15 mitigation management: plan a mitigation, advance its status (Planned → InProgress → Done). Both are
// RBAC = Risk.Manage (Chairman/Secretary; Member/Reviewer allow-if-owner) and audited (docs/11 §Mitigation
// "full mutation audited"). They share RiskTransition (load → mutate → save → audit); the aggregate enforces
// that the risk is still live and that a mitigation never regresses.

internal static class RiskEditRoles
{
    public static readonly string[] Value = { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Member, AcmpRoles.Reviewer };
}

// ── Add a mitigation ─────────────────────────────────────────────────────────────────────────────────────
public sealed record AddMitigationCommand(
    Guid RiskId,
    LocalizedString Description,
    MitigationType Type,
    string? OwnerUserId,
    Guid? LinkedActionId,
    DateTimeOffset? DueDate) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = RiskEditRoles.Value;
}

public sealed class AddMitigationValidator : AbstractValidator<AddMitigationCommand>
{
    public AddMitigationValidator()
    {
        RuleFor(x => x.Description).NotNull().WithMessage("A mitigation description is required.");
        RuleFor(x => x.Description!.En).NotEmpty().When(x => x.Description is not null).WithMessage("Mitigation (EN) is required.");
        RuleFor(x => x.Description!.Ar).NotEmpty().When(x => x.Description is not null).WithMessage("Mitigation (AR) is required.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("A valid mitigation type is required.");
    }
}

public sealed class AddMitigationHandler : IRequestHandler<AddMitigationCommand>
{
    private readonly IRisksDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public AddMitigationHandler(IRisksDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public Task Handle(AddMitigationCommand r, CancellationToken ct) =>
        RiskTransition.ApplyAsync(_db, _clock, _audit, _user, r.RiskId, "Risks.MitigationAdded",
            (risk, _) => risk.AddMitigation(r.Description, r.Type, r.OwnerUserId, r.LinkedActionId, r.DueDate), ct);
}

// ── Advance a mitigation's status ────────────────────────────────────────────────────────────────────────
public sealed record SetMitigationStatusCommand(Guid RiskId, Guid MitigationId, MitigationStatus Status)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = RiskEditRoles.Value;
}

public sealed class SetMitigationStatusValidator : AbstractValidator<SetMitigationStatusCommand>
{
    public SetMitigationStatusValidator() =>
        RuleFor(x => x.Status).IsInEnum().WithMessage("A valid mitigation status is required.");
}

public sealed class SetMitigationStatusHandler : IRequestHandler<SetMitigationStatusCommand>
{
    private readonly IRisksDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public SetMitigationStatusHandler(IRisksDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public Task Handle(SetMitigationStatusCommand r, CancellationToken ct) =>
        RiskTransition.ApplyAsync(_db, _clock, _audit, _user, r.RiskId, "Risks.MitigationStatusChanged",
            (risk, _) => risk.SetMitigationStatus(r.MitigationId, r.Status), ct);
}
