using Acmp.Modules.Risks.Application.Abstractions;
using Acmp.Modules.Risks.Application.Features.ManageMitigations;
using Acmp.Modules.Risks.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Risks.Application.Features.ChangeRiskStatus;

// W15 risk-level transitions: begin mitigation, close, escalate. All RBAC = Risk.Manage (Chairman/Secretary;
// Member/Reviewer allow-if-owner) — accept is the separate, more-narrowly-authorized slice (Risk.Accept).
// Begin/close share RiskTransition; escalate has its own handler because it fans a notification out to the
// Secretary + Chairman (BL-135). The domain enforces the legal state machine (a wrong-state call → 409).

// ── Begin mitigation: Open / Escalated → Mitigating (needs ≥1 mitigation) ────────────────────────────────
public sealed record BeginMitigationCommand(Guid RiskId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = RiskEditRoles.Value;
}

public sealed class BeginMitigationHandler : IRequestHandler<BeginMitigationCommand>
{
    private readonly IRisksDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public BeginMitigationHandler(IRisksDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public Task Handle(BeginMitigationCommand r, CancellationToken ct) =>
        RiskTransition.ApplyAsync(_db, _clock, _audit, _user, r.RiskId, "Risks.RiskMitigating",
            (risk, now) => risk.BeginMitigation(now), ct);
}

// ── Close: Mitigating / Escalated → Closed (mitigations Done or a closure note) ──────────────────────────
public sealed record CloseRiskCommand(Guid RiskId, LocalizedString? ClosureNote) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = RiskEditRoles.Value;
}

public sealed class CloseRiskValidator : AbstractValidator<CloseRiskCommand>
{
    public CloseRiskValidator()
    {
        RuleFor(x => x.ClosureNote!.En).NotEmpty().When(x => x.ClosureNote is not null).WithMessage("Closure note (EN) is required when a note is given.");
        RuleFor(x => x.ClosureNote!.Ar).NotEmpty().When(x => x.ClosureNote is not null).WithMessage("Closure note (AR) is required when a note is given.");
    }
}

public sealed class CloseRiskHandler : IRequestHandler<CloseRiskCommand>
{
    private readonly IRisksDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public CloseRiskHandler(IRisksDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public Task Handle(CloseRiskCommand r, CancellationToken ct) =>
        RiskTransition.ApplyAsync(_db, _clock, _audit, _user, r.RiskId, "Risks.RiskClosed",
            (risk, now) => risk.Close(r.ClosureNote, now), ct);
}

// ── Escalate: Open / Mitigating → Escalated (reason + target); notify Secretary + Chairman (BL-135) ──────
public sealed record EscalateRiskCommand(Guid RiskId, LocalizedString Reason, string Target)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = RiskEditRoles.Value;
}

public sealed class EscalateRiskValidator : AbstractValidator<EscalateRiskCommand>
{
    public EscalateRiskValidator()
    {
        RuleFor(x => x.Reason).NotNull().WithMessage("An escalation reason is required.");
        RuleFor(x => x.Reason!.En).NotEmpty().When(x => x.Reason is not null).WithMessage("Escalation reason (EN) is required.");
        RuleFor(x => x.Reason!.Ar).NotEmpty().When(x => x.Reason is not null).WithMessage("Escalation reason (AR) is required.");
        RuleFor(x => x.Target).NotEmpty().WithMessage("An escalation target is required.");
    }
}

public sealed class EscalateRiskHandler : IRequestHandler<EscalateRiskCommand>
{
    private readonly IRisksDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;
    private readonly ICommitteeDirectory _committee;
    private readonly INotificationChannel _notifications;

    public EscalateRiskHandler(IRisksDbContext db, IClock clock, IAuditSink audit, ICurrentUser user,
        ICommitteeDirectory committee, INotificationChannel notifications)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _user = user;
        _committee = committee;
        _notifications = notifications;
    }

    public async Task Handle(EscalateRiskCommand request, CancellationToken ct)
    {
        var risk = await _db.Risks.FirstOrDefaultAsync(r => r.PublicId == request.RiskId, ct)
            ?? throw new KeyNotFoundException("Risk not found.");

        var (sub, _) = CurrentActor.Of(_user);
        risk.Escalate(request.Reason, request.Target, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Risks.RiskEscalated", sub,
            new { risk.PublicId, risk.Key, Target = risk.EscalationTarget }, ct);

        // BL-135: fan out to the current Secretary + Chairman (headless roster lookup), skipping the actor.
        var secretaries = await _committee.GetActiveMembersInRoleAsync(AcmpRoles.Secretary, ct);
        var chairmen = await _committee.GetActiveMembersInRoleAsync(AcmpRoles.Chairman, ct);
        var recipients = secretaries.Concat(chairmen)
            .Select(m => m.UserId)
            .Where(id => !string.Equals(id, sub, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal);

        foreach (var recipient in recipients)
            await _notifications.PublishAsync(
                RiskNotifications.Escalated(recipient, risk.Key, risk.EscalationTarget ?? request.Target), ct);
    }
}
