using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Topics;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.PublishAgenda;

// W6: publish (or re-publish) the agenda. The domain guards items + per-item presenters and versions
// the agenda; then each placed topic is advanced Prepared → Scheduled via the cross-module
// ITopicScheduler seam (Meetings never writes Topics' tables, ADR-0001). RBAC = Agenda.Publish.
// AC-051: the in-app "agenda published" notification to committee members is wired in P6b — this
// handler raises AgendaPublished and is where that fan-out hooks in.
// ponytail: the per-topic scheduler is idempotent, so a re-publish (or a retry after a mid-loop
// failure) is safe; at committee scale the rare partial flip self-heals on the next publish.
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

    public PublishAgendaHandler(IMeetingsDbContext db, ITopicScheduler topics,
        ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _topics = topics;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<AgendaDto> Handle(PublishAgendaCommand request, CancellationToken ct)
    {
        var agenda = await _db.Agendas.FirstOrDefaultAsync(a => a.MeetingId == request.MeetingId, ct)
            ?? throw new KeyNotFoundException("Agenda not found for this meeting.");

        agenda.Publish(_clock.UtcNow);

        foreach (var item in agenda.Items)
            await _topics.ScheduleAsync(item.TopicId, agenda.MeetingId, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.EmitAsync("Meetings.AgendaPublished", _user.UserId,
            new { agenda.PublicId, agenda.Key, agenda.MeetingId, agenda.Version, ItemCount = agenda.Items.Count }, ct);

        return MeetingMapping.ToDto(agenda);
    }
}
