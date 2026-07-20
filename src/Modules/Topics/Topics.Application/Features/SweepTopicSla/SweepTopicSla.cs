using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.SweepTopicSla;

// AC-057 / docs/domain/notification-strategy.md: the recurring sweep that turns a derived SLA breach
// (TopicAging.IsBreaching — time in the current status vs the urgency threshold) into a Secretary in-app
// notification. HEADLESS — no ICurrentUser in a Hangfire scope, so the audit actor is the system. Idempotent:
// Topic.SlaNotifiedAt gates the send and is reset on any status transition, so a topic is notified at most once
// per breach window and re-notified only if it breaches again in a later status. ALL logic is here and
// unit-tested against a fake clock + in-memory topics; Hangfire only cron-triggers it.
public sealed record SweepTopicSlaCommand : IRequest<int>;

public sealed class SweepTopicSlaHandler : IRequestHandler<SweepTopicSlaCommand, int>
{
    private const string SystemActor = "system:topic-sla";

    private readonly ITopicsDbContext _db;
    private readonly IClock _clock;
    private readonly INotificationChannel _notifications;
    private readonly ICommitteeDirectory _directory;
    private readonly IAuditSink _audit;

    public SweepTopicSlaHandler(ITopicsDbContext db, IClock clock, INotificationChannel notifications,
        ICommitteeDirectory directory, IAuditSink audit)
    {
        _db = db;
        _clock = clock;
        _notifications = notifications;
        _directory = directory;
        _audit = audit;
    }

    public async Task<int> Handle(SweepTopicSlaCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        // Only topics never notified for their CURRENT status window can qualify (the marker resets on transition).
        // IsBreaching reads History (time-in-current-status), so History must be loaded, and it excludes terminal
        // statuses — a topic that has left the backlog no longer ages.
        var candidates = await _db.Topics.Include(t => t.History)
            .Where(t => t.SlaNotifiedAt == null)
            .ToListAsync(ct);

        var breaching = candidates.Where(t => TopicAging.IsBreaching(t, now)).ToList();
        if (breaching.Count == 0)
            return 0;

        // Commit the markers FIRST (idempotency), THEN send: a failed save sends nothing and Hangfire retries the
        // whole sweep — the persisted markers keep the retry from double-notifying. At-most-once by design.
        foreach (var topic in breaching)
            topic.MarkSlaNotified(now);
        await _db.SaveChangesAsync(ct);

        var secretaries = await _directory.GetActiveMembersInRoleAsync(AcmpRoles.Secretary, ct);
        foreach (var topic in breaching)
        {
            var thresholdDays = TopicAging.SlaThresholdDays(topic.Urgency);
            var build = TopicNotifications.TopicSlaBreached(topic.Key, thresholdDays);
            foreach (var sec in secretaries)
                await _notifications.PublishAsync(build(sec.UserId), ct);

            await _audit.EmitAsync("Topics.SlaBreachNotified", SystemActor,
                new { topic.Key, Urgency = topic.Urgency.ToString(), ThresholdDays = thresholdDays }, ct);
        }

        return breaching.Count;
    }
}
