using Acmp.Modules.Actions.Domain.Enums;
using Acmp.Modules.Actions.Domain.Events;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Actions.Domain;

// The Action aggregate root (docs/domain/domain-model.md §Action, docs/domain/entity-lifecycles.md §7; workflows W13/W14/W22) — a concrete, owned
// follow-up task arising from a decision/condition/meeting/topic/risk. Named ActionItem, not Action, to
// avoid an ambiguous-reference clash with System.Action (the BCL delegate) in the many files that import
// this namespace alongside System.
//
// Identity to other modules is by value only: (SourceType, SourceId = source PublicId) + display-key
// snapshots — never an EF navigation (ADR-0001). The people fields (Owner/CompletedBy/VerifiedBy) are
// Keycloak subjects (the ICurrentUser.UserId space, like Decision.ChairApprovedByUserId), so the SoD-1
// verifier ≠ owner/completer guard is a direct string comparison; a display-name snapshot rides alongside
// for the UI. (docs/domain/domain-model.md types the owner as a member id; we store the sub for a self-contained SoD check —
// flagged in the progress log.)
//
// Overdue is DERIVED (IsOverdue), never persisted (docs/domain/entity-lifecycles.md line 159). Verification stamps are write-once
// (AC-013). The SoD-1 verifier gate (AC-012/013) is enforced — and its denial audited — by the verify
// handler (SegregationOfDuties.CanVerifyAction); the domain keeps the transition guards.
public sealed class ActionItem : AuditableEntity
{
    private ActionItem() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → 409 (ADR-0018).
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty;          // ACT-YYYY-###
    public LocalizedString Title { get; private set; } = null!;
    public LocalizedString? Description { get; private set; }
    public ActionStatus Status { get; private set; }
    public ActionPriority Priority { get; private set; }

    public string OwnerUserId { get; private set; } = string.Empty;  // Keycloak sub
    public string OwnerName { get; private set; } = string.Empty;    // display snapshot
    public DateTimeOffset? DueDate { get; private set; }
    public int ProgressPct { get; private set; }

    // Source link (W13): the originating artifact, plus display snapshots for the register's "Linked"
    // column and the detail's "Raised in meeting" line — no cross-module read (ADR-0001).
    public ActionSourceType SourceType { get; private set; }
    public Guid SourceId { get; private set; }
    public string? SourceKey { get; private set; }   // e.g. DECN-2026-008 — snapshot for the Linked column
    public string? MeetingKey { get; private set; }  // e.g. MTG-2026-018 — "raised in meeting" snapshot

    public LocalizedString? BlockedReason { get; private set; }
    public LocalizedString? CompletionNote { get; private set; }     // "evidence noted" on completion (W14)
    public LocalizedString? CancelReason { get; private set; }

    public string? CompletedByUserId { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? VerifiedByUserId { get; private set; }
    public string? VerifiedByName { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }

    // Reminder/escalation bookkeeping (P8c, W22): the Hangfire sweep stamps these so it stays idempotent
    // between runs — they are NOT lifecycle transitions (no domain event), just the "already told them"
    // record. Nullable = not-yet-sent; OverdueNotifiedAt is refreshed each send under DailyWhileOverdue.
    public DateTimeOffset? DueReminderSentAt { get; private set; }
    public DateTimeOffset? OverdueNotifiedAt { get; private set; }
    public DateTimeOffset? EscalatedToSecretaryAt { get; private set; }
    public DateTimeOffset? EscalatedToChairmanAt { get; private set; }

    public void MarkDueReminderSent(DateTimeOffset now) => DueReminderSentAt = now;
    public void MarkOverdueNotified(DateTimeOffset now) => OverdueNotifiedAt = now;
    public void MarkEscalatedToSecretary(DateTimeOffset now) => EscalatedToSecretaryAt = now;
    public void MarkEscalatedToChairman(DateTimeOffset now) => EscalatedToChairmanAt = now;

    // Derived overdue overlay (docs/domain/entity-lifecycles.md line 159): past due while work is still open. Never persisted.
    public bool IsOverdue(DateTimeOffset now) =>
        DueDate is { } due && due < now &&
        Status is ActionStatus.Open or ActionStatus.InProgress or ActionStatus.Blocked;

    // W13: create an Open action against a source. Owner + title + source required; progress starts at 0.
    public static ActionItem Create(string key, LocalizedString title, LocalizedString? description,
        ActionPriority priority, string ownerUserId, string ownerName, DateTimeOffset? dueDate,
        ActionSourceType sourceType, Guid sourceId, string? sourceKey, string? meetingKey, DateTimeOffset now)
    {
        if (title is null) throw new InvalidOperationException("An action title is required.");
        if (string.IsNullOrWhiteSpace(ownerUserId)) throw new InvalidOperationException("An action owner is required.");
        if (sourceId == Guid.Empty) throw new InvalidOperationException("An action must reference a source artifact.");

        var action = new ActionItem
        {
            Key = key.Trim(),
            Title = title,
            Description = description,
            Priority = priority,
            Status = ActionStatus.Open,
            OwnerUserId = ownerUserId,
            OwnerName = (ownerName ?? string.Empty).Trim(),
            DueDate = dueDate,
            ProgressPct = 0,
            SourceType = sourceType,
            SourceId = sourceId,
            SourceKey = sourceKey?.Trim(),
            MeetingKey = meetingKey?.Trim(),
        };
        action.Raise(new ActionCreatedEvent(action.PublicId, action.Key, ownerUserId, now));
        return action;
    }

    // W14: start work. Open → InProgress.
    public void Start(DateTimeOffset now)
    {
        RequireStatus(ActionStatus.Open);
        Status = ActionStatus.InProgress;
        Raise(new ActionStartedEvent(PublicId, Key, now));
    }

    // W14: block on an impediment (reason required). InProgress → Blocked.
    public void Block(LocalizedString reason, DateTimeOffset now)
    {
        RequireStatus(ActionStatus.InProgress);
        BlockedReason = reason ?? throw new InvalidOperationException("A blocking reason is required.");
        Status = ActionStatus.Blocked;
        Raise(new ActionBlockedEvent(PublicId, Key, now));
    }

    // W14: unblock. Blocked → InProgress (the reason is retained as history).
    public void Unblock(DateTimeOffset now)
    {
        RequireStatus(ActionStatus.Blocked);
        Status = ActionStatus.InProgress;
        Raise(new ActionUnblockedEvent(PublicId, Key, now));
    }

    // W14: record progress (0–100) while the action is live. Not allowed once completed/terminal.
    public void UpdateProgress(int pct)
    {
        RequireStatus(ActionStatus.Open, ActionStatus.InProgress, ActionStatus.Blocked);
        if (pct is < 0 or > 100) throw new InvalidOperationException("Progress must be between 0 and 100.");
        ProgressPct = pct;
    }

    // W14: mark complete with optional evidence. InProgress → Completed; progress is 100 by definition.
    public void Complete(LocalizedString? completionNote, string completedByUserId, DateTimeOffset now)
    {
        RequireStatus(ActionStatus.InProgress);
        if (string.IsNullOrWhiteSpace(completedByUserId)) throw new InvalidOperationException("The completer is required.");
        Status = ActionStatus.Completed;
        ProgressPct = 100;
        CompletionNote = completionNote;
        CompletedByUserId = completedByUserId;
        CompletedAt = now;
        Raise(new ActionCompletedEvent(PublicId, Key, now));
    }

    // W14: verify completion. Completed → Verified; the verifier is stamped write-once. The SoD-1 gate
    // (verifier ≠ owner/completer) and its audited denial live in the verify handler — the domain keeps
    // only the state guard so a raw domain call stays free of an authorization dependency.
    public void Verify(string verifierUserId, string verifierName, DateTimeOffset now)
    {
        RequireStatus(ActionStatus.Completed);
        if (string.IsNullOrWhiteSpace(verifierUserId)) throw new InvalidOperationException("A verifier is required.");
        Status = ActionStatus.Verified;
        VerifiedByUserId = verifierUserId;
        VerifiedByName = (verifierName ?? string.Empty).Trim();
        VerifiedAt = now;
        Raise(new ActionVerifiedEvent(PublicId, Key, now));
    }

    // W14: cancel with a reason. Any non-terminal state → Cancelled (terminal; a re-open is a new action).
    public void Cancel(LocalizedString reason, DateTimeOffset now)
    {
        RequireStatus(ActionStatus.Open, ActionStatus.InProgress, ActionStatus.Blocked, ActionStatus.Completed);
        CancelReason = reason ?? throw new InvalidOperationException("A cancellation reason is required.");
        Status = ActionStatus.Cancelled;
        Raise(new ActionCancelledEvent(PublicId, Key, now));
    }

    private void RequireStatus(params ActionStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"This operation is not allowed while the action is {Status}.");
    }
}
