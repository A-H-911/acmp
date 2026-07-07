# ADR-0024: Dedicated Background-Worker Container

- Status: Accepted
- Date: 2026-07-07
- Deciders: Architecture Committee (operator-requested; implemented in the P13 WS0 slice)

## Context and Problem Statement

Today the app-owned Hangfire **server** (ADR-0014) runs inside the API host: recurring jobs (the action-reminder sweep) and, with P13, the Webex send/create/webhook-processing jobs all execute in the same process that serves HTTP requests. As Webex adds rate-limited, retry-heavy, network-bound background work, co-locating it with request serving couples their failure and restart behaviour. The operator has asked to run the Hangfire server in a **separate worker container**.

INV-002 requires the platform to stay a modular monolith — no second process without a measured, ADR-recorded need. A dedicated worker is **not** microservices (same codebase, same database, same modules, one more host *type*), but splitting the API and the worker is a deployment-topology change that this ADR records rather than making silently.

## Decision Drivers

- Isolate long-running / rate-limited (Webex 429 back-off) and scheduled jobs from request-serving, so a stuck job or a Hangfire restart never affects the API and vice-versa.
- Keep one codebase, one SQL Server (the shared `HangFire` schema), one set of modules — no new datastore, no service mesh (INV-002, CON-001).
- Independent restart of the worker without an API outage.

## Considered Options

1. **Keep Hangfire server in the API** (status quo) — simplest, but couples job execution to request serving.
2. **Dedicated `Acmp.Worker` container running the Hangfire server; API keeps only the Hangfire client** — API enqueues, worker processes; both compose the same modules via a shared composition root.
3. Separate microservice per integration — rejected (INV-002; massive over-build for ≤20 users).

## Decision Outcome

Chosen option: **2**. A new `Acmp.Worker` .NET Generic Host runs `AddHangfireServer()` and the recurring-job registrations (the action-reminder sweep moves here); the API keeps `AddHangfire(storage)` **client-only** (retains `IBackgroundJobClient` to enqueue). Both hosts share an `AddAcmpModules(config)` / `AddAcmpHangfireStorage(cs)` composition root in the new `Acmp.Bootstrap` library so their DI never drifts. The **API owns migrations** (`MigrationRunner`); the worker waits for the schema (compose `depends_on: api healthy`), avoiding a two-host migration race. A configurable `ngrok` ingress service (compose profile `ngrok`) exposes the API's webhook endpoint (dev + optional prod via a reserved `NGROK_DOMAIN`).

No bespoke headless identity was added: every worker job either opts out of the MediatR authorization check (`SweepActionRemindersCommand` is not an `IAuthorizedRequest`) or hardcodes its own `system:*` audit actor (the sweep uses `system:action-reminders`, the Webex writers `system:webex`), and the shared-kernel `CurrentUserService` is null-safe with no `HttpContext`. A `SystemCurrentUser` would change zero observable behaviour, so it is deliberately omitted (YAGNI) — add one only if a future job reads `ICurrentUser`.

### Consequences

- Good: job execution is isolated and independently restartable; the Webex jobs run off the request path; one image/codebase, one DB.
- Trade-off: a second container to run and monitor; the shared composition root must be kept the single wiring source; ngrok in production adds a third-party to the inbound path (operator-accepted; org reverse-proxy preferred where available).

## Validation

- `CompositionRootTests` (in `Acmp.Application.Tests`) confirm the shared `AddAcmpModules` resolves the MediatR pipeline (the recurring sweep) and constructs every Webex job the worker runs, and that the Webex provisioner overrides the Meetings no-op default (Webex-after-Meetings order). This makes composition drift a build-time failure.
- The API/Application/ArchUnit suites stay green after moving the Hangfire server out of the API (143 API + 722 Application + 41 ArchUnit).
- **Pending (operator, live env):** `docker compose up` brings up API + worker + ngrok; a scheduled reminder and a Webex send/webhook both execute on the worker against the live sandbox.

## Links / Notes

- Extends ADR-0014 (Hangfire jobs), ADR-0023 (Webex jobs). The shared composition root lives in `Acmp.Bootstrap`; `Acmp.Worker` is a **host** (like `Acmp.Api`), exempt from the ArchUnit module-boundary rules.
