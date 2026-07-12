using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Topics;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.PublishAgenda;

// W6: publish (or re-publish) the agenda. The domain guards items + per-item presenters and versions
// the agenda; then each placed topic is advanced Prepared → Scheduled via the cross-module
// ITopicScheduler seam (Meetings never writes Topics' tables, ADR-0001). RBAC = Agenda.Publish.
// AC-051: on publish, every active committee member gets an in-app notification carrying the meeting
// date, the agenda title, and a deep link to the agenda view, delivered within ≤5s (synchronous write).
// ponytail: the per-topic scheduler is idempotent, so a re-publish (or a retry after a mid-loop
// failure) is safe; at committee scale the rare partial flip self-heals on the next publish.
// ponytail: a re-publish DOES re-notify every member (notifications are not deduped) — intentional, a
// changed agenda is worth re-announcing at committee scale; revisit if re-publish becomes noisy.
public sealed record PublishAgendaCommand(Guid MeetingId) : IRequest<AgendaDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class PublishAgendaValidator : AbstractValidator<PublishAgendaCommand>
{
    public PublishAgendaValidator() => RuleFor(x => x.MeetingId).NotEmpty();
}

public sealed class PublishAgendaHandler : IRequestHandler<PublishAgendaCommand, AgendaDto>
{
    private readonly IMeetingsDbContext _db;
    private readonly ITopicScheduler _topics;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICommitteeDirectory _directory;
    private readonly INotificationChannel _notifications;

    public PublishAgendaHandler(IMeetingsDbContext db, ITopicScheduler topics,
        ICurrentUser user, IClock clock, IAuditSink audit,
        ICommitteeDirectory directory, INotificationChannel notifications)
    {
        _db = db;
        _topics = topics;
        _user = user;
        _clock = clock;
        _audit = audit;
        _directory = directory;
        _notifications = notifications;
    }

    public async Task<AgendaDto> Handle(PublishAgendaCommand request, CancellationToken ct)
    {
        var agenda = await _db.Agendas.FirstOrDefaultAsync(a => a.MeetingId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Agenda not found for this meeting.");
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.PublicId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Meeting not found.");

        agenda.Publish(_clock.UtcNow);

        foreach (var item in agenda.Items)
            await _topics.ScheduleAsync(item.TopicId, agenda.MeetingId, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Meetings.AgendaPublished", nameof(Agenda), agenda.PublicId.ToString(), ct: ct);

        // AC-051: notify every active committee member with date + agenda title + deep link to the agenda.
        await MeetingNotifications.FanOutAsync(_directory, _notifications,
            MeetingNotifications.AgendaPublished(meeting.Title, meeting.Key, meeting.ScheduledStart), ct);

        return MeetingMapping.ToDto(agenda);
    }
}
