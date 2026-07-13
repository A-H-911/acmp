using Acmp.Shared.Contracts.Topics;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Infrastructure.Persistence;

// Topics-side implementation of the cross-module ITopicReader seam (ADR-0001, P15c / FR-115): another module
// reads a topic's key + title snapshot (e.g. Research recording a Topic→Mission source edge) without touching
// Topics' tables. An unknown id returns null (a since-deleted or unknown source topic → the caller records no
// edge). Read-only; no lifecycle side effects (mirrors TopicStreamReader).
public sealed class TopicReader : ITopicReader
{
    private readonly TopicsDbContext _db;

    public TopicReader(TopicsDbContext db) => _db = db;

    public async Task<TopicSummary?> GetSummaryAsync(Guid topicId, CancellationToken ct = default)
    {
        var topic = await _db.Topics.AsNoTracking().FirstOrDefaultAsync(t => t.PublicId == topicId, ct);
        return topic is null ? null : new TopicSummary(topic.PublicId, topic.Key, topic.Title);
    }
}
