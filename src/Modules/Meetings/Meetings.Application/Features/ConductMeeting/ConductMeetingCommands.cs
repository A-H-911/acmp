using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Topics;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.ConductMeeting;

// W7/W8/W9 live-meeting commands. Process control (start/end/actual-time) = Meeting.Schedule;
// attendance = Attendance.Record; discussion capture = Minutes.Capture (Chairman/Secretary).

internal static class ConductRoles
{
    public static readonly string[] Chairing = { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

// ---- W7: start the meeting → lock the agenda → move each placed topic InCommittee ----
public sealed record StartMeetingCommand(Guid MeetingId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ConductRoles.Chairing;
}

public sealed class StartMeetingHandler : IRequestHandler<StartMeetingCommand>
{
    private readonly IMeetingsDbContext _db;
    private readonly ITopicScheduler _topics;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public StartMeetingHandler(IMeetingsDbContext db, ITopicScheduler topics, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _topics = topics;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(StartMeetingCommand request, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.PublicId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Meeting not found.");
        var agenda = await _db.Agendas.FirstOrDefaultAsync(a => a.MeetingId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Agenda not found for this meeting.");

        meeting.Start(_clock.UtcNow);
        agenda.Lock();
        foreach (var item in agenda.Items)
            await _topics.EnterCommitteeAsync(item.TopicId, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.EmitAsync("Meetings.MeetingStarted", _user.UserId, new { meeting.PublicId, meeting.Key }, ct);
    }
}

// ---- W8: record attendance / apologies (seeds the roster row on first touch) ----
public sealed record MarkAttendanceCommand(
    Guid MeetingId, Guid UserId, string Name, AttendanceRole Role, AttendanceStatus Status, bool IsVotingEligible)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ConductRoles.Chairing;
}

public sealed class MarkAttendanceValidator : AbstractValidator<MarkAttendanceCommand>
{
    public MarkAttendanceValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
    }
}

public sealed class MarkAttendanceHandler : IRequestHandler<MarkAttendanceCommand>
{
    private readonly IMeetingsDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public MarkAttendanceHandler(IMeetingsDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _user = user;
    }

    public async Task Handle(MarkAttendanceCommand request, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.PublicId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Meeting not found.");

        meeting.SeedAttendee(request.UserId, request.Name, request.Role, request.IsVotingEligible);
        meeting.MarkAttendance(request.UserId, request.Status, _clock.UtcNow);

        await _db.SaveChangesAsync(ct);
        await _audit.EmitAsync("Meetings.AttendanceRecorded", _user.UserId,
            new { meeting.PublicId, request.UserId, Status = request.Status.ToString() }, ct);
    }
}

// ---- W9: capture/curate the discussion note for an agenda topic ----
public sealed record CaptureDiscussionCommand(Guid MeetingId, Guid TopicId, string Body) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ConductRoles.Chairing;
}

public sealed class CaptureDiscussionValidator : AbstractValidator<CaptureDiscussionCommand>
{
    public CaptureDiscussionValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty();
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().WithMessage("Discussion notes cannot be empty.");
    }
}

public sealed class CaptureDiscussionHandler : IRequestHandler<CaptureDiscussionCommand>
{
    private readonly IMeetingsDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;

    public CaptureDiscussionHandler(IMeetingsDbContext db, IClock clock, ICurrentUser user)
    {
        _db = db;
        _clock = clock;
        _user = user;
    }

    public async Task Handle(CaptureDiscussionCommand request, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.PublicId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Meeting not found.");

        var (sub, name) = CurrentActor.Of(_user);
        meeting.SetDiscussionNote(request.TopicId, request.Body, sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }
}

// ---- W7: record per-item actual time + outcome (the design's "12:30 / 20:00") ----
public sealed record RecordActualTimeCommand(Guid MeetingId, Guid TopicId, int ActualMinutes, AgendaItemOutcome? Outcome)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ConductRoles.Chairing;
}

public sealed class RecordActualTimeHandler : IRequestHandler<RecordActualTimeCommand>
{
    private readonly IMeetingsDbContext _db;

    public RecordActualTimeHandler(IMeetingsDbContext db) => _db = db;

    public async Task Handle(RecordActualTimeCommand request, CancellationToken ct)
    {
        var agenda = await _db.Agendas.FirstOrDefaultAsync(a => a.MeetingId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Agenda not found for this meeting.");

        agenda.RecordActualMinutes(request.TopicId, request.ActualMinutes);
        if (request.Outcome is { } outcome) agenda.SetOutcome(request.TopicId, outcome);
        await _db.SaveChangesAsync(ct);
    }
}

// ---- W7: conclude the meeting → close the agenda ----
public sealed record EndMeetingCommand(Guid MeetingId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = ConductRoles.Chairing;
}

public sealed class EndMeetingHandler : IRequestHandler<EndMeetingCommand>
{
    private readonly IMeetingsDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public EndMeetingHandler(IMeetingsDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _user = user;
    }

    public async Task Handle(EndMeetingCommand request, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.PublicId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Meeting not found.");
        var agenda = await _db.Agendas.FirstOrDefaultAsync(a => a.MeetingId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Agenda not found for this meeting.");

        meeting.Hold(_clock.UtcNow);
        agenda.Close();
        await _db.SaveChangesAsync(ct);
        await _audit.EmitAsync("Meetings.MeetingHeld", _user.UserId, new { meeting.PublicId, meeting.Key }, ct);
    }
}
