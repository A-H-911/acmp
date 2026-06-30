namespace Acmp.Shared.Contracts.Topics;

// Cross-module seam (ADR-0001): the Decisions module asks the Topics module to advance a topic to Decided
// when a decision is issued (W12) — without ever reading Topics' tables. Implemented in
// Topics.Infrastructure against the Topics DbContext (mirrors ITopicScheduler). Idempotent: a topic that
// is not InCommittee (e.g. already Decided from a prior issue, or a decision recorded outside the live
// flow) is left untouched, so re-issuing or a supersession's successor never fails on topic state.
public interface ITopicDecisionRecorder
{
    // W12: InCommittee → Decided. No-op if the topic is not currently InCommittee.
    Task MarkDecidedAsync(Guid topicId, CancellationToken ct = default);
}
