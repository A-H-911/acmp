using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.DraftMinutes;

// W10 (start MoM): the Secretary (or Chairman) opens an editable Draft against a concluded/live meeting.
// The parent Meeting is in the SAME module, so loading it to guard status + snapshot its key/title is an
// in-module read, not a cross-module reach (ADR-0001). One MoM per meeting — a second draft-from-scratch
// is rejected (corrections go through supersede). RBAC = Minutes.Capture (docs/10 row 8).
public sealed record DraftMinutesCommand(Guid MeetingId, LocalizedString Summary)
    : IRequest<MinutesSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Secretary, AcmpRoles.Chairman };
}

public sealed class DraftMinutesValidator : AbstractValidator<DraftMinutesCommand>
{
    public DraftMinutesValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty();

        // Content is mirrored to both columns (FTS), so both EN and AR must be present — a clean 400
        // rather than the LocalizedString ctor throwing at SaveChanges (docs/16 §1.5).
        RuleFor(x => x.Summary).NotNull().WithMessage("A summary is required.");
        RuleFor(x => x.Summary!.En).NotEmpty().When(x => x.Summary is not null).WithMessage("Summary (EN) is required.");
        RuleFor(x => x.Summary!.Ar).NotEmpty().When(x => x.Summary is not null).WithMessage("Summary (AR) is required.");
    }
}

public sealed class DraftMinutesHandler : IRequestHandler<DraftMinutesCommand, MinutesSummaryDto>
{
    private readonly IMeetingsDbContext _db;
    private readonly IMeetingKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public DraftMinutesHandler(IMeetingsDbContext db, IMeetingKeyGenerator keys,
        ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<MinutesSummaryDto> Handle(DraftMinutesCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, _) = CurrentActor.Of(_user);

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.PublicId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Meeting not found.");
        if (meeting.Status is not (MeetingStatus.InProgress or MeetingStatus.Held))
            throw new InvalidOperationException("Minutes can only be drafted for a meeting that is in progress or held.");
        if (await _db.Minutes.AnyAsync(m => m.MeetingId == request.MeetingId, ct))
            throw new InvalidOperationException("Minutes already exist for this meeting; correct them by superseding.");

        var key = await _keys.NextMinutesKeyAsync(now.Year, ct);
        var minutes = MinutesOfMeeting.Draft(key, meeting.PublicId, meeting.Key, meeting.Title, request.Summary, now);

        _db.Minutes.Add(minutes);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Meetings.MinutesDrafted", sub, new { minutes.PublicId, minutes.Key }, ct);
        return MinutesMapping.ToSummary(minutes);
    }
}
