# ADR-0023: Webex Integration — Multi-Channel Dispatch, HMAC Webhook, and OAuth Token Storage

- Status: Accepted
- Date: 2026-07-07
- Deciders: Architecture Committee (execution agent, operator-gated)

## Context and Problem Statement

P13 adds the Phase-2 Webex adapter (ADR-0005): governance events pushed to the committee Webex space as Adaptive Cards, and recording references pulled back. Realising it surfaced three decisions not settled by ADR-0005:

1. **Fan-out to multiple channels.** v1 had exactly one `INotificationChannel` (the in-app center), injected by ~15 handlers. Adding Webex as a second channel must not touch any caller.
2. **Authenticating an inbound webhook.** Webex calls back over HTTP with no user session — the first non-session endpoint in the codebase (no `AllowAnonymous` existed anywhere).
3. **Holding a Webex user token.** Meeting auto-create needs a secretary OAuth token (a bot cannot host); the token must survive restarts and refresh.

## Decision Drivers

- Zero churn to existing notification callers (INV-004 / clean boundaries).
- Webex strictly behind an adapter; v1 runs with it off (INV-003, CON-001; AC-071).
- Least-privilege, auditable inbound path; no secrets in source (INV-005, security-controls.md).
- Reuse the app-owned Hangfire (ADR-0014) and the cross-module seam pattern (ADR-0021); no new datastore beyond a tiny app-owned schema (INV-002).

## Decision Outcome

**1. Multi-channel dispatch via `INotificationSink` + `NotificationDispatcher`.** A new `INotificationSink` is the pluggable channel; `NotificationDispatcher : INotificationChannel` fans one message out to every registered sink and is the single `INotificationChannel`. The in-app channel becomes a sink; Webex registers a second sink (only when enabled). Callers are unchanged. The Webex sink collapses a per-recipient fan-out to **one** space post per `(Category, DeepLink)` within a request/job scope, and swallows its own failures so an optional channel never breaks the in-app write (the system of record). Only genuinely committee-wide events are mirrored to the shared space (subset-targeted events — vote/risk — stay in-app until per-user DM exists, D-14).

**2. Inbound webhook authenticated by HMAC, not a session.** `POST /api/webex/webhook` is the one anonymous endpoint; an endpoint filter verifies the `x-spark-signature` HMAC (default **HMAC-SHA1** — Webex's default; SHA256/512 configurable) over the **raw** body, constant-time, and rejects 401 on mismatch. A 5-minute timestamp window drops replays. The handler returns 200 immediately and enqueues processing on Hangfire; the recording write-back is idempotent, so no event-id store is needed.

**3. OAuth token stored encrypted in an app-owned `webex` schema.** The single secretary token row is AES-GCM-encrypted (key derived from `Webex:TokenEncryptionKey`) in a tiny `WebexDbContext` (`webex` schema, provisioned by the API `MigrationRunner`). The token service refreshes transparently and re-persists rotated refresh tokens; it returns null when unavailable so meeting-create degrades gracefully (AC-072).

Meeting-create is triggered by the `ScheduleMeeting` handler through a new Shared seam `IWebexMeetingProvisioner` (Meetings registers a no-op default; the Webex adapter overrides it when enabled) — enqueue only, so scheduling never blocks on Webex.

### Consequences

- Good: adding a channel is a sink + DI line, not a refactor; the webhook is auditable and fails closed; tokens never appear in source or logs; everything is off when `Webex:Enabled=false`; the adapter depends only on `Acmp.Shared` (ArchUnit-enforced).
- Trade-off: space delivery broadcasts committee-wide events to everyone in the space (acceptable — cards carry no sensitive content; deep links enforce their own RBAC); per-user DM/invitations/attendance-matching are deferred pending a Membership email projection (D-14). The webhook needs public ingress (ngrok/reverse-proxy) — a deployment concern; air-gapped installs fall back to manual recording attach.

## Validation

- Unit: card v1.3/≤80KB, sink gating + collapse + failure isolation, 429 reschedule, HMAC accept/reject (SHA1/SHA256), recording write-back, token encryption round-trip + refresh, meeting-create + provisioner gating.
- Integration (WebApplicationFactory): webhook good/bad signature → 200/401 with enqueue only on valid (AC-069); disabled → 200 no-op (AC-071 at the edge).
- ArchUnit: the Webex module depends only on `Acmp.Shared`.

## Links / Notes

- Extends ADR-0005 (INotificationChannel; Webex Phase 2), ADR-0021 (cross-module seams), ADR-0014 (Hangfire).
- The dedicated background-worker container (Hangfire server split out of the API) is a separate topology decision recorded under **ADR-0024** when that slice (WS0) lands; until then the send/create/webhook jobs run on the API's Hangfire server.
- Webex references: signature (developer.webex.com/blog/using-a-webhook-secret), Adaptive Cards v1.3, rate limiting (429 + Retry-After).
