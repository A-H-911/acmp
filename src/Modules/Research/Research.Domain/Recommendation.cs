using Acmp.Modules.Research.Domain.Enums;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Research.Domain;

// A proposed course of action arising from a research mission (FR-113). Owned child of the ResearchMission
// aggregate (reached only through it): a BaseEntity identity, a per-mission display key (REC-###), a bilingual
// statement + optional rationale, a priority band, its own Proposed→Accepted|Rejected disposition, and an
// optional LinkedTopicId (a soft value ref stored ONLY — the graph edge is P15c, ADR-0001, no FK). Mutation is
// driven by the ResearchMission aggregate so its invariants hold.
public sealed class Recommendation : BaseEntity
{
    private Recommendation() { }

    public string Key { get; private set; } = string.Empty;   // REC-### (per mission)
    public LocalizedString Statement { get; private set; } = null!;
    public LocalizedString? Rationale { get; private set; }
    public RecommendationPriority Priority { get; private set; }
    public RecommendationStatus Status { get; private set; }
    public Guid? LinkedTopicId { get; private set; }          // stored only — no edge (P15c)

    internal static Recommendation Create(string key, LocalizedString statement, LocalizedString? rationale,
        RecommendationPriority priority, Guid? linkedTopicId)
    {
        if (statement is null) throw new InvalidOperationException("A recommendation statement is required.");
        if (!Enum.IsDefined(priority)) throw new InvalidOperationException("A valid priority is required.");
        return new Recommendation
        {
            Key = key,
            Statement = statement,
            Rationale = rationale,
            Priority = priority,
            Status = RecommendationStatus.Proposed,
            LinkedTopicId = linkedTopicId,
        };
    }

    internal void Update(LocalizedString statement, LocalizedString? rationale, RecommendationPriority priority,
        Guid? linkedTopicId)
    {
        Statement = statement ?? throw new InvalidOperationException("A recommendation statement is required.");
        if (!Enum.IsDefined(priority)) throw new InvalidOperationException("A valid priority is required.");
        Rationale = rationale;
        Priority = priority;
        LinkedTopicId = linkedTopicId;
    }

    // Accept or reject a still-open recommendation. Terminal dispositions are set once — a decided
    // recommendation does not flip. Convert (→ Topic/Action) is the P15c flow, not modelled here.
    internal void SetStatus(RecommendationStatus status)
    {
        if (status is not (RecommendationStatus.Accepted or RecommendationStatus.Rejected))
            throw new InvalidOperationException("A recommendation can only be Accepted or Rejected.");
        if (Status != RecommendationStatus.Proposed)
            throw new InvalidOperationException($"This recommendation is already {Status}.");
        Status = status;
    }

    // P15c / W16: record that this recommendation has been converted into an execution Topic (TOP-). A display
    // disposition set once, only from Accepted — the authoritative one-per-recommendation guard is the Topics
    // side (Topic.SourceRecommendationId's filtered unique index + the Informs edge). LinkedTopicId records the
    // successor for the mission detail's "Converted → TOP-" chip.
    internal void MarkConverted(Guid topicId)
    {
        if (topicId == Guid.Empty) throw new InvalidOperationException("The successor topic id is required.");
        if (Status != RecommendationStatus.Accepted)
            throw new InvalidOperationException("Only an accepted recommendation can be converted.");
        Status = RecommendationStatus.Converted;
        LinkedTopicId = topicId;
    }
}
