using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.SupersedeMinutes;

// W10 (AC-036): correct a published (or approved) MoM. In ONE transaction: build the corrected successor
// already Published under the SAME key with Version+1, then flip the prior to Superseded with a back-link.
// The prior's content is never edited — it stays a readable, immutable version. Because the successor is a
// freshly published record, it fans out the publish notification too (AC-038 semantics; mirrors the
// decision supersede-creates-issued-successor path). RBAC = Minutes.Approve.
//
// Version-PRESERVING supersede: unlike a Decision (which mints a NEW DECN key), a MoM correction keeps the
// same MIN key and bumps the version — the minutes of one meeting are one document with a version history.
public sealed record SupersedeMinutesCommand(Guid PriorMinutesId, LocalizedString Summary, LocalizedString Reason)
    : IRequest<MinutesSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Secretary, AcmpRoles.Chairman };
}

public sealed class SupersedeMinutesValidator : AbstractValidator<SupersedeMinutesCommand>
{
    public SupersedeMinutesValidator()
    {
        RuleFor(x => x.PriorMinutesId).NotEmpty();
        RuleFor(x => x.Summary).NotNull().WithMessage("A summary is required.");
        RuleFor(x => x.Summary!.En).NotEmpty().When(x => x.Summary is not null).WithMessage("Summary (EN) is required.");
        RuleFor(x => x.Summary!.Ar).NotEmpty().When(x => x.Summary is not null).WithMessage("Summary (AR) is required.");

        RuleFor(x => x.Reason).NotNull().WithMessage("A supersession reason is required.");
        RuleFor(x => x.Reason!.En).NotEmpty().When(x => x.Reason is not null).WithMessage("Supersession reason (EN) is required.");
        RuleFor(x => x.Reason!.Ar).NotEmpty().When(x => x.Reason is not null).WithMessage("Supersession reason (AR) is required.");
    }
}

public sealed class SupersedeMinutesHandler : IRequestHandler<SupersedeMinutesCommand, MinutesSummaryDto>
{
    private readonly IMeetingsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICommitteeDirectory _directory;
    private readonly INotificationChannel _notifications;

    public SupersedeMinutesHandler(IMeetingsDbContext db, ICurrentUser user, IClock clock, IAuditSink audit,
        ICommitteeDirectory directory, INotificationChannel notifications)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
        _directory = directory;
        _notifications = notifications;
    }

    public async Task<MinutesSummaryDto> Handle(SupersedeMinutesCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, name) = CurrentActor.Of(_user);

        var prior = await _db.Minutes.FirstOrDefaultAsync(m => m.PublicId == request.PriorMinutesId, ct)
            ?? throw new KeyNotFoundException("Minutes not found.");

        // Same key, next version — the successor is published in one shot (the actor holds Minutes.Approve).
        var successor = MinutesOfMeeting.PublishedCorrection(prior.Key, prior.MeetingId, prior.MeetingKey,
            prior.MeetingTitle, prior.Version + 1, request.Summary, sub, name, now);
        _db.Minutes.Add(successor);

        prior.Supersede(successor.PublicId, request.Reason, now);
        await _db.SaveChangesAsync(ct);

        await MinutesNotifications.FanOutAsync(_directory, _notifications,
            MinutesNotifications.MinutesPublished(successor.MeetingTitle, successor.MeetingKey), ct);
        await _audit.EmitEnrichedAsync("Meetings.MinutesSuperseded", nameof(MinutesOfMeeting), prior.PublicId.ToString(), ct: ct);
        await _audit.EmitEnrichedAsync("Meetings.MinutesPublished", nameof(MinutesOfMeeting), successor.PublicId.ToString(), ct: ct);

        return MinutesMapping.ToSummary(successor);
    }
}
