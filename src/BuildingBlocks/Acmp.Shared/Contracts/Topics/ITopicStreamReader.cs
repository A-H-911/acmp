namespace Acmp.Shared.Contracts.Topics;

// Cross-module read seam (ADR-0001, P10f / FR-095 Topic-scope): the Traceability impact-graph composer asks
// the Topics module for a topic's affected-stream CODES to classify cross-stream edges — without reading
// Topics' tables. Implemented in Topics.Infrastructure against the Topics DbContext (mirrors ITopicScheduler).
// Speaks primitives (lowercase stream codes) — the design's localized stream NAME lives in Membership and is
// deliberately NOT resolved here (that would be a third seam; the node badge shows the code). Only Topic
// carries streams (Topic.AffectedStreams); other artifact types have none, so cross-stream is Topic-scope
// only (OQ-047 tracks the inherit-from-topic model; default is this Topic-only scope).
public interface ITopicStreamReader
{
    // Returns the topic's affected-stream codes, or an empty list when the topic is unknown.
    Task<IReadOnlyList<string>> GetStreamsAsync(Guid topicId, CancellationToken ct = default);
}
