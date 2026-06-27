using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Topics;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Infrastructure.Persistence;

// Topics-side implementation of the cross-module ITopicScheduler seam (ADR-0001): the Meetings module
// calls this — never Topics' tables directly — to advance a topic's lifecycle when it is placed on a
// published agenda (Prepared → Scheduled) or when the meeting starts (Scheduled → InCommittee). Both
// operations are idempotent: a topic not in the expected source state is left untouched, so re-publish
// or re-start never throws. Actor attribution uses the calling principal (the Secretary/Chairman).
public sealed class TopicScheduler : ITopicScheduler
{
    private readonly TopicsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public TopicScheduler(TopicsDbContext db, ICurrentUser user, IClock clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task ScheduleAsync(Guid topicId, Guid meetingId, CancellationToken ct = default)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == topicId, ct);
        if (topic is null || topic.Status != TopicStatus.Prepared) return;

        var (sub, name) = Actor();
        topic.Schedule(meetingId, sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }

    public async Task EnterCommitteeAsync(Guid topicId, CancellationToken ct = default)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == topicId, ct);
        if (topic is null || topic.Status != TopicStatus.Scheduled) return;

        var (sub, name) = Actor();
        topic.EnterCommittee(sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }

    private (string Sub, string Name) Actor() =>
        (_user.UserId ?? "system", _user.DisplayName ?? _user.UserName ?? _user.Email ?? "Unknown");
}
