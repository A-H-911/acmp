using Acmp.Modules.Actions.Application.Abstractions;
using Acmp.Modules.Actions.Application.Contracts;
using Acmp.Modules.Actions.Application.Internal;
using Acmp.Modules.Actions.Domain;
using Acmp.Modules.Actions.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Actions.Application.Features.CreateAction;

// W13: create a follow-up action against a source artifact (decision/condition/meeting/topic/risk). Creates
// an Open action owned by the named member, notifies that owner (unless they created it themselves), and
// audits. RBAC = Action.Create (Chairman/Secretary; Member allow-if-owner). Content is entered in one UI
// language and MIRRORED to both LocalizedString columns (the locked FTS pattern), so both EN+AR are required.
public sealed record CreateActionCommand(
    LocalizedString Title,
    LocalizedString? Description,
    ActionPriority Priority,
    string OwnerUserId,
    string OwnerName,
    DateTimeOffset? DueDate,
    ActionSourceType SourceType,
    Guid SourceId,
    string? SourceKey,
    string? MeetingKey) : IRequest<ActionSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Member };
}

public sealed class CreateActionValidator : AbstractValidator<CreateActionCommand>
{
    public CreateActionValidator()
    {
        // LocalizedString's positional ctor does not validate (only Create does, which throws → 500). The
        // boundary checks live here for a clean 400 (docs/16 §1.5). Title is mirrored EN+AR; the per-language
        // max guards the nvarchar(512) column so an over-long title is a clean 400, not a SaveChanges 500.
        RuleFor(x => x.Title).NotNull().WithMessage("A title is required.");
        RuleFor(x => x.Title!.En).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (EN) is required (max 512).");
        RuleFor(x => x.Title!.Ar).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (AR) is required (max 512).");

        // Description is optional, but when present both languages must be filled (mirrored).
        RuleFor(x => x.Description!.En).NotEmpty().When(x => x.Description is not null).WithMessage("Description (EN) is required when a description is given.");
        RuleFor(x => x.Description!.Ar).NotEmpty().When(x => x.Description is not null).WithMessage("Description (AR) is required when a description is given.");

        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.SourceType).IsInEnum();
        RuleFor(x => x.OwnerUserId).NotEmpty().WithMessage("An owner is required.");
        RuleFor(x => x.OwnerName).NotEmpty().WithMessage("An owner name is required.");
        RuleFor(x => x.SourceId).NotEmpty().WithMessage("A source artifact is required.");
    }
}

public sealed class CreateActionHandler : IRequestHandler<CreateActionCommand, ActionSummaryDto>
{
    private readonly IActionsDbContext _db;
    private readonly IActionKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly INotificationChannel _notifications;

    public CreateActionHandler(IActionsDbContext db, IActionKeyGenerator keys, ICurrentUser user,
        IClock clock, IAuditSink audit, INotificationChannel notifications)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task<ActionSummaryDto> Handle(CreateActionCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, _) = CurrentActor.Of(_user);

        var key = await _keys.NextActionKeyAsync(now.Year, ct);
        var action = ActionItem.Create(key, request.Title, request.Description, request.Priority,
            request.OwnerUserId, request.OwnerName, request.DueDate, request.SourceType, request.SourceId,
            request.SourceKey, request.MeetingKey, now);

        _db.Actions.Add(action);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Actions.ActionCreated", sub,
            new { action.PublicId, action.Key, action.OwnerUserId, SourceType = action.SourceType.ToString() }, ct);

        // W13: notify the assigned owner (skip if the creator assigned it to themselves — no self-noise).
        if (!string.Equals(action.OwnerUserId, sub, StringComparison.Ordinal))
            await _notifications.PublishAsync(ActionNotifications.Assigned(action.OwnerUserId, action.Key), ct);

        return ActionMapping.ToSummary(action, now);
    }
}
