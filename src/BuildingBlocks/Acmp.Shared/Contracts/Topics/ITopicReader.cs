namespace Acmp.Shared.Contracts.Topics;

// Cross-module read seam (ADR-0001, P15c / FR-115): another module reads a topic's key + title snapshot —
// e.g. the Research module recording a Topic→Mission traceability edge for a mission's source topic — without
// touching the Topics module's tables. Implemented in Topics.Infrastructure over the Topics store (mirrors
// ITopicStreamReader). Primitive-only; the Topics enums never leak.
public sealed record TopicSummary(Guid Id, string Key, string Title);

/// <summary>One attachment on a topic, as a guest presenter sees it (FR-159). No storage key — the
/// object name is Topics' business and must not travel.</summary>
public sealed record TopicMaterial(Guid Id, string FileName, string ContentType, long SizeBytes);

/// <summary>A topic as the /session page renders it: the summary card plus its materials.</summary>
public sealed record TopicBrief(
    Guid Id, string Key, string Title, string Summary, IReadOnlyList<TopicMaterial> Materials);

public interface ITopicReader
{
    // Returns null when the topic does not exist (the caller treats a missing source topic as "no edge").
    Task<TopicSummary?> GetSummaryAsync(Guid topicId, CancellationToken ct = default);

    /// <summary>The topic card and materials list for /session (FR-159). Null when the topic is gone.</summary>
    /// <remarks>
    /// A SEPARATE METHOD rather than a widened TopicSummary: that record is already consumed by the
    /// Research conversion path and by a test double, and reshaping it would break both to serve a
    /// caller that wants different fields.
    /// </remarks>
    Task<TopicBrief?> GetBriefAsync(Guid topicId, CancellationToken ct = default);

    /// <summary>
    /// A short-lived pre-signed URL for one attachment, SCOPED TO THE TOPIC (FR-159 / NFR-027).
    /// </summary>
    /// <remarks>
    /// ⚠ THE TOPIC ID IS PART OF THE QUESTION, NOT CONTEXT. It returns null when the attachment is not
    /// on that topic, so a guest holding one slot cannot fetch another topic's file by guessing an
    /// attachment id. Fail-closed: an unknown attachment and a mismatched one are the same answer.
    /// </remarks>
    Task<string?> GetMaterialUrlAsync(Guid topicId, Guid attachmentId, CancellationToken ct = default);
}
