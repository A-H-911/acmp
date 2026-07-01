using Acmp.Modules.Actions.Application.Abstractions;
using Acmp.Modules.Actions.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Actions.Application.Features.ChangeActionStatus;

// The simple W14 lifecycle transitions grouped as one vertical slice: start, block, unblock, update
// progress, complete, cancel. Each is RBAC = Action.Create (Chairman/Secretary; Member allow-if-owner) —
// verification is the separate SoD-gated slice. They share ActionTransition (load → mutate → save → audit);
// the domain enforces the legal state machine (a wrong-state call throws → 409). Reasons are bilingual
// (guardrail 9), mirrored EN+AR.

// ── the roles that may edit an action's progress/status (docs/10 row 14) ────────────────────────────────
internal static class ActionEditRoles
{
    public static readonly string[] Value = { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Member };
}

// ── Start: Open → InProgress ─────────────────────────────────────────────────────────────────────────────
public sealed record StartActionCommand(Guid ActionId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ActionEditRoles.Value;
}

public sealed class StartActionHandler : IRequestHandler<StartActionCommand>
{
    private readonly IActionsDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public StartActionHandler(IActionsDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public Task Handle(StartActionCommand r, CancellationToken ct) =>
        ActionTransition.ApplyAsync(_db, _clock, _audit, _user, r.ActionId, "Actions.ActionStarted",
            (a, now) => a.Start(now), ct);
}

// ── Unblock: Blocked → InProgress ────────────────────────────────────────────────────────────────────────
public sealed record UnblockActionCommand(Guid ActionId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ActionEditRoles.Value;
}

public sealed class UnblockActionHandler : IRequestHandler<UnblockActionCommand>
{
    private readonly IActionsDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public UnblockActionHandler(IActionsDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public Task Handle(UnblockActionCommand r, CancellationToken ct) =>
        ActionTransition.ApplyAsync(_db, _clock, _audit, _user, r.ActionId, "Actions.ActionUnblocked",
            (a, now) => a.Unblock(now), ct);
}

// ── Block: InProgress → Blocked (reason required) ────────────────────────────────────────────────────────
public sealed record BlockActionCommand(Guid ActionId, LocalizedString Reason) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ActionEditRoles.Value;
}

public sealed class BlockActionValidator : AbstractValidator<BlockActionCommand>
{
    public BlockActionValidator()
    {
        RuleFor(x => x.Reason).NotNull().WithMessage("A blocking reason is required.");
        RuleFor(x => x.Reason!.En).NotEmpty().When(x => x.Reason is not null).WithMessage("Blocking reason (EN) is required.");
        RuleFor(x => x.Reason!.Ar).NotEmpty().When(x => x.Reason is not null).WithMessage("Blocking reason (AR) is required.");
    }
}

public sealed class BlockActionHandler : IRequestHandler<BlockActionCommand>
{
    private readonly IActionsDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public BlockActionHandler(IActionsDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public Task Handle(BlockActionCommand r, CancellationToken ct) =>
        ActionTransition.ApplyAsync(_db, _clock, _audit, _user, r.ActionId, "Actions.ActionBlocked",
            (a, now) => a.Block(r.Reason, now), ct);
}

// ── UpdateProgress: set 0–100 while live ─────────────────────────────────────────────────────────────────
public sealed record UpdateActionProgressCommand(Guid ActionId, int ProgressPct) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ActionEditRoles.Value;
}

public sealed class UpdateActionProgressValidator : AbstractValidator<UpdateActionProgressCommand>
{
    public UpdateActionProgressValidator() =>
        RuleFor(x => x.ProgressPct).InclusiveBetween(0, 100).WithMessage("Progress must be between 0 and 100.");
}

public sealed class UpdateActionProgressHandler : IRequestHandler<UpdateActionProgressCommand>
{
    private readonly IActionsDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public UpdateActionProgressHandler(IActionsDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public Task Handle(UpdateActionProgressCommand r, CancellationToken ct) =>
        ActionTransition.ApplyAsync(_db, _clock, _audit, _user, r.ActionId, "Actions.ActionProgressUpdated",
            (a, _) => a.UpdateProgress(r.ProgressPct), ct);
}

// ── Complete: InProgress → Completed (optional evidence note; stamps the completer for SoD-1) ────────────
public sealed record CompleteActionCommand(Guid ActionId, LocalizedString? CompletionNote) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ActionEditRoles.Value;
}

public sealed class CompleteActionValidator : AbstractValidator<CompleteActionCommand>
{
    public CompleteActionValidator()
    {
        RuleFor(x => x.CompletionNote!.En).NotEmpty().When(x => x.CompletionNote is not null).WithMessage("Completion note (EN) is required when a note is given.");
        RuleFor(x => x.CompletionNote!.Ar).NotEmpty().When(x => x.CompletionNote is not null).WithMessage("Completion note (AR) is required when a note is given.");
    }
}

public sealed class CompleteActionHandler : IRequestHandler<CompleteActionCommand>
{
    private readonly IActionsDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public CompleteActionHandler(IActionsDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public Task Handle(CompleteActionCommand r, CancellationToken ct)
    {
        var (sub, _) = CurrentActor.Of(_user);
        return ActionTransition.ApplyAsync(_db, _clock, _audit, _user, r.ActionId, "Actions.ActionCompleted",
            (a, now) => a.Complete(r.CompletionNote, sub, now), ct);
    }
}

// ── Cancel: any non-terminal → Cancelled (reason required) ───────────────────────────────────────────────
public sealed record CancelActionCommand(Guid ActionId, LocalizedString Reason) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ActionEditRoles.Value;
}

public sealed class CancelActionValidator : AbstractValidator<CancelActionCommand>
{
    public CancelActionValidator()
    {
        RuleFor(x => x.Reason).NotNull().WithMessage("A cancellation reason is required.");
        RuleFor(x => x.Reason!.En).NotEmpty().When(x => x.Reason is not null).WithMessage("Cancellation reason (EN) is required.");
        RuleFor(x => x.Reason!.Ar).NotEmpty().When(x => x.Reason is not null).WithMessage("Cancellation reason (AR) is required.");
    }
}

public sealed class CancelActionHandler : IRequestHandler<CancelActionCommand>
{
    private readonly IActionsDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public CancelActionHandler(IActionsDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public Task Handle(CancelActionCommand r, CancellationToken ct) =>
        ActionTransition.ApplyAsync(_db, _clock, _audit, _user, r.ActionId, "Actions.ActionCancelled",
            (a, now) => a.Cancel(r.Reason, now), ct);
}
