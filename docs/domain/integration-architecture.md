# ACMP Integration Architecture

**Purpose:** Define the overall integration approach for ACMP — patterns, integration points, protocols, phase assignment, and failure handling — such that v1 is fully functional with only Keycloak (mandatory) while all optional integrations slot in without architectural rework.

---

## 1. Integration Philosophy

ACMP is a **self-contained, on-prem deployment** (CON-001). It does not use the org's shared runtime infrastructure. Every external integration — whether mandatory (Keycloak) or optional (Webex, Tarseem, Keystone, SMTP) — sits behind a well-defined interface. This guarantees:

- **v1 ships without optional integrations.** The notification center, research module, and diagram module all have stub/no-op implementations of their interfaces in v1.
- **Adapters are swappable.** A new channel, renderer, or importer is added by implementing the interface; no core domain code changes.
- **Interface contracts are in the application layer.** Domain and use-case code never imports an integration SDK directly.

Two structural patterns govern all integrations:

### 1.1 Adapter / Anti-Corruption Layer (ACL)

Each external system is wrapped in a dedicated adapter that translates between the external system's model and ACMP's domain model. The adapter is the only code that knows the external API surface. Domain services call the interface; adapters implement it.

```
Domain / Use-case
     │  calls
     ▼
INotificationChannel / IDiagramRenderer / IResearchImporter / IIdentityProvider
     │  implemented by
     ▼
WebexAdapter / TarseemSidecarAdapter / KeystoneImportAdapter / KeycloakOidcAdapter
     │  communicates with
     ▼
External system (Webex SaaS / Tarseem container / Keystone package / Keycloak IdP)
```

The ACL converts error codes, maps identities, normalizes data formats, and enforces retry/circuit-breaker policy — shielding the domain from volatility in external contracts.

### 1.2 Transactional Outbox for Reliable Outbound

Any integration where ACMP must push a message/event outbound (notifications, diagram-render jobs) uses the **outbox pattern** backed by ACMP's own SQL Server (app-owned schema, same as Hangfire):

1. Domain write + outbox record are committed in **one local transaction**.
2. A Hangfire background job polls the outbox table, delivers the message (to in-app store, Webex API, Tarseem sidecar, etc.), and marks the record delivered or schedules retry.
3. Idempotency keys prevent duplicate delivery.

This guarantees no notification or render job is lost even if the process crashes between commit and send.

---

## 2. Integration Points

### 2.1 Keycloak — Identity (Mandatory, Phase 1)

| Attribute | Detail |
|---|---|
| **Direction** | Inbound (ACMP consumes identity from Keycloak) |
| **Protocol** | **OIDC Authorization Code + PKCE** (OAuth 2.1 flow) |
| **Sync/Async** | Synchronous (redirect → token exchange at login) |
| **Auth** | Client credentials registered in Keycloak; client secret in env/secret store |
| **Interface** | `IIdentityProvider` (returns `ClaimsPrincipal`; maps Keycloak realm/group roles to ACMP canonical roles) |
| **Failure handling** | Token validation failure → 401; Keycloak unreachable → 503 with graceful error page; no fallback local auth (SSO is mandatory per ADR-0004) |
| **Security** | HTTPS only; PKCE mitigates auth-code interception; tokens validated against JWKS endpoint; audience + issuer claims verified; short-lived access tokens; refresh token stored server-side only |
| **Phase** | **v1 (mandatory)** |

Claims flow: Keycloak group/realm-role claims → `KeycloakClaimsTransformer` → ACMP `RoleClaim` (Chairman, Secretary, Member, Reviewer, Auditor, Administrator, Submitter, Guest) → RBAC policies on endpoints and ABAC per topic/stream. No self-registration; user accounts are provisioned via Keycloak admin or invitation workflow.

### 2.2 In-App Notification Center (Mandatory, Phase 1)

| Attribute | Detail |
|---|---|
| **Direction** | Outbound (ACMP → in-app store → user browser via SSE/polling) |
| **Protocol** | Internal only; UI polls or subscribes via Server-Sent Events |
| **Sync/Async** | Async (outbox → Hangfire job → notification record in SQL → SSE push) |
| **Auth** | Same session auth; no external credential |
| **Interface** | `INotificationChannel` (method: `SendAsync(notification)`) |
| **Implementation** | `InAppNotificationChannel` — writes to `Notifications` table; SSE endpoint streams to authenticated session |
| **Failure handling** | Outbox guarantees delivery to the SQL store; SSE reconnect handles client disconnection; no external dependency |
| **Phase** | **v1 (mandatory)** |

This is the only notification channel in v1. Email (SMTP) and Webex are Phase 2+ adapters implementing the same `INotificationChannel` interface.

### 2.3 Webex Integration (Phase 2)

| Attribute | Detail |
|---|---|
| **Direction** | Bidirectional: ACMP → Webex (schedule, notify, create meetings); Webex → ACMP (webhooks for recording-ready, attendance events) |
| **Protocol** | REST (Webex API v1 over HTTPS); webhooks (Webex POST → ACMP public endpoint); OAuth 2.0 bot token |
| **Sync/Async** | Outbound: async via outbox + Hangfire (rate-limit safe); Inbound webhooks: async (idempotent handler, returns 200 immediately) |
| **Auth** | Bot token for sending; OAuth integration token for user-scoped operations; secret verified on incoming webhook payload |
| **Interface** | `INotificationChannel` (messaging/cards); `IMeetingIntegration` (scheduling, metadata retrieval) |
| **Failure handling** | 429 + Retry-After respected by Hangfire job; exponential back-off with jitter; circuit-breaker after N consecutive failures; outbox retains unsent records for manual replay |
| **Security** | Webhook secret validated; no Webex credentials stored outside secrets store; TLS 1.2+ enforced; air-gapped caveat: if no outbound internet, adapter is deployed as a no-op stub |
| **Phase** | **Phase 2** |

Detail: see `docs/domain/webex-feasibility.md`.

### 2.4 Tarseem Diagram Renderer (Phase 2)

| Attribute | Detail |
|---|---|
| **Direction** | Outbound (ACMP → Tarseem sidecar → artifacts returned) |
| **Protocol** | Internal HTTP (FastAPI thin wrapper) on Docker network; no public exposure |
| **Sync/Async** | Async (Hangfire render job calls sidecar; result stored in MinIO; diagram record updated) |
| **Auth** | Docker-network-internal only; no credential needed; sidecar not reachable from outside |
| **Interface** | `IDiagramRenderer` (method: `RenderAsync(DiagramSpec) → RenderResult`) |
| **Failure handling** | Sidecar returns `{ok: false, errors[]}` on bad spec (no exception); Hangfire retries transient failures; Chromium subprocess timeout configurable; circuit-breaker if sidecar is down |
| **Security** | Sidecar has no network access at render time (Tarseem design); artifacts stored in MinIO with pre-signed URLs; spec hash stored for traceability |
| **Phase** | **Phase 2** |

Detail: see `docs/domain/tarseem-analysis.md`.

### 2.5 Keystone Research Import (Optional, Phase 2)

| Attribute | Detail |
|---|---|
| **Direction** | Inbound (Keystone package → ACMP import) |
| **Protocol** | File import (JSON/Markdown manifest uploaded by secretary); no live API |
| **Sync/Async** | Synchronous import job triggered manually |
| **Auth** | ACMP session auth on the import endpoint; no Keystone credential |
| **Interface** | `IResearchImporter` (method: `ImportAsync(KeystonePackagePath) → ImportResult`) |
| **Failure handling** | Validation failure returns structured error list; partial imports are rolled back; idempotent by package hash |
| **Phase** | **Optional, Phase 2** |

Detail: see `docs/domain/keystone-analysis.md`.

### 2.6 SMTP / Email (Deferred, Phase 3+)

| Attribute | Detail |
|---|---|
| **Direction** | Outbound (ACMP → SMTP relay → user email) |
| **Protocol** | SMTP (TLS); MailKit library |
| **Interface** | `INotificationChannel` (same as in-app and Webex) |
| **Phase** | **Deferred** — no SMTP relay available in v1; implement when relay is provisioned |

---

## 3. Summary Table

| Integration | Direction | Protocol | Sync/Async | Phase | Mandatory? | Interface |
|---|---|---|---|---|---|---|
| Keycloak (OIDC) | Inbound | OIDC/OAuth 2.1 + PKCE | Sync (login) | v1 | **Yes** | `IIdentityProvider` |
| In-app notifications | Internal | SQL + SSE | Async (outbox) | v1 | **Yes** | `INotificationChannel` |
| Webex messaging/cards | Outbound | REST + bot token | Async (outbox) | Phase 2 | Optional | `INotificationChannel` |
| Webex webhooks | Inbound | HTTP POST (webhook) | Async (handler) | Phase 2 | Optional | `IMeetingIntegration` |
| Webex meeting metadata | Outbound | REST | Async | Phase 2 | Optional | `IMeetingIntegration` |
| Tarseem render sidecar | Outbound | Internal HTTP (FastAPI) | Async (Hangfire) | Phase 2 | Optional | `IDiagramRenderer` |
| Keystone import | Inbound | File (JSON/MD) | Sync (on demand) | Phase 2 | Optional | `IResearchImporter` |
| SMTP email | Outbound | SMTP/TLS | Async (outbox) | Phase 3+ | Deferred | `INotificationChannel` |

---

## 4. Outbox Schema (summary)

```sql
CREATE TABLE OutboxMessages (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Type        NVARCHAR(200)    NOT NULL,  -- 'Notification' | 'DiagramRender' | ...
    Payload     NVARCHAR(MAX)    NOT NULL,  -- JSON
    TargetChannel NVARCHAR(100)  NOT NULL,  -- 'InApp' | 'Webex' | 'Tarseem' | ...
    Status      NVARCHAR(50)     NOT NULL DEFAULT 'Pending',  -- Pending | Sent | Failed
    Attempts    INT              NOT NULL DEFAULT 0,
    NextRetryAt DATETIMEOFFSET   NULL,
    SentAt      DATETIMEOFFSET   NULL,
    Error       NVARCHAR(MAX)    NULL,
    CreatedAt   DATETIMEOFFSET   NOT NULL DEFAULT SYSUTCDATETIME(),
    IdempotencyKey NVARCHAR(200) NOT NULL
);
CREATE UNIQUE INDEX UX_Outbox_IdempotencyKey ON OutboxMessages (IdempotencyKey);
```

Hangfire job `ProcessOutboxJob` runs every 30 seconds (configurable); reads `Pending` records ordered by `CreatedAt`; dispatches to the appropriate adapter; updates `Status`. Retry cap and dead-letter behavior are configurable per channel.

---

## 5. Security Cross-Cuts

- All external HTTPS calls enforce TLS 1.2+; certificate pinning is configurable for high-sensitivity environments.
- Adapter credentials (bot tokens, client secrets) are externalized to environment variables / Docker secrets; never committed to source.
- Inbound webhooks (Webex) validate HMAC-SHA256 signature before processing.
- Rate-limit headers (`Retry-After`) are always respected; never spin-wait.
- Outbox records containing notification payloads are treated as sensitive (PII may be present); SQL encryption at rest applies.

---

## 6. v1 Behavior Without Optional Integrations

In v1, the following are true:
- `IDiagramRenderer` is a no-op stub that returns a placeholder SVG; diagram spec is stored and rendered in Phase 2.
- `IResearchImporter` is a no-op stub; research missions/findings/recommendations are entered manually.
- `INotificationChannel` has one registered implementation: `InAppNotificationChannel`.
- `IMeetingIntegration` is a no-op stub; meeting creation is manual; Webex links are entered as free-text.

All interface registrations are done via DI; swapping to a real adapter requires adding the adapter class and updating DI configuration — no domain or use-case changes.

---

**Traceability:** Implements ADR-0004 (Keycloak), ADR-0005 (INotificationChannel, Webex Phase 2), ADR-0006 (IDiagramRenderer, Tarseem Phase 2), ADR-0007 (IResearchImporter, Keystone optional). References `docs/domain/webex-feasibility.md`, `docs/domain/tarseem-analysis.md`, `docs/domain/keystone-analysis.md`. Constraint CON-001 governs the self-contained posture. FR/NFR references: `docs/requirements/functional.md`, `docs/requirements/non-functional.md`.
