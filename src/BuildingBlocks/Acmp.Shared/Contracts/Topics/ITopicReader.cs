namespace Acmp.Shared.Contracts.Topics;

// Cross-module read seam (ADR-0001, P15c / FR-115): another module reads a topic's key + title snapshot —
// e.g. the Research module recording a Topic→Mission traceability edge for a mission's source topic — without
// touching the Topics module's tables. Implemented in Topics.Infrastructure over the Topics store (mirrors
// ITopicStreamReader). Primitive-only; the Topics enums never leak.
public sealed record TopicSummary(Guid Id, string Key, string Title);

public interface ITopicReader
{
    // Returns null when the topic does not exist (the caller treats a missing source topic as "no edge").
    Task<TopicSummary?> GetSummaryAsync(Guid topicId, CancellationToken ct = default);
}
