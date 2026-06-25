# ACMP Webex Integration Feasibility Analysis

**Purpose:** Assess what Webex APIs can and cannot support for ACMP's Phase-2 integration, based on inspected developer documentation, and define the recommended Phase-2 scope and explicit exclusions.

---

## 1. Source Basis

All findings below are drawn from official Webex developer documentation inspected 2026-06-24. Citations are in §7. Items not independently verified from those sources are marked `[unverified]`.

---

## 2. API Fundamentals

### 2.1 Authentication

Two principal token types are relevant:

| Token type | Scope | Rate-limit tier | Ownership caveat |
|---|---|---|---|
| **Bot token** | Bot user identity; less restrictive limits | Higher headroom | Messages sent by bot are owned by bot, not the meeting host |
| **OAuth integration token** | User-scoped (delegated); user consents | Per-user limit | Content owned by the real user |

**Recommendation:** Use a **bot token** for outbound notifications (Adaptive Cards in spaces/rooms) and for API reads where bot membership in the meeting space is sufficient. Use **OAuth integration** only if user-attributed content ownership is required and users can complete the OAuth consent flow on a private-network device. For an on-prem, low-traffic, ≤20-user deployment, a single bot account simplifies credential management.

### 2.2 Rate Limits

Source: https://developer.webex.com/blog/rate-limiting-and-the-webex-api ; https://developer.webex.com/docs/rest-api-basics

- Most REST endpoints: **~300 requests/minute** per token (shared across all calls using that token).
- `/people` and `/messages` endpoints: **higher and dynamically adjusted** limits.
- Limits are **shared per user** (or per bot), not per endpoint in isolation.
- On limit breach: HTTP **429** with a `Retry-After` header specifying seconds to wait.
- Bot accounts have **less restrictive** rate limits than user tokens `[unverified exact values]`.

**ACMP implication:** At ≤20 users, peak notification bursts are unlikely to exceed 300 req/min. The outbox pattern (§3 of `docs/17-integration-architecture.md`) queues outbound calls and processes them with Hangfire; the `WebexAdapter` must read `Retry-After` on 429 and reschedule the Hangfire job accordingly. Never retry in-process tight-loop.

---

## 3. Capability Analysis

### 3.1 Capability Table

| Desired capability | Supported? | API / mechanism | Constraints / licensing / privacy | ACMP use & phase |
|---|---|---|---|---|
| **Scheduling a meeting** | Yes | `POST /meetings` | Host must have a Webex account; meeting type, password, timezone params | Phase 2: create meeting from Agenda record |
| **Sending meeting invitation** | Yes | `POST /meetings/invitees` | Invitees must have Webex accounts; email-based | Phase 2: invite committee members |
| **Retrieving meeting metadata** | Yes | `GET /meetings/{id}` | Returns title, start/end, host, state, siteUrl | Phase 2: store in Meeting record for traceability |
| **Meeting attendance / participants** | Yes | `GET /meetingParticipants` | Real-time or post-meeting; includes join/leave timestamps | Phase 2: attendance record on MoM |
| **Recording download URL** | Yes | `GET /recordings/{id}` (downloadUrl + playbackUrl) | Requires recording to be enabled; storage in Webex cloud | Phase 2: store reference URL in MinIO metadata |
| **Recording-ready webhook** | Yes | `recordings` / `convergedRecordings` webhook resource | HTTP POST to ACMP endpoint when recording available | Phase 2: trigger ingestion of recording reference |
| **Transcripts (machine-readable)** | **Gated** | `GET /meetingTranscripts`, snippets API | **Requires Webex Assistant to be ON** — cannot be enabled programmatically; must be enabled by host/participant or Control Hub default | Phase 3 at earliest; depends on licensing + privacy approval |
| **Transcript webhook** | Yes (but gated) | `meetingTranscripts` webhook resource | Only fires if transcripts are generated (i.e., Webex Assistant was on) | Phase 3 |
| **Messaging to a space/room** | Yes | `POST /messages` | Bot must be member of the space | Phase 2: notifications into a committee Webex space |
| **Adaptive Cards (Buttons & Cards)** | Yes — v1.3 | `POST /messages` with `attachments` | Cards ≤80 KB, ≤10 image links; Webex supports **Adaptive Cards v1.3** | Phase 2: structured notification cards |
| **Card action responses (attachmentActions)** | Yes | `GET /attachment/actions/{id}` | Requires webhook on `attachmentActions` resource | Phase 2: read user responses from cards |
| **Webhooks (general)** | Yes | `POST /webhooks` (create); `POST {targetUrl}` (Webex calls back) | Supported resources: meetings, recordings, convergedRecordings, meetingParticipants, meetingTranscripts, rooms, messages, attachmentActions, adminBatchJobs | Phase 2 |
| **Webhook secret verification** | Yes | `secret` field on webhook; HMAC-SHA256 of payload | Must verify on every inbound request | Phase 2: mandatory security control |

### 3.2 Transcript Deep-Dive (Critical Constraint)

Source: https://developer.webex.com/docs/api/v1/meeting-transcripts ; https://developer.webex.com/docs/api/guides/access-meeting-resources-guide

- **Transcripts are generated only when Webex Assistant is active** for the meeting. Webex Assistant is a licensed add-on or Control Hub default setting.
- **There is no API call to enable Webex Assistant programmatically.** A host, participant, or admin must turn it on before/during the meeting (in the Webex client or via Control Hub defaults).
- The **snippets API** provides machine-readable, speaker-attributed transcript fragments — but only after a transcript exists.
- Privacy implications: transcript capture requires disclosure to participants and may conflict with organizational privacy policies. This decision requires explicit organizational approval, not an engineering default.

**Decision (confirmed):** Do NOT assume automated transcript availability. ACMP v1 and Phase 2 treat transcripts as **manually uploaded** (secretary uploads the file or pastes content into a text field). Webex transcript automation is deferred to Phase 3 and is conditional on: (a) Webex Assistant licensing being in place, (b) org privacy policy approval, (c) the implementation of `meetingTranscripts` webhook + snippets ingestion.

### 3.3 Adaptive Cards Constraints

Source: https://developer.webex.com/messaging/docs/buttons-and-cards

- Webex supports **Adaptive Cards v1.3** (not later versions).
- Card payload + images: ≤80 KB total, ≤10 image links.
- Card actions trigger `attachmentActions` events retrievable via webhook.

**ACMP design implication:** Notification cards for meeting reminders, decision-ready alerts, and action-due reminders must be authored against the v1.3 schema. Do not use v1.4+ elements. Keep cards lightweight — title, status, one CTA button, link back to ACMP. Test card rendering in the Webex client; Webex's renderer may not support all v1.3 elements uniformly `[unverified exact gaps]`.

---

## 4. Authentication and Bot Setup

**Recommended model for ACMP Phase 2:**

1. Create a **Webex Bot** in the Webex Developer portal (https://developer.webex.com/my-apps).
2. Store the bot token in ACMP's secrets store (Docker secret / env var); inject via `IOptions<WebexBotOptions>`.
3. The bot is added to a designated committee Webex space; ACMP sends all notifications to that space.
4. Register a **webhook** (`POST /webhooks`) pointing to ACMP's public-facing inbound endpoint (or a relay/ngrok in dev) for `recordings`, `meetingParticipants`, and `attachmentActions` resources.
5. For meeting scheduling (user-attributed), consider an OAuth integration with one designated secretary account rather than forcing all 20 users through OAuth consent.

**Air-gapped caveat:** If the ACMP deployment environment has no outbound internet access to `webexapis.com`, the `WebexAdapter` is built and registered in DI but configured as inactive (feature flag `WebexEnabled = false`). All outbound calls short-circuit; the in-app notification channel remains the active channel. The adapter is not removed — it is ready to activate when network access is granted.

---

## 5. Recommended Phase-2 Scope

### In scope for Phase 2

| Capability | Detail |
|---|---|
| **Bot messaging + Adaptive Cards v1.3** | Notifications into a committee Webex space: meeting reminders, recording-ready alerts, decision published, action overdue |
| **Meeting creation + invitations** | ACMP creates meeting from Agenda publish; invitees auto-populated from membership |
| **Meeting metadata retrieval** | `GET /meetings/{id}` → store meeting URL, join link in Meeting record |
| **Attendance from meetingParticipants** | Post-meeting: retrieve participant list → auto-populate attendance on draft MoM |
| **Recording reference storage** | `recordings` webhook triggers ACMP to fetch `downloadUrl`/`playbackUrl` and store as a reference (not download the file) in the Meeting record |
| **Webhook infrastructure** | Inbound endpoint, HMAC-SHA256 verification, idempotent handler; registered for recordings + meetingParticipants + attachmentActions |

### Explicitly out of scope for Phase 2 (do not assume)

| Capability | Reason |
|---|---|
| **Automated transcript ingestion** | Gated by Webex Assistant; cannot be enabled programmatically; requires licensing + privacy approval |
| **Speaker-attributed snippets** | Dependent on transcript availability (same gate) |
| **Programmatic Webex Assistant control** | Not possible via API |
| **Full recording file download** | Not needed; reference URL is sufficient; large binary storage in MinIO requires separate sizing |
| **Real-time in-meeting participation** | Out of scope for governance platform |
| **Adaptive Cards v1.4+** | Webex supports only v1.3 |

---

## 6. Integration Decision

**Problem:** ACMP needs to notify committee members where they already communicate (Webex) and capture meeting artefacts (recordings, attendance) with minimal manual effort.

**Constraints:** Self-contained (CON-001); Webex is external SaaS; transcript automation is gated by org policy + Webex Assistant; ≤20 users; on-prem network may restrict outbound.

**Options:**
1. Full Webex integration (scheduling + messaging + transcripts)
2. Webex messaging + recording reference only; transcripts manual
3. No Webex; in-app notifications only

**Trade-offs:**
- Option 1 creates a hard dependency on Webex Assistant licensing and org privacy approval — not controllable by the engineering team.
- Option 3 is functionally correct but misses where users already live (Webex).
- Option 2 delivers real value (meeting workflow + notifications) without uncontrollable dependencies.

**Recommendation:** Option 2 — Phase 2 scope as defined in §5. Transcript automation is Phase 3, conditional on prerequisites.

**Risks:**
- Bot access to committee space may require Webex admin approval (not an engineering control).
- Outbound network access to webexapis.com must be confirmed with infrastructure team (ASM-XXX `[to be assigned]`).
- Webex API schema changes could break the adapter; pin to a tested API version; adapter unit-tests should mock the API.

**Validation:** Phase 2 integration tested in a Webex sandbox org before org deployment. Webhook endpoint tested with Webex developer tools. Card rendering validated in actual Webex client (not just schema validation).

---

## 7. Citation URLs

- Rate limiting: https://developer.webex.com/blog/rate-limiting-and-the-webex-api
- REST API basics: https://developer.webex.com/docs/rest-api-basics
- Webhooks: https://developer.webex.com/docs/api/v1/webhooks ; https://developer.webex.com/admin/docs/api/guides/webhooks
- Recordings API: https://developer.webex.com/admin/docs/api/v1/recordings
- Meeting transcripts: https://developer.webex.com/docs/api/v1/meeting-transcripts
- Access meeting resources guide: https://developer.webex.com/docs/api/guides/access-meeting-resources-guide
- Buttons & Cards: https://developer.webex.com/messaging/docs/buttons-and-cards

---

**Traceability:** Deliverable 26. Implements ADR-0005 (INotificationChannel + Webex Phase 2 adapter). References `docs/17-integration-architecture.md` §2.3 (Webex integration point) and `docs/29-notification-strategy.md`. Recordings/transcript scope confirmed by settled decision §0 item 9 (digest). Scale constraint ≤20 users from README §A.
