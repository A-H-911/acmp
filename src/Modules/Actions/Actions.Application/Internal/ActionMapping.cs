using Acmp.Modules.Actions.Application.Contracts;
using Acmp.Modules.Actions.Domain;

namespace Acmp.Modules.Actions.Application.Internal;

// Aggregate → read-model projection, shared by the queries and the command return values. IsOverdue is
// derived against the supplied clock (never a stored column).
internal static class ActionMapping
{
    public static ActionSummaryDto ToSummary(ActionItem a, DateTimeOffset now) => new(
        a.PublicId, a.Key, a.Title, a.Status.ToString(), a.Priority.ToString(),
        a.OwnerUserId, a.OwnerName, a.DueDate, a.IsOverdue(now), a.ProgressPct,
        a.SourceType.ToString(), a.SourceId, a.SourceKey, a.MeetingKey);

    public static ActionDetailDto ToDetail(ActionItem a, DateTimeOffset now) => new(
        a.PublicId, a.Key, a.Title, a.Description, a.Status.ToString(), a.Priority.ToString(),
        a.OwnerUserId, a.OwnerName, a.DueDate, a.IsOverdue(now), a.ProgressPct,
        a.SourceType.ToString(), a.SourceId, a.SourceKey, a.MeetingKey,
        a.BlockedReason, a.CompletionNote, a.CancelReason,
        a.CompletedByUserId, a.CompletedAt, a.VerifiedByUserId, a.VerifiedByName, a.VerifiedAt, a.CreatedAt);
}
