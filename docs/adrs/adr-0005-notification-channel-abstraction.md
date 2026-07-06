# ADR-0005: Notification Channel Abstraction (INotificationChannel)

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

## Context and Problem Statement

ACMP needs to notify committee members of meeting reminders, action due-dates, vote openings, and status changes. The notification landscape is uncertain: no SMTP relay is available in v1, the organization has a centralized notification platform (Email/SMS/Firebase) that ACMP must not depend on (CON-001), and Webex integration is desirable but complex (see §5.3 — rate limits, Webex Assistant requirement for transcripts, Adaptive Cards). A design that hard-codes any single channel creates coupling that will be expensive to change.

## Decision Drivers

- CON-001: ACMP must not use the org's centralized notification platform.
- No email in v1 (no SMTP relay available at deployment time).
- Webex integration is technically feasible for messaging/cards but carries risk (rate limits, auth token lifecycle, Adaptive Cards ≤80 KB / ≤10 images — see §5.3 / https://developer.webex.com/messaging/docs/buttons-and-cards). Webex is deferred to Phase 2.
- An in-app notification center solves the immediate need (persistent, role-appropriate, dismissible notifications visible on next login) without any external dependency.
- Future channels (Webex, possibly email later) must be addable without modifying existing notification dispatch code.

## Considered Options

1. **`INotificationChannel` abstraction; in-app center v1; Webex adapter Phase 2** — clean interface, pluggable adapters, zero external dependency in v1.
2. **Webex-only from day one** — risky (Webex Assistant constraints, rate limits, token management); blocks v1 delivery on Webex integration complexity.
3. **Email only** — no SMTP relay in v1; deferred until infrastructure is available.
4. **Use org notification platform** — explicitly excluded by CON-001.
5. **Direct Webex SDK calls embedded in notification dispatch** — hard-codes the channel; no abstraction layer; prevents adding in-app or email later without refactoring.

## Decision Outcome

Chosen option: "`INotificationChannel` abstraction with in-app notification center as the v1 channel", because it eliminates external runtime dependencies in v1, meets the delivery deadline, and allows Webex and future channels to be added as pluggable adapters in Phase 2 without touching notification business logic. The in-app center provides a persistent, auditable notification history that Webex push alone would not.

### Consequences

- Good: v1 has zero external notification dependencies; notification logic is testable with a mock channel; in-app center doubles as a notification history/audit trail; adding Webex in Phase 2 is a new `INotificationChannel` implementation + Hangfire job, not a refactor; abstraction prevents org-platform coupling.
- Bad / trade-off: committee members who expect push notifications (email, Webex) in v1 will find the in-app center less immediate — this is a known gap and a product decision (Phase 2 addresses it). The in-app center requires the user to open ACMP to see notifications; there is no out-of-band delivery in v1.

## Validation

- v1 acceptance: in-app notification center delivers meeting reminders, vote-open notices, action-due alerts, and status-change events for all roles. Hangfire job dispatches via the registered `INotificationChannel` implementations.
- Phase 2 acceptance: Webex adapter implemented as a new `INotificationChannel`; Adaptive Cards sent for key events; adapter registered alongside in-app channel; both channels notified in parallel. Webex rate-limit handling (429 + Retry-After) implemented in the adapter (https://developer.webex.com/blog/rate-limiting-and-the-webex-api).
- No org notification platform call appears anywhere in the codebase (linted).

## Links / Notes

- Webex Adaptive Cards spec: https://developer.webex.com/messaging/docs/buttons-and-cards (v1.3; ≤80 KB card JSON + images; ≤10 image links).
- Webex rate limits: https://developer.webex.com/blog/rate-limiting-and-the-webex-api (most APIs ~300 req/min; shared per user; retry on 429).
- Notification durability: an SQL-backed outbox (in ACMP's own schema) guarantees at-least-once delivery even across app restarts; Hangfire retries handle transient adapter failures.
- Email channel: deferred until an SMTP relay is provisioned. When available, add an `INotificationChannel` implementation — no other code changes needed.
- Related: ADR-0013 (CON-001 — no org infra), ADR-0014 (Hangfire outbox), ADR-0002 (MediatR pipeline publishes domain events → notification dispatch).
