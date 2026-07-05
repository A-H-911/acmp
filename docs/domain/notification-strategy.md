# 29 — Notification Strategy (Deliverable 37)

**Purpose:** Define the ACMP notification architecture, channel abstraction, v1 in-app catalog, Phase 2 Webex adapter, notification preferences, reliability guarantees, and security constraints — implementing ADR-0005.

> Canonical decision (ADR-0005, README §A): **v1 channel = in-app notification center only. No email in v1. Webex adapter = Phase 2. Email = later when SMTP relay available.** Self-contained (CON-001): no org notification platform.

---

## 1. Channel Abstraction

### 1.1 `INotificationChannel` Interface

All notification delivery is mediated through a single abstraction. Channels are pluggable; adding a new channel does not change the notification dispatch logic.

```csharp
// Notification.Infrastructure (Platform module)
public interface INotificationChannel
{
    /// <summary>Channel identifier, e.g. "InApp", "Webex", "Email".</summary>
    string ChannelId { get; }

    /// <summary>Phase this channel is active from.</summary>
    DeliveryPhase Phase { get; }

    /// <summary>
    /// Deliver a notification. Returns a DeliveryResult indicating success,
    /// failure, or retry-required (e.g. 429 rate-limit).
    /// </summary>
    Task<DeliveryResult> SendAsync(NotificationPayload payload, CancellationToken ct);
}

public record NotificationPayload(
    string EventType,        // maps to catalog EventType below
    Guid RecipientUserId,
    LocalizedString Title,
    LocalizedString Body,
    string? DeepLinkUrl,     // relative route, e.g. "/topics/TOP-2026-042"
    NotificationUrgency Urgency,
    Dictionary<string, string> Metadata // event-specific key/value pairs
);

public record DeliveryResult(bool Success, bool ShouldRetry, string? ErrorCode, DateTimeOffset? RetryAfter);
```

### 1.2 Channel Registry

The `NotificationDispatcher` (application service) resolves active channels from DI at startup. Only channels registered for the current phase are instantiated.

```
Phase 1: [InAppChannel]
Phase 2: [InAppChannel, WebexChannel]
Phase 3+: [InAppChannel, WebexChannel, EmailChannel (when SMTP available)]
```

Channel selection per event: the `NotificationPreference` model (§4) determines which channels a given user wants for a given event type. The dispatcher queries preferences before dispatching to each channel.

### 1.3 Channel Implementations

#### v1 — InAppChannel

- Writes a `Notification` record to the ACMP SQL database.
- The React frontend polls `/api/v1/notifications/unread` at a configurable interval (default 30s) `[unverified: polling vs WebSocket TBD — polling is sufficient at ≤20 users]` and updates the notification bell badge.
- The notification center UI is a slide-over panel listing notifications newest-first; each is clickable (follows `DeepLinkUrl`).
- Notifications have read/unread state; bulk "Mark all read" action available.
- Notification records are retained per records management policy (not deleted on read).

#### Phase 2 — WebexChannel

Per research findings §5.3 and ADR-0005:

- Sends messages to a designated Webex space (committee notifications space) or direct message to user's Webex account, using bot token.
- Payload format: **Microsoft Adaptive Cards v1.3** (Webex-supported). Card JSON + images must stay **≤80 KB total, ≤10 image links**.
- Rate-limit handling: on HTTP 429, read `Retry-After` header; `DeliveryResult(Success: false, ShouldRetry: true, RetryAfter: ...)` triggers Hangfire retry with exponential backoff up to 3 attempts. Dead-letter to admin alert after exhaustion.
- Webex bot is a separate registered Webex integration (OAuth bot token); stored in ACMP secrets vault (not hardcoded).
- Card content must **not include** sensitive decision content — card contains title, urgency, and a deep link back to ACMP only (security constraint, §6).
- Implementation: `WebexChannel : INotificationChannel` wraps a `WebexApiClient` with typed retry policy (Polly). Never hard-depends on Webex: if Webex is unavailable, in-app channel is the fallback; no governance event is lost.

#### Future — EmailChannel

Deferred until an SMTP relay is made available. When implemented, uses a `SmtpEmailChannel : INotificationChannel` backed by a configured SMTP relay. Email is lowest urgency channel; digests only (not real-time alerts). Not scoped for v1 or Phase 2.

---

## 2. Outbox Pattern (Reliability)

All notifications flow through a **transactional outbox** to guarantee delivery even under application restart, network failure, or channel unavailability.

```
Event raised in domain/application layer
    → write NotificationOutboxItem to DB (same transaction as the triggering operation)
    → Hangfire background job reads OutboxItems (status=Pending) on a short polling interval
    → dispatches via INotificationChannel → updates OutboxItem.Status (Sent | Failed | DeadLettered)
```

- `NotificationOutboxItem` schema: `Id · EventType · RecipientUserId · Payload (JSON) · ChannelId · Status · AttemptCount · LastAttemptAt · DeadLetteredAt · CreatedAt`.
- Max retry attempts: 3 per channel (configurable). After exhaustion: status = `DeadLettered`; admin can view and manually retry via Admin → Job Monitor.
- Deduplication key: `(EventType + SubjectId + RecipientUserId + ChannelId + DateHourBucket)` — prevents duplicate notifications from rapid re-queue within the same hour for the same event+recipient+channel.
- Outbox polling interval: 15 seconds `[unverified]`.

---

## 3. Notification Catalog

> Each entry defines: **Event | Trigger condition | Recipients (role/relationship) | Channel (phase) | Template summary | Urgency | Mandatory/Opt-outable**.

### 3.1 Meeting Lifecycle

| Event | Trigger | Recipients | Channel | Template | Urgency | Opt-out? |
|---|---|---|---|---|---|---|
| **MeetingScheduled** | `Meeting.Status` transitions to `Scheduled` | All active committee members | InApp (v1) · Webex (Ph2) | "Meeting {MTG-ID} scheduled for {date} {time}. View agenda." + deep link | Normal | No (mandatory) |
| **MeetingUpdated** | Meeting date/time/location changed | All invited participants | InApp (v1) · Webex (Ph2) | "Meeting {MTG-ID} updated: {changed fields}. View updated details." | High | No |
| **AgendaPublished** | `Agenda.Status` transitions to `Published` | All committee members | InApp (v1) · Webex (Ph2) | "Agenda for {MTG-ID} ({date}) is published. Review {N} items." + deep link | Normal | Yes |
| **AgendaUpdated** | Agenda modified after publication | All committee members | InApp (v1) | "Agenda for {MTG-ID} updated. Item added/removed/reordered." | Low | Yes |
| **MeetingStarting** | 30 min before scheduled start (Hangfire job) | All committee members | InApp (v1) | "Meeting {MTG-ID} starts in 30 minutes." | High | Yes |
| **MeetingMinutesPublished** | `MinutesOfMeeting.Status` transitions to `Published` | All committee members | InApp (v1) · Webex (Ph2) | "Minutes for {MTG-ID} ({date}) are published. Review actions and decisions." | Normal | Yes |

---

### 3.2 Topic & Backlog

| Event | Trigger | Recipients | Channel | Template | Urgency | Opt-out? |
|---|---|---|---|---|---|---|
| **TopicSubmitted** | `Topic.Status` transitions to `Submitted` | Secretary | InApp (v1) | "New topic submitted: {title} by {submitter}. Pending triage." | Normal | No |
| **TopicAssigned** | `Topic.OwnerId` set or changed | Assigned owner | InApp (v1) | "You have been assigned as owner of {TOP-ID}: {title}." | Normal | No |
| **TopicScheduled** | Topic added to meeting agenda | Topic owner + assignees | InApp (v1) | "Topic {TOP-ID} scheduled for {MTG-ID} on {date}." | Normal | Yes |
| **TopicStatusChanged** | Topic status changes (any) | Topic owner | InApp (v1) | "Topic {TOP-ID} status changed: {OldStatus} → {NewStatus}." | Low | Yes |
| **TopicDeferred** | Decision outcome = Deferred | Topic owner + Secretary | InApp (v1) | "Topic {TOP-ID} deferred. Review decision rationale." | Normal | No |
| **TopicRejected** | Decision outcome = Rejected | Topic owner | InApp (v1) | "Topic {TOP-ID} rejected. Reason: {summary}." | Normal | No |

---

### 3.3 Voting

| Event | Trigger | Recipients | Channel | Template | Urgency | Opt-out? |
|---|---|---|---|---|---|---|
| **VoteOpened** | `Vote.Status` transitions to `Open` | All eligible voters (from `Vote.EligibleVoterIds`) | InApp (v1) · Webex (Ph2) | "Vote opened on {DECN-ID}: {title}. Closes {datetime}. Cast your vote." | High | No (mandatory) |
| **VoteClosing24h** | 24h before `Vote.ClosingAt` (Hangfire job); only if voter has not yet voted | Non-voting eligible members | InApp (v1) · Webex (Ph2) | "Reminder: Vote on {DECN-ID} closes in 24 hours. You have not yet voted." | High | Yes |
| **VoteClosed** | `Vote.Status` transitions to `Closed` | Chairman + Secretary | InApp (v1) | "Vote on {DECN-ID} closed. Results: {summary}. Pending chairman ratification." | Normal | No |
| **DecisionPublished** | `Decision.Status` transitions to `Issued` (after chairman ratification) | All committee members | InApp (v1) · Webex (Ph2) | "Decision {DECN-ID} published: {title} — {outcome}." + deep link | Normal | Yes |
| **ChairmanApprovalRequired** | Vote closed; awaiting chairman final ratification | Chairman | InApp (v1) | "Decision {DECN-ID} requires your ratification. Voting summary available." | High | No |

---

### 3.4 Actions

| Event | Trigger | Recipients | Channel | Template | Urgency | Opt-out? |
|---|---|---|---|---|---|---|
| **ActionAssigned** | `Action.AssigneeId` set | Assignee | InApp (v1) | "Action {ACT-ID} assigned to you: {title}. Due: {date}." | Normal | No |
| **ActionDueReminder** | 3 days before `Action.DueDate` (Hangfire job) | Assignee | InApp (v1) | "Action {ACT-ID} due in 3 days: {title}." | Normal | Yes |
| **ActionOverdue** | `Action.DueDate < today` AND not completed (daily Hangfire job) | Assignee | InApp (v1) | "Action {ACT-ID} is overdue by {N} days: {title}." | High | No |
| **ActionOverdueEscalation** | Action overdue by >7 days (configurable threshold) | Assignee + Secretary (+ Chairman if >14d) | InApp (v1) | "ESCALATION: Action {ACT-ID} is {N} days overdue. Secretary notified." | Critical | No |
| **ActionCompleted** | `Action.Status` transitions to `Completed` | Secretary + topic owner | InApp (v1) | "Action {ACT-ID} marked complete by {user}. Pending verification." | Low | Yes |
| **ActionBlocked** | `Action.Status` transitions to `Blocked` | Secretary + topic owner | InApp (v1) | "Action {ACT-ID} marked blocked. Reason: {summary}." | Normal | No |

---

### 3.5 Risk

| Event | Trigger | Recipients | Channel | Template | Urgency | Opt-out? |
|---|---|---|---|---|---|---|
| **RiskEscalated** | `Risk.Severity` escalated to Critical OR `Risk.Status` transitions to `Escalated` | Chairman + Secretary | InApp (v1) · Webex (Ph2) | "RISK ESCALATION: {RSK-ID} {title} escalated to {severity}. Immediate attention required." | Critical | No |
| **RiskOpened** | New `Risk` created | Secretary + Topic owner | InApp (v1) | "New risk logged: {RSK-ID} {title} (severity: {sev}) on topic {TOP-ID}." | Normal | Yes |

---

### 3.6 Reviews, Approvals & Governance

| Event | Trigger | Recipients | Channel | Template | Urgency | Opt-out? |
|---|---|---|---|---|---|---|
| **ADRReviewDue** | ADR.ApprovedAt > 1 year ago (configurable) AND `ADR.Status = Approved` (monthly Hangfire job) | Secretary + ADR author | InApp (v1) | "ADR {ADR-ID}: {title} is due for annual review." | Normal | Yes |
| **ADRProposed** | `ADR.Status` transitions to `Proposed` | Chairman + all Members (committee-wide ADR) | InApp (v1) | "ADR {ADR-ID} proposed: {title}. Review and comment before committee session." | Normal | Yes |
| **InvariantViolationRecorded** | New `InvariantViolation` created | Chairman + Secretary | InApp (v1) | "Invariant violation recorded for {AIV-ID}: {title}. Review required." | High | No |
| **MoMPendingApproval** | `MinutesOfMeeting.Status` transitions to `PendingApproval` | Chairman + Secretary | InApp (v1) | "Minutes for {MTG-ID} ready for your approval." | Normal | No |
| **ResearchMissionOverdue** | `ResearchMission.TargetDate < today` AND not complete | Mission owner + Secretary | InApp (v1) | "Research mission {RMS-ID} is overdue: {title}." | Normal | Yes |

---

### 3.7 Digests (Batched)

| Event | Trigger | Recipients | Channel | Content | Opt-out? |
|---|---|---|---|---|---|
| **DailyDigest** | Hangfire CRON: 07:30 daily (if user has unread notifications) | All active users | InApp (v1) | Summary: unread count, overdue actions, open votes, today's meetings. Does NOT repeat individual events already sent. | Yes |
| **WeeklyDigest** | Hangfire CRON: Monday 08:00 | Chairman, Secretary | InApp (v1) | Week summary: decisions made, actions due this week, upcoming meeting prep status, KPI snapshot (DB-01). Attached PDF report stored in MinIO. | Yes |

Digest deduplication: the digest job reads the notification outbox for each user and summarizes; it does not re-emit individual notification records that were already sent. Digest is a new single `Notification` record with `IsDigest=true` and a structured payload.

---

## 4. Notification Preference Model

### 4.1 Schema

```
NotificationPreference {
    UserId:Guid
    EventType:string          // matches EventType column in catalog
    ChannelId:string          // "InApp", "Webex", etc.
    IsEnabled:bool            // user opt-in/out
    UpdatedAt:DateTimeOffset
}
```

Preferences are per-user per-event per-channel. Defaults are seeded at user creation based on role (e.g., Chairman gets all mandatory + all High/Critical events defaulted ON).

### 4.2 Override Rules

| Rule | Behaviour |
|---|---|
| **Mandatory events** (`Opt-out? = No`) | `IsEnabled` is forced `true`; preference record exists but is read-only in the UI. The preference still exists in the DB so the dispatcher can query it uniformly. |
| **Opt-outable events** | User can disable per-event per-channel in their profile preferences page. |
| **Channel not yet active** | Preferences for Phase 2+ channels (Webex, Email) are stored but ignored by the dispatcher until that channel is registered. This allows pre-configuring preferences before Phase 2 ships. |
| **User deactivated** | Dispatcher skips delivery for `User.Status ≠ Active`. Outbox item is marked `Skipped`. |

### 4.3 Preference UI

Located at: `/profile/preferences → Notification Settings`

Rendered as a grouped table: event category (Meeting, Voting, Actions, etc.) × channel (InApp only in v1). Rows with `Mandatory` are shown but the toggle is disabled with a tooltip explaining why.

---

## 5. Routing Rules

```
Event raised
    │
    ▼
RecipientResolver: determine recipient UserIds from event context
    (e.g. VoteOpened → Vote.EligibleVoterIds;
     ActionAssigned → Action.AssigneeId;
     MeetingScheduled → CommitteeMembership.ActiveMemberIds)
    │
    ▼
PreferenceFilter: for each (UserId, ChannelId) — is IsEnabled=true?
    │
    ▼
OutboxWriter: write one OutboxItem per (UserId, ChannelId) in same DB transaction
    │
    ▼
[Hangfire picks up OutboxItem]
    │
    ▼
INotificationChannel.SendAsync(payload)
    │
    ├─ Success → OutboxItem.Status = Sent
    ├─ RetryRequired (429, timeout) → Hangfire retry with backoff
    └─ DeadLettered → OutboxItem.Status = DeadLettered; admin alert
```

### 5.1 RecipientResolver Patterns

| Pattern | Events |
|---|---|
| **Individual** (one user by relationship) | ActionAssigned, TopicAssigned, ActionOverdue |
| **Role-broadcast** (all members of a global role) | MeetingScheduled, AgendaPublished, DecisionPublished |
| **Eligible-voter-set** | VoteOpened, VoteClosing24h |
| **Topic-scoped** (owner + assignees) | TopicStatusChanged, TopicDeferred, TopicScheduled |
| **Chairman-only** | ChairmanApprovalRequired, VoteClosed, RiskEscalated |
| **Secretary-only** | TopicSubmitted, MoMPendingApproval |

---

## 6. Security & Content Constraints

The following rules apply to all notification content regardless of channel:

1. **No sensitive decision content in notifications.** Notification body contains only: event type, artifact ID + title, urgency, and a deep link to ACMP. The actual decision outcome, voting breakdown, or risk detail is never embedded in the notification payload. The recipient must navigate to ACMP to see sensitive content. This ensures that if a notification channel is compromised, no sensitive governance data is exposed.
2. **Deep links are relative internal routes, never absolute URLs constructed from user input.** Prevents open-redirect injection. The dispatcher constructs links from known route templates + artifact ID only.
3. **No PII in Webex card content.** Webex card bodies must not contain email addresses, user identifiers, or personal data beyond display names where strictly necessary.
4. **Access check on delivery.** The notification deep link loads the artifact detail page; the page enforces its own RBAC. A notification does not bypass access control — if a user's permissions change between notification send and page load, they will be denied access on the page.
5. **Recipient validation.** The `RecipientResolver` resolves only active users (`User.Status = Active`). Deactivated users never receive notifications; their pending outbox items are skipped.
6. **Rate-limit awareness (Webex Phase 2).** The `WebexChannel` implements token-bucket rate limiting client-side to pre-empt 429s, and backs off exponentially on receipt. It never retries more than 3 times; dead-lettered items surface in Admin → Job Monitor.

---

## 7. Operational Concerns

### 7.1 Notification Center UI Spec (v1)

| Element | Behaviour |
|---|---|
| Bell icon + unread badge | In persistent top nav; badge shows unread count (capped at "99+") |
| Notification panel (slide-over) | Newest-first; infinite scroll; grouped by date (Today / Yesterday / Older) |
| Each notification card | Icon (by event category) · Title · Body (≤2 lines) · Timestamp · Read/Unread indicator · Deep-link click-through |
| Mark read | Auto-mark-read on click; "Mark all read" button at top |
| Filter | Filter by: All / Unread / High+Critical urgency |
| Empty state | "You're all caught up." |
| Localization | Full EN/AR; RTL panel direction mirrors correctly |

### 7.2 Admin Observability

- **Job Monitor** (Hangfire dashboard, Admin area): shows outbox job queue, retry counts, dead-lettered items.
- **Dead-letter queue**: Admin can view failed notification details + manually trigger retry.
- **Serilog** logs every `SendAsync` call result at Information level; failures at Warning; dead-letters at Error. Shipped to self-hosted Seq.

### 7.3 Phase Delivery Summary

| Phase | Channels active | New events available |
|---|---|---|
| **Phase 1** | InApp only | All catalog events via InApp; full preference model; outbox reliability; Hangfire jobs |
| **Phase 2** | InApp + Webex | Webex adapter for High/Critical events (MeetingScheduled, VoteOpened, DecisionPublished, RiskEscalated, AgendaPublished); 429 handling; Adaptive Cards v1.3 |
| **Phase 3+** | InApp + Webex + Email (when SMTP available) | Email digest channel; per-user email preference |

---

## Traceability

| Link | Reference |
|---|---|
| ADR-0005 | Channel abstraction; in-app only v1; Webex Phase 2 |
| CON-001 | No org notification platform; ACMP bundles its own |
| Brief §6.17 | All §6.17 notification events enumerated in §3 catalog |
| Webex research | `.context/brief-digest.md §5.3` (rate limits, Adaptive Cards v1.3, 429 handling) |
| Domain model | `docs/domain/domain-model.md` — `Notification`, `User.Status`, `Vote.EligibleVoterIds` |
| Background jobs | README §A — app-owned Hangfire; outbox on ACMP's own SQL |
| Reporting dashboards | `docs/domain/reporting-dashboards.md` — scheduled report delivery via in-app notifications |
| Security | `docs/domain/security-controls.md` (deep-link safety, no sensitive content in payloads) |
