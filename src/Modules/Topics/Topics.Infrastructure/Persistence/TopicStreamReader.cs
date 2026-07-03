using Acmp.Shared.Contracts.Topics;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Infrastructure.Persistence;

// Topics-side implementation of the cross-module ITopicStreamReader seam (ADR-0001, P10f / FR-095): the
// Traceability impact-graph composer calls this — never Topics' tables directly — to read a topic's affected
// stream codes and classify Topic↔Topic cross-stream edges. An unknown id returns empty (the far endpoint may
// be a routeless snapshot or a since-deleted topic), so cross-stream detection degrades to "not cross," never
// throws. Read-only; no lifecycle side effects.
public sealed class TopicStreamReader : ITopicStreamReader
{
    private readonly TopicsDbContext _db;

    public TopicStreamReader(TopicsDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> GetStreamsAsync(Guid topicId, CancellationToken ct = default)
    {
        var topic = await _db.Topics.AsNoTracking()
            .FirstOrDefaultAsync(t => t.PublicId == topicId, ct);

        return topic is null ? Array.Empty<string>() : topic.AffectedStreams.ToArray();
    }
}
