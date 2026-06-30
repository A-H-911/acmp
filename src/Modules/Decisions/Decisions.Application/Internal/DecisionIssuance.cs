using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Contracts.Topics;

namespace Acmp.Modules.Decisions.Application.Internal;

// Shared post-issue side-effects for both IssueDecision (W12) and SupersedeDecision (W21): mark the topic
// Decided via the cross-module seam (idempotent), fan out the in-app notification, and emit the audit
// signal. Factored here so the supersession path — whose successor IS an issued decision — produces the
// exact same side-effects as a first-time issue (DRY; the seam's idempotency makes it safe to call again).
internal static class DecisionIssuance
{
    public static async Task ApplyAsync(
        ITopicDecisionRecorder topics, ICommitteeDirectory directory, INotificationChannel notifications,
        IAuditSink audit, string? actorSub, Guid topicId, string decisionKey, bool chairOverride, CancellationToken ct)
    {
        await topics.MarkDecidedAsync(topicId, ct);
        await DecisionNotifications.FanOutAsync(directory, notifications,
            DecisionNotifications.DecisionIssued(decisionKey), ct);
        await audit.EmitAsync("Decisions.DecisionIssued", actorSub, new { decisionKey, topicId, chairOverride }, ct);
    }
}
