---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Architecture — ACMP

## Overview

ACMP is the single, auditable, bilingual (EN/AR) system of record for one Architecture Committee: intake → backlog → agenda → meeting → minutes → vote → decision → ADR → action → risk → dependency → traceability. It is **architecture governance, not generic project management**, serving a single committee, on-prem, at low traffic (≤20 users, ~15 concurrent).

The macro-architecture is a **modular monolith** (ADR-0001): one deployable ASP.NET Core 8 application, internally partitioned into bounded-context modules with enforced isolation — no module reads another module's tables; modules collaborate only through in-process public contracts, MediatR messages, or domain events. This was chosen over microservices (which would demand a broker, service discovery, and Kubernetes that the deployment constraint forbids, and would turn single-transaction vote/decision/audit immutability and in-SQL traceability traversal into a distributed-systems problem) and over a plain layered monolith (which, with 16 modules and rich cross-entity links, predictably decays into tangled cross-table reads). The modular monolith keeps the operational simplicity the constraints demand and the boundary discipline the domain demands, with isolation enforced at build time (ArchUnit/NDepend, NFR-047) and by schema-per-module DbContexts. Backend stack, storage, and identity are settled in ADR-0002, ADR-0003, and ADR-0004/ADR-0015; the frontend in ADR-0012.

Everything runs on one VM via Docker Compose, with a warm-standby VM for backup/restore. Per ADR-0015 all runtime dependencies are bundled and app-owned, so v1 has **zero external runtime services** (CON-001); the only external integration is Webex, deferred to Phase 2 behind an adapter.

Container topology (C4 Level 2):

```
                        ┌──────────────────────────┐
    Committee user      │  Keycloak (OIDC IdP)      │
    (browser)           │  self-hosted, bundled     │
        │               │  ADR-0015 — realm: acmp   │
        │ HTTPS         └────────────▲──────────────┘
        │                            │ OIDC auth-code + PKCE
        │                            │ validate JWT / JWKS
┌───────▼────────────────────────────┼─────────────────────────────────┐
│  PRIMARY VM — Docker Compose network (internal, TLS ≥1.2)             │
│                                     │                                  │
│   ┌──────────────┐   /api    ┌──────┴───────────────────────────┐     │
│   │ nginx        │──────────▶│ ACMP API — ASP.NET Core 8         │     │
│   │ TLS term.    │           │ modular monolith (16 modules)     │     │
│   │ serves SPA   │           │ + Hangfire in-process (jobs)      │     │
│   │ React 18/Vite│           │ + Serilog / OpenTelemetry         │     │
│   └──────────────┘           └───┬─────────┬──────────┬─────┬────┘     │
│                                  │ EF Core  │ Serilog  │ IFileStore    │
│                                  │ (TLS)    │ + OTLP   │ pre-signed    │
│                                  ▼          ▼          ▼     │ (PH-2)   │
│                          ┌──────────┐ ┌────────┐ ┌────────┐  │ internal │
│                          │ SQL      │ │ Seq    │ │ MinIO  │  │ HTTP     │
│                          │ Server   │ │ logs + │ │ S3-obj │  ▼          │
│                          │ schema-  │ │ traces │ │ store  │ ┌─────────┐ │
│                          │ per-mod  │ └────────┘ └────────┘ │ Tarseem │ │
│                          │ +outbox  │                       │ sidecar │ │
│                          │ +Hangfire│                       │ (PH-2)  │ │
│                          └────┬─────┘                       └─────────┘ │
└───────────────────────────────┼───────────────────────────────────────┘
                                 │ nightly SQL + MinIO backup
                        ┌────────▼─────────────────┐
                        │  STANDBY / BACKUP VM      │
                        │  warm; DR restore target  │
                        │  RTO ≤8h · RPO ≤4h        │
                        └───────────────────────────┘
```

Solid dependencies are v1 runtime. The Tarseem diagram sidecar (ADR-0006) is Phase 2 and always behind the `IDiagramRenderer` seam; Webex, SMTP, and Keystone are deferred/optional and never hard-wired.

## Components

The 16 canonical modules (bounded contexts). Each core-domain module owns one SQL schema and its own EF migrations; cross-module effects flow only through contracts, MediatR, and domain events.

| Component | Responsibility | Key controls |
|---|---|---|
| **Membership** | Users, committee, roles, permissions, streams, governed systems/services. | Roles sourced from Keycloak claims (ADR-0004); no self-registration; schema `membership`. |
| **Topics** | Central backlog anchor; topic intake, status lifecycle, stream assignment, DnD prioritization. | Explicit domain state machine; all other modules reference topics by id only; schema `topics`. |
| **Meetings** | Meeting, agenda, attendance, recording/transcript refs, discussion, Minutes of Meeting. | Agenda time-boxing; MoM version-preserving supersede; schema `meetings`. |
| **Decisions** | Committee `Decision` + `Vote`; outcomes, conditions, chair approval/override. | Votes + issued decisions immutable (ADR-0009); voting always attributed (ADR-0010); schema `decisions`. |
| **Actions** | Follow-up actions from decisions/meetings; progress, escalation, verification. | SoD-1 self-verification rejected; Hangfire reminders/overdue; schema `actions`. |
| **Risks** | Architecture risks with likelihood/impact and mitigations. | Linked-topic scope; append-only audit on every transition; schema `risks`. |
| **Dependencies** | Typed, status-bearing dependency edges between governed items. | Cross-aggregate reference by id only; schema `dependencies`. |
| **Governance** | Architecture Decision Records (in-app ADRs) and Architecture Invariants. | ADR immutable once Approved; Invariant immutable once Active (ADR-0009); filtered-unique one-ADR-per-decision; schema `governance`. |
| **Research** | Research missions, findings, recommendations. | Works standalone; optional Keystone import is additive (ADR-0007); schema `research`. |
| **Knowledge** | Living documentation, templates, shared comments. | TipTap OSS editor embedded; SQL FTS; bilingual pages; schema `knowledge`. |
| **Diagrams** | Diagram entities storing the version-controlled Tarseem JSON spec + artifact refs. | JSON spec is source of truth; render deferred to Phase 2 (ADR-0006); schema `diagrams`. |
| **Notifications** (cross-cutting) | In-app notification center; channel abstraction. | `INotificationChannel`; in-app only in v1 (ADR-0005); durable via SQL outbox; schema `notifications`. |
| **Reporting** (cross-cutting) | Dashboards and reports read models. | Read-only projections; columnstore for aggregation; no separate analytics DB (ADR-0003); schema `reporting`. |
| **Search & Traceability** (cross-cutting) | Typed directed `Relationship` edges + SQL Full-Text Search. | In-process graph traversal (ADR-0008); SQL FTS (ADR-0011); schema `trace`. |
| **Audit & Records** (cross-cutting) | Append-only `AuditEvent` log. | No UPDATE/DELETE path (NFR-040); audit row written in the same transaction as each state change (NFR-042); schema `audit`. |
| **Platform (Shared Kernel)** | IDs, `LocalizedString`, base entities, `IClock`, `ICurrentUser`, `IFileStore`, job infra, `Attachment`. | The only module every other may depend on (downward only); schema `platform`. |

## Contracts (seams)

**The dependency rule (enforced).** A module may not read another module's tables. Modules communicate only via in-process **public contracts** (interfaces / MediatR commands and queries) or **domain events**; cross-aggregate references are **by id only**, never EF navigation across a boundary. Polymorphic hosts (`Comment`, `Attachment`, `Relationship`, `AuditEvent`) use a soft `(SubjectType, SubjectId)` reference so they create no FK coupling. The only legal compile-time dependency direction into another module is **downward into the Shared Kernel**. Enforcement is mechanical, not cultural: a cross-module internal reference is a build failure (ArchUnit/NDepend, NFR-047), and a schema-per-module DbContext cannot map another schema's tables.

Named seams (public contracts across module or integration boundaries):

| Seam | Purpose | Notes |
|---|---|---|
| `ITraceabilityLinks` | Read typed relationship edges for graph traversal and impact analysis. | Read side of ADR-0008 / ADR-0020. |
| `ITraceabilityWriter` | Write a typed edge (e.g. Decision→ADR `RecordedAs`) from another module. | First cross-module write seam (ADR-0021). |
| `IActionLinkDirectory` | Resolve whether a decision has the required action link (AC-029 gate). | Gate lives in the handler, not the domain (ADR — P8d). |
| `IDecisionReader` | Read a `Decision` (outcome, rationale, statement) from Governance. | Feeds Decision→ADR promotion, FR-068 (ADR-0021). |
| `IFileStore` | Blob put/get and pre-signed time-limited URLs. | MinIO impl (ADR-0003); pre-signed ≤1h for sensitive media (NFR-027). |
| `INotificationChannel` | Send a notification via a channel. | In-app only in v1; Webex Phase 2 (ADR-0005); dispatched via SQL outbox. |
| `IDiagramRenderer` | Render a Tarseem JSON spec to artifacts. | Phase 2 Tarseem sidecar/CLI (ADR-0006); no-op before. |

Every command/query also flows through an ordered MediatR pipeline — Logging → Authorization (policy + ABAC) → Validation (FluentValidation) → Transaction/UoW → Audit → Outbox — so cross-cutting concerns are uniform and no handler is reachable unchecked.

## Data & deployment

- **Single datastore: Microsoft SQL Server only** (ADR-0003). Transactional store (schema-per-module), reporting via **columnstore** read models, and **SQL Full-Text Search** (ADR-0011) all live in one app-owned SQL Server instance. No second datastore, no distributed DB in v1. Persistence is **EF Core** with forward-only migrations run at container startup, in dependency order (Shared Kernel → platform → domain modules → Audit).
- **Object storage:** self-hosted MinIO behind `IFileStore` (bytes in MinIO, metadata in SQL); pre-signed URLs for sensitive files.
- **Deployment:** on-prem VM(s) + Docker Compose — no Kubernetes, no service mesh, no message broker. Self-contained (CON-001): SQL Server, Seq, MinIO, and Keycloak are bundled and app-owned; 99.9% availability is met by warm standby + nightly backups + fast container restart, not clustering.

Deep views: [data architecture](../domain/data-architecture.md) · [integration architecture](../domain/integration-architecture.md) · [deployment](../domain/deployment.md).

## Runtime status models

Runtime keys and status models follow the canonical scheme; a *proposed* item is never rendered as *approved*. Brief summary (full transitions, guards, and events in [entity lifecycles](../domain/entity-lifecycles.md)):

- **Topic:** `Draft → Submitted → Triage → Accepted → Prepared → Scheduled → InCommittee → Decided → Closed`, with side states `Rejected, Deferred, Reopened, Converted`.
- **Decision (committee outcome):** lifecycle `Draft → Issued → Superseded`; **outcome** is a separate closed enum (`Approved, ConditionallyApproved, Rejected, MoreInfoRequired, FeedbackProvided, EnhancementsRequired, DesignChangesRequired, ResearchRequired, Deferred, Escalated, Converted`). Immutable once `Issued` (ADR-0009).
- **Vote:** `Configured → Open → Closed → Ratified`; ballots append-only and always attributed; tally frozen at close (ADR-0010).
- **Action:** `Open → InProgress → Blocked → Completed → Verified`, side `Cancelled`, derived `Overdue`; SoD-1 blocks self-verification.
- **ADR (in-app):** `Draft → Proposed → Approved → (Superseded | Deprecated)`; immutable once `Approved`.
- **Architecture Invariant:** `Draft → Proposed → Active → (Retired | Superseded)`; `Active` statement immutable — material change = supersede. Violations tracked separately as Risk/Action/AuditEvent.
- **Risk:** `Open → Mitigating → Closed`, side `Accepted, Escalated`.

## Open architectural points

Unresolved architecture questions and rejected alternatives are tracked as first-class items in the [open-question register](../decisions/open-question-register.md).

## Diagrams

Architecture diagrams (system context, container, component/module, core-loop sequence) are authored as diagram-as-code and, from Phase 2, rendered by the Tarseem sidecar from a version-controlled JSON spec (ADR-0006). See [diagrams/](diagrams/).
