using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Topics;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Infrastructure.Persistence;

// Topics-side implementation of the cross-module ITopicDecisionRecorder seam (ADR-0001): the Decisions
// module calls this — never Topics' tables directly — to advance a topic to Decided when a decision is
// issued (W12). Idempotent: a topic not currently InCommittee is left untouched, so a decision recorded
// outside the live flow, or a supersession's successor (topic already Decided), never throws. Actor
// attribution uses the calling principal (the Chairman issuing the decision).
public sealed class TopicDecisionRecorder : ITopicDecisionRecorder
{
    private readonly TopicsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public TopicDecisionRecorder(TopicsDbContext db, ICurrentUser user, IClock clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task MarkDecidedAsync(Guid topicId, CancellationToken ct = default)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == topicId, ct);
        if (topic is null || topic.Status != TopicStatus.InCommittee) return;

        var sub = _user.UserId ?? "system";
        var name = _user.DisplayName ?? _user.UserName ?? _user.Email ?? "Unknown";
        topic.Decide(sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }
}
