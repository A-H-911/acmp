using Acmp.Shared.Authorization.Abac;
using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Membership.Domain;

// A per-topic relationship (Owner/Assignee/Presenter) granted to a member on a topic (docs/domain/permission-role-matrix.md §D).
// References the topic by id only — no FK into the Topics module (module boundary, ADR-0001).
// Presenter grants are meeting-scoped and time-boxed via ValidFrom/ValidTo (docs/domain/permission-role-matrix.md §D, §E.3).
public sealed class TopicCapabilityGrant : AuditableEntity
{
    private TopicCapabilityGrant() { }

    public long CommitteeMemberId { get; private set; }
    public Guid TopicId { get; private set; }
    public TopicCapabilityType Capability { get; private set; }
    public Guid? MeetingId { get; private set; }
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }

    public static TopicCapabilityGrant Grant(
        long memberId, Guid topicId, TopicCapabilityType capability,
        Guid? meetingId = null, DateTimeOffset? from = null, DateTimeOffset? to = null) =>
        new()
        {
            CommitteeMemberId = memberId,
            TopicId = topicId,
            Capability = capability,
            MeetingId = meetingId,
            ValidFrom = from,
            ValidTo = to,
        };

    public bool IsActiveAt(DateTimeOffset now) =>
        (ValidFrom is null || ValidFrom <= now) && (ValidTo is null || now <= ValidTo);
}
