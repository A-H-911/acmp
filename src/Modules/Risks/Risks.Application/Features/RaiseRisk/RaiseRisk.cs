using Acmp.Modules.Risks.Application.Abstractions;
using Acmp.Modules.Risks.Application.Contracts;
using Acmp.Modules.Risks.Application.Internal;
using Acmp.Modules.Risks.Domain;
using Acmp.Modules.Risks.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Risks.Application.Features.RaiseRisk;

// W15: raise a risk against a subject artifact. Creates an Open risk owned by the named member, optionally
// seeds the first mitigation from the design create form's "Mitigation plan" field (Type = Reduce by
// default — the form does not collect a type), notifies that owner (unless they raised it themselves), and
// audits. RBAC = Risk.Manage (Chairman/Secretary; Member/Reviewer allow-if-owner). Content is entered in one
// UI language and MIRRORED to both LocalizedString columns (the locked FTS pattern), so both EN+AR required.
public sealed record RaiseRiskCommand(
    LocalizedString Title,
    LocalizedString? Description,
    RiskLevel Likelihood,
    RiskLevel Impact,
    string OwnerUserId,
    string OwnerName,
    RiskSubjectType SubjectType,
    Guid SubjectId,
    string? SubjectKey,
    LocalizedString? InitialMitigation) : IRequest<RiskSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Member, AcmpRoles.Reviewer };
}

public sealed class RaiseRiskValidator : AbstractValidator<RaiseRiskCommand>
{
    public RaiseRiskValidator()
    {
        RuleFor(x => x.Title).NotNull().WithMessage("A title is required.");
        RuleFor(x => x.Title!.En).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (EN) is required (max 512).");
        RuleFor(x => x.Title!.Ar).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (AR) is required (max 512).");

        RuleFor(x => x.Description!.En).NotEmpty().When(x => x.Description is not null).WithMessage("Description (EN) is required when a description is given.");
        RuleFor(x => x.Description!.Ar).NotEmpty().When(x => x.Description is not null).WithMessage("Description (AR) is required when a description is given.");

        RuleFor(x => x.InitialMitigation!.En).NotEmpty().When(x => x.InitialMitigation is not null).WithMessage("Mitigation plan (EN) is required when a plan is given.");
        RuleFor(x => x.InitialMitigation!.Ar).NotEmpty().When(x => x.InitialMitigation is not null).WithMessage("Mitigation plan (AR) is required when a plan is given.");

        RuleFor(x => x.Likelihood).IsInEnum().WithMessage("A valid likelihood is required.");
        RuleFor(x => x.Impact).IsInEnum().WithMessage("A valid impact is required.");
        RuleFor(x => x.SubjectType).IsInEnum();
        RuleFor(x => x.OwnerUserId).NotEmpty().WithMessage("An owner is required.");
        RuleFor(x => x.OwnerName).NotEmpty().WithMessage("An owner name is required.");
        RuleFor(x => x.SubjectId).NotEmpty().WithMessage("A subject artifact is required.");
    }
}

public sealed class RaiseRiskHandler : IRequestHandler<RaiseRiskCommand, RiskSummaryDto>
{
    private readonly IRisksDbContext _db;
    private readonly IRiskKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly INotificationChannel _notifications;

    public RaiseRiskHandler(IRisksDbContext db, IRiskKeyGenerator keys, ICurrentUser user,
        IClock clock, IAuditSink audit, INotificationChannel notifications)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task<RiskSummaryDto> Handle(RaiseRiskCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, _) = CurrentActor.Of(_user);

        var key = await _keys.NextRiskKeyAsync(now.Year, ct);
        var risk = Risk.Create(key, request.Title, request.Description, request.Likelihood, request.Impact,
            request.OwnerUserId, request.OwnerName, request.SubjectType, request.SubjectId, request.SubjectKey, now);

        if (request.InitialMitigation is not null)
            risk.AddMitigation(request.InitialMitigation, MitigationType.Reduce, null, null, null);

        _db.Risks.Add(risk);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Risks.RiskRaised", nameof(Risk), risk.PublicId.ToString(), ct: ct);

        // W15 (docs/domain/workflows.md line 201): notify the owner (skip if the raiser owns it themselves — no self-noise).
        if (!string.Equals(risk.OwnerUserId, sub, StringComparison.Ordinal))
            await _notifications.PublishAsync(RiskNotifications.Assigned(risk.OwnerUserId, risk.Key), ct);

        return RiskMapping.ToSummary(risk);
    }
}
