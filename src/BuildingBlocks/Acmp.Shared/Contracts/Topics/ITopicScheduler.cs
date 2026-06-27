namespace Acmp.Shared.Contracts.Topics;

// Cross-module seam (ADR-0001): the Meetings module asks the Topics module to advance a topic's
// lifecycle without ever reading Topics' tables. Implemented in Topics.Infrastructure against the
// Topics DbContext (mirrors how Membership implements ITopicCapabilityWriter for Topics). Both
// methods are idempotent — a topic already at/after the target state is left untouched — so
// re-publishing an agenda or re-starting a meeting never fails on an already-advanced topic.
public interface ITopicScheduler
{
    // W6: Prepared → Scheduled when a topic is placed on a published agenda.
    Task ScheduleAsync(Guid topicId, Guid meetingId, CancellationToken ct = default);

    // W7: Scheduled → InCommittee when the meeting starts.
    Task EnterCommitteeAsync(Guid topicId, CancellationToken ct = default);
}
