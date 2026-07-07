# Webex Integration — Use-Case Flows & Enrichment Backlog

**Purpose:** Catalogue every Webex integration flow ACMP ships today (P13, Phase 2) and a prioritized backlog of UX-enriching additions. Companion to [webex-feasibility.md](webex-feasibility.md) (API capabilities) and [notification-strategy.md](notification-strategy.md) (channel architecture, ADR-0005). Governed by ADR-0023 (webhook HMAC + multi-channel dispatch + OAuth) and ADR-0024 (worker container).

> Webex is the one sanctioned external dependency (INV-003/CON-001) and is **off by default** (`Webex:Enabled=false` → the adapter is unregistered, in-app is the sole channel, AC-071). Delivery is **space-only**: the committee Webex space (`roomId`), never per-user email — the directory exposes no email (D-14).

---

## 1. Current flows (shipped)

### 1.1 Committee-wide event → space Adaptive Card
A governance event fans out to in-app notifications; the `WebexNotificationSink` mirrors the **committee-wide** subset to the space as one Adaptive Card (v1.3, ≤80 KB, title + one-line body + one "Open in ACMP" deep-link button, dual-language Arabic-first). Eligible categories (`WebexEligibleEvents`): **MeetingScheduled, AgendaPublished, MinutesPublished, DecisionIssued**. Subset-targeted events (VoteOpened → voters only, RiskEscalated → Chair+Sec) stay in-app to avoid over-broadcasting to the shared space (D-14). One event = one card (scoped collapse by `Category + DeepLink`). Enqueued to Hangfire → posted by the **worker** container; 429 reschedules for `Retry-After`. — AC-067.

### 1.2 Online meeting scheduled → Webex meeting auto-created
Scheduling an online meeting (`Mode != InPerson`) with Webex enabled enqueues `WebexMeetingCreateJob`, which creates a Webex meeting with the **secretary OAuth user token** (a bot cannot host), then writes `WebexMeetingId` + `JoinUrl` back to the ACMP meeting via `IMeetingWebexWriter`. Best-effort: no OAuth token → the job no-ops, the meeting persists without a Webex link (never blocks scheduling). The join URL then surfaces on the meeting view (point 1) and drives the schedule form's read-only field (point 5). — AC-072.

### 1.3 Recording ready → reference attached
Webex posts a `recordings/created` webhook to `POST /api/webex/webhook`; `WebexSignatureFilter` verifies the `x-spark-signature` HMAC (SHA1 default) over the raw body and drops replays >5 min. The worker fetches `GET /recordings/{id}` with the **OAuth host token** (recordings are host-owned; the bot token 403s) and attaches the reference idempotently. **Requires the webhook to be registered** (not retroactive; processing is delayed minutes–hours). — AC-070 (closed once auto-registration ships).

### 1.4 One-time OAuth consent
`GET /api/webex/oauth/start?key=<setup-key>` (operator-only, fail-closed) → Webex authorize → `GET /api/webex/oauth/callback` (anonymous; single-use `state` cookie; token-link audited). Access + refresh tokens are stored AES-GCM-encrypted (`webex` schema) and refreshed transparently.

---

## 2. Enrichment backlog (brainstorm)

Prioritized; each tagged with the earliest phase it's unblocked and its blocker.

| Idea | Value | When / blocker |
|------|-------|----------------|
| Meeting-start reminder card to the space (T-15 min) | Attendance nudge | **Now** — reuses the send pipeline + a scheduled job |
| Meeting update / cancel → update or delete the Webex meeting | Keeps Webex in sync with reschedules | **Now** — `PUT/DELETE /meetings/{id}` via OAuth token |
| Recording link surfaced in Minutes / Decision views | Discoverability of the record | **Now** — the reference already attaches (1.3); FE surfacing |
| Vote-open / decision-issued cards to the space | Faster committee awareness | **Now** — add categories, but confirm they're committee-wide, not subset-targeted |
| Interactive card actions (RSVP / acknowledge) | Two-way engagement | **Now-ish** — Adaptive Card `Action.Submit` + a new inbound webhook + attribution mapping |
| Per-user DM cards + attendance auto-populate | Personalized delivery, less manual roster work | **D-14** — blocked: the committee directory has no email; needs member email + identity mapping |
| Transcript → AI meeting summary | Draft minutes from the call | **Phase 3** — Webex Assistant transcripts + AI extraction (candidate-only until human-approved) |
| Webex bot slash-commands (e.g. `/acmp agenda`) | In-space quick actions | **Later** — new inbound command webhook + command router |

---

## 3. Cross-references
- API surface & limits: [webex-feasibility.md](webex-feasibility.md)
- Channel abstraction & eligibility: [notification-strategy.md](notification-strategy.md) §3, §6
- Integration boundaries & seams: [integration-architecture.md](integration-architecture.md)
- Deferred email-gated work: **D-14** in the deferred-work register
