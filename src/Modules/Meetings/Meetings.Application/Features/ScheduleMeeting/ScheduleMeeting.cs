using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Meetings.Application.Features.ScheduleMeeting;

// W5: schedule a committee meeting. Creates the Meeting plus an empty Draft Agenda (so the builder has
// a target). RBAC = Meeting.Schedule (Chairman/Secretary). The MeetingScheduled notification fan-out to
// committee members lands in P6b (the in-app channel); the domain event is already raised.
// CommitteeId is supplied by the caller (single committee, CON-001) — Meetings never reads Membership.
public sealed record ScheduleMeetingCommand(
    string Title,
    Guid CommitteeId,
    Guid ChairUserId,
    string ChairName,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd,
    string? Location,
    string? JoinUrl) : IRequest<MeetingSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class ScheduleMeetingValidator : AbstractValidator<ScheduleMeetingCommand>
{
    public ScheduleMeetingValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ChairUserId).NotEmpty().WithMessage("A chair must be assigned.");
        RuleFor(x => x.ChairName).NotEmpty();
        RuleFor(x => x.ScheduledEnd).GreaterThan(x => x.ScheduledStart)
            .WithMessage("Meeting end must be after its start.");
    }
}

public sealed class ScheduleMeetingHandler : IRequestHandler<ScheduleMeetingCommand, MeetingSummaryDto>
{
    private readonly IMeetingsDbContext _db;
    private readonly IMeetingKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public ScheduleMeetingHandler(IMeetingsDbContext db, IMeetingKeyGenerator keys,
        ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<MeetingSummaryDto> Handle(ScheduleMeetingCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, _) = CurrentActor.Of(_user);

        var meetingKey = await _keys.NextMeetingKeyAsync(now.Year, ct);
        var meeting = Meeting.Schedule(meetingKey, request.Title, request.CommitteeId,
            request.ChairUserId, request.ChairName, request.ScheduledStart, request.ScheduledEnd,
            request.Location, request.JoinUrl, now);

        var agendaKey = await _keys.NextAgendaKeyAsync(now.Year, ct);
        var agenda = Agenda.Draft(agendaKey, meeting.PublicId);

        _db.Meetings.Add(meeting);
        _db.Agendas.Add(agenda);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Meetings.MeetingScheduled", sub, new { meeting.PublicId, meeting.Key }, ct);
        return MeetingMapping.ToSummary(meeting, agenda);
    }
}
