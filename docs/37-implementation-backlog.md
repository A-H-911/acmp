# 37 — Prioritized Implementation Backlog

**Purpose:** Ordered build sequence for ACMP — PH-1 first, then PH-2 and PH-3 — prioritized by value and dependency. Covers Deliverable 45.

**Priority key:** P1 = blocker/foundation; P2 = core loop; P3 = workflow completion; P4 = enhancement/expansion.

**Size key:** XS = <1d; S = 1–2d; M = 3–5d; L = 1–2w; XL = 2–4w.

**Depends-on:** BL-### references must be delivered before this item starts. Items with no listed dependency may begin immediately after their epic's prerequisites.

---

## Phase 1 — MVP Governance

### Group A: Foundation (Deliver First — Everything Depends on This)

| BL-ID | Work Item | EPIC | Module | Phase | Size | Priority | Depends-On |
|---|---|---|---|---|---|---|---|
| BL-001 | Docker Compose stack: ACMP app + SQL Server + Seq + MinIO + health probes | EPIC-01 | Platform | 1 | M | P1 | — |
| BL-002 | EF Core project setup: DbContext, migrations infrastructure, apply-on-startup (non-prod) / CLI (prod), rollback doc | EPIC-01 | Platform | 1 | S | P1 | BL-001 |
| BL-003 | Configuration management: secrets via env vars; no secrets in image; `IOptions<>` binding per module | EPIC-01 | Platform | 1 | S | P1 | BL-001 |
| BL-004 | Serilog structured logging → self-hosted Seq; correlation ID + masked user ID middleware; log scopes | EPIC-01 | Platform | 1 | S | P1 | BL-001 |
| BL-005 | OpenTelemetry: traces + metrics for all HTTP requests and Hangfire jobs; Seq/OTel export | EPIC-01 | Platform | 1 | S | P1 | BL-004 |
| BL-006 | App-owned Hangfire: own SQL schema; retry/failure queue; Administrator dashboard; job registration scaffold | EPIC-01 | Platform | 1 | S | P1 | BL-002 |
| BL-007 | SQL outbox pattern: transactional publish + Hangfire delivery consumer; IOutboxMessage contract | EPIC-01 | Platform | 1 | S | P1 | BL-006 |
| BL-008 | MinIO IFileStore implementation: S3-compatible; pre-signed time-limited URLs; metadata entity + migration | EPIC-01 | Platform | 1 | S | P1 | BL-002 |
| BL-009 | ASP.NET Core health checks: liveness + readiness; Docker Compose HEALTHCHECK wired | EPIC-01 | Platform | 1 | XS | P1 | BL-001 |
| BL-010 | OpenAPI/Swagger document: auto-generated; accessible in dev/staging; ProblemDetails RFC 7807 error model | EPIC-01 | Platform | 1 | XS | P1 | BL-001 |
| BL-011 | React + Vite + TypeScript scaffold: module layout, route guards, error boundaries, API client (typed via OpenAPI) | EPIC-01 | Platform | 1 | M | P1 | BL-010 |
| BL-012 | react-i18next: EN/AR locale files; locale switcher; persisted locale; switch without losing unsaved data | EPIC-01 | Platform | 1 | S | P1 | BL-011 |
| BL-013 | RTL layout: CSS logical properties throughout; `dir` attribute toggled; no LTR-only layouts; RTL smoke test | EPIC-01 | Platform | 1 | M | P1 | BL-012 |
| BL-014 | Light/dark theme: CSS variables; user preference persisted in localStorage; applied on load | EPIC-01 | Platform | 1 | S | P1 | BL-011 |
| BL-015 | Unsaved-work guard: navigation-away confirmation prompt on dirty forms; reusable hook | EPIC-01 | Platform | 1 | S | P1 | BL-011 |
| BL-016 | Localized validation errors: ProblemDetails returned in active locale; frontend error display component | EPIC-01 | Platform | 1 | S | P1 | BL-012 |

### Group B: Identity & Membership (Must Precede All Feature Work)

| BL-ID | Work Item | EPIC | Module | Phase | Size | Priority | Depends-On |
|---|---|---|---|---|---|---|---|
| BL-017 | OIDC authorization-code + PKCE flow: Keycloak integration; token validation middleware; silent renew; logout | EPIC-02 | Platform/Identity | 1 | M | P1 | BL-003 |
| BL-018 | Auth service adapter: configuration-driven switch between Keycloak and legacy org auth; no code change to switch | EPIC-02 | Platform/Identity | 1 | S | P1 | BL-017 |
| BL-019 | Keycloak claim parser: group/realm-role claims → ACMP canonical roles; role stored on token context | EPIC-02 | Platform/Identity | 1 | S | P1 | BL-017 |
| BL-020 | ASP.NET Core policy-based authorization: role policies per endpoint; 403 on unauthorized (never 401 post-login) | EPIC-02 | Platform/Identity | 1 | S | P1 | BL-019 |
| BL-021 | Per-topic ABAC capability resolver: checks Owner/Assignee/Presenter relationship for artifact-scoped access | EPIC-02 | Platform/Identity | 1 | S | P1 | BL-020 |
| BL-022 | User entity + migration: name, email, streams, deactivated flag; seeded on first-login from OIDC claims | EPIC-03 | Membership | 1 | S | P1 | BL-002, BL-019 |
| BL-023 | Administrator: create user (name, email, stream assignment); no self-registration endpoint | EPIC-03 | Membership | 1 | S | P2 | BL-022 |
| BL-024 | Stream entity + migration; stream assignment multi-select per user; stream filter context propagated | EPIC-03 | Membership | 1 | S | P2 | BL-022 |
| BL-025 | User deactivation: blocks login; historical records (votes, authorship) intact and attributed | EPIC-03 | Membership | 1 | S | P2 | BL-022 |
| BL-026 | Member directory UI: paginated list of active users with name, role, streams, email; all authenticated users | EPIC-03 | Membership | 1 | S | P2 | BL-023, BL-024 |
| BL-027 | Role-aware navigation: frontend hides unauthorized nav items and action buttons (hidden, not merely disabled) | EPIC-02 | Platform/Identity | 1 | S | P2 | BL-019, BL-011 |

### Group C: Topics — Core (Core Governance Artifact)

| BL-ID | Work Item | EPIC | Module | Phase | Size | Priority | Depends-On |
|---|---|---|---|---|---|---|---|
| BL-028 | Topic entity + migration: all fields (title, description, type, urgency, source, scope, affected streams/systems, target date, priority ordinal, status); TOP-YYYY-### ID generation | EPIC-04 | Topics | 1 | M | P2 | BL-002, BL-024 |
| BL-029 | Topic submission API + UI: form with required-field validation (EN+AR); attachment upload via IFileStore | EPIC-04 | Topics | 1 | M | P2 | BL-028, BL-008, BL-016 |
| BL-030 | Topic status state machine: transitions enforced in domain; immutable rejection/deferral event recording | EPIC-04 | Topics | 1 | M | P2 | BL-028 |
| BL-031 | Topic triage workflow: Secretary accept/reject (mandatory rationale)/defer (mandatory reason + date) | EPIC-04 | Topics | 1 | S | P2 | BL-030 |
| BL-032 | Topic edit lock: editable by Owner/Secretary in Draft/Submitted/Triage; post-Accepted metadata-only for Secretary | EPIC-04 | Topics | 1 | S | P2 | BL-030 |
| BL-033 | Topic comment thread: timestamped, attributed, immutable after post; API + UI | EPIC-04 | Topics | 1 | S | P2 | BL-028 |
| BL-034 | Topic detail page: all fields, status history with actors/timestamps, linked artifacts panel, traceability excerpt | EPIC-04 | Topics | 1 | M | P2 | BL-028, BL-033 |
| BL-035 | Aging indicator: background Hangfire job evaluates urgency SLA thresholds; visual badge + Secretary notification | EPIC-04 | Topics | 1 | S | P2 | BL-028, BL-006 |
| BL-036 | Prepared + Scheduled status transitions: Secretary marks Prepared; schedules to meeting (one future meeting at a time) | EPIC-04 | Topics | 1 | S | P2 | BL-030 |

### Group D: Topics — Backlog Views

| BL-ID | Work Item | EPIC | Module | Phase | Size | Priority | Depends-On |
|---|---|---|---|---|---|---|---|
| BL-037 | Backlog list view: title, type badge, urgency badge, status, owner, streams, dates; server-side sort/filter | EPIC-05 | Topics | 1 | M | P2 | BL-028 |
| BL-038 | Backlog table/dense view: all fields as columns; user-configurable column visibility/order per session | EPIC-05 | Topics | 1 | M | P2 | BL-037 |
| BL-039 | Backlog kanban view: status columns; topic cards; @dnd-kit DnD within column (priority reorder) + between columns (status change with permission check) | EPIC-05 | Topics | 1 | L | P2 | BL-028 |
| BL-040 | Backlog calendar view: topics by scheduled meeting date; monthly + weekly layout | EPIC-05 | Topics | 1 | M | P2 | BL-028 |
| BL-041 | DnD reprioritization: explicit ordinal stored; keyboard-accessible move-up/down alternative (WCAG 2.2 AA) | EPIC-05 | Topics | 1 | S | P2 | BL-039 |
| BL-042 | Aging indicators on all backlog views: badge, color, tooltip with SLA deadline | EPIC-05 | Topics | 1 | S | P2 | BL-037, BL-035 |

### Group E: Meetings — Agenda & Meeting Record

| BL-ID | Work Item | EPIC | Module | Phase | Size | Priority | Depends-On |
|---|---|---|---|---|---|---|---|
| BL-043 | Meeting record entity + migration: MTG-YYYY-###; date, time, type, mode, agenda reference | EPIC-06 | Meetings | 1 | S | P2 | BL-002 |
| BL-044 | Agenda entity + migration: AGN-YYYY-###; ordered agenda items with time-box, presenter assignment | EPIC-06 | Meetings | 1 | S | P2 | BL-043 |
| BL-045 | Agenda creation UI: select Scheduled topics; @dnd-kit order + keyboard alternative; time-box per item; presenter assign | EPIC-06 | Meetings | 1 | M | P3 | BL-044, BL-036 |
| BL-046 | Carry-over auto-suggest: unresolved agenda items from previous meeting surfaced on new agenda creation | EPIC-06 | Meetings | 1 | S | P3 | BL-044 |
| BL-047 | Agenda publish: API + Hangfire notification dispatch; in-app notification to all members with deep link | EPIC-06 | Meetings | 1 | S | P3 | BL-044, BL-052 |
| BL-048 | Attendance tracking: Secretary marks Present/Absent/Remote per member per meeting; quorum context | EPIC-06 | Meetings | 1 | S | P3 | BL-043 |
| BL-049 | Live meeting notes: free-text Markdown per agenda item; auto-save debounce ≤2s | EPIC-06 | Meetings | 1 | S | P3 | BL-044 |
| BL-050 | Manual recording attachment: Secretary pastes link or uploads file to meeting record | EPIC-06 | Meetings | 1 | S | P3 | BL-043, BL-008 |
| BL-051 | Meeting calendar view: past + upcoming meetings with status badges | EPIC-06 | Meetings | 1 | S | P3 | BL-043 |

### Group F: MoM

| BL-ID | Work Item | EPIC | Module | Phase | Size | Priority | Depends-On |
|---|---|---|---|---|---|---|---|
| BL-052 | In-app notification center: bell icon, notification list, mark-as-read, deep-link per notification; INotificationChannel abstraction + in-app concrete adapter | EPIC-13 | Notifications | 1 | M | P2 | BL-007, BL-011 |
| BL-053 | MoM entity + migration: MIN-YYYY-###; versioned content; approval state | EPIC-07 | Meetings | 1 | S | P3 | BL-043 |
| BL-054 | MoM generation: compile attendance + agenda items + notes + decisions + actions into structured Markdown | EPIC-07 | Meetings | 1 | M | P3 | BL-053, BL-048, BL-049 |
| BL-055 | MoM versioning + approval workflow: Secretary drafts → Reviewer/Chairman annotates → Chairman approves → content locked | EPIC-07 | Meetings | 1 | M | P3 | BL-054 |
| BL-056 | MoM distribution: in-app notification to all committee members on approval with deep link | EPIC-07 | Meetings | 1 | S | P3 | BL-055, BL-052 |

### Group G: Decisions & Voting

| BL-ID | Work Item | EPIC | Module | Phase | Size | Priority | Depends-On |
|---|---|---|---|---|---|---|---|
| BL-057 | Decision entity + migration: DECN-YYYY-###; outcome (canonical enum), rationale, alternatives, conditions, authority, effective date | EPIC-08 | Decisions | 1 | M | P2 | BL-002 |
| BL-058 | Decision creation API + UI: canonical outcome dropdown; alternatives list; conditions; downstream link requirement before Issued | EPIC-08 | Decisions | 1 | M | P2 | BL-057, BL-028 |
| BL-059 | Decision immutability enforcement: content locked once Issued; corrections via new superseding decision; supersession chain | EPIC-08 | Decisions | 1 | S | P2 | BL-057 |
| BL-060 | Vote entity + migration: VOTE-… IDs; eligible voters list, options, quorum threshold, abstention flag; lifecycle status | EPIC-09 | Decisions | 1 | M | P2 | BL-057 |
| BL-061 | Vote configuration + open UI: Secretary configures; vote opens; in-app notification to eligible voters | EPIC-09 | Decisions | 1 | M | P2 | BL-060, BL-052 |
| BL-062 | Vote casting: one-vote-per-voter enforcement at API layer; abstention option; live aggregate display | EPIC-09 | Decisions | 1 | M | P2 | BL-060 |
| BL-063 | Quorum enforcement: vote cannot close if present-voters < quorum threshold; quorum check from attendance record | EPIC-09 | Decisions | 1 | S | P2 | BL-060, BL-048 |
| BL-064 | Chairman approval: Confirm / Override (+ mandatory reason) / Abstain-from-override; immutable record | EPIC-09 | Decisions | 1 | M | P2 | BL-060, BL-020 |
| BL-065 | Vote audit: all individual choices + timestamps + chairman action in append-only audit log; immutable after Ratified | EPIC-09 | Decisions | 1 | S | P2 | BL-060, BL-066 |
| BL-066 | Audit log entity + migration: append-only; entity type, entity ID, action, actor, timestamp UTC, before/after JSON, correlation ID; no UPDATE/DELETE permitted | EPIC-16 | Audit&Records | 1 | M | P1 | BL-002 |
| BL-067 | Audit hash chain: SHA-256 chained hash over vote + issued-decision audit records; chain integrity verifiable | EPIC-16 | Audit&Records | 1 | M | P1 | BL-066 |
| BL-068 | Decision history list: filterable by outcome, topic, stream, date range, chairman | EPIC-08 | Decisions | 1 | S | P3 | BL-057 |

### Group H: Actions, Risks, Dependencies

| BL-ID | Work Item | EPIC | Module | Phase | Size | Priority | Depends-On |
|---|---|---|---|---|---|---|---|
| BL-069 | Action entity + migration: ACT-… IDs; title, owner (required), due date (required), description, priority; linked to topic/decision/risk | EPIC-10 | Actions | 1 | S | P2 | BL-002 |
| BL-070 | Action creation API: required-field enforcement (owner + due date) at API + UI layer | EPIC-10 | Actions | 1 | S | P2 | BL-069 |
| BL-071 | Action status tracking: Open→InProgress→Blocked→Completed; Verified (Secretary/Chairman); Cancelled (mandatory reason) | EPIC-10 | Actions | 1 | M | P2 | BL-069 |
| BL-072 | Overdue derivation: background Hangfire daily job; derived status displayed; no user action required | EPIC-10 | Actions | 1 | S | P2 | BL-069, BL-006 |
| BL-073 | Action reminder Hangfire job: notification to owner N days before due (N configurable, default 3) | EPIC-10 | Actions | 1 | S | P3 | BL-069, BL-006, BL-052 |
| BL-074 | Action escalation Hangfire job: notification to Secretary + Chairman when overdue > threshold (default 2d) | EPIC-10 | Actions | 1 | S | P3 | BL-072, BL-052 |
| BL-075 | Actions dashboard: open/blocked/overdue across all topics; filterable by owner, stream, topic, due date, status | EPIC-10 | Actions | 1 | M | P3 | BL-069 |
| BL-076 | Risk entity + migration: RSK-… IDs; title, description, likelihood, impact, owner, mitigation plan; linked to topic/action | EPIC-11 | Risks | 1 | S | P2 | BL-002 |
| BL-077 | Risk creation + status lifecycle API + UI: mitigation plan required before Close; Accepted status with rationale | EPIC-11 | Risks | 1 | M | P2 | BL-076 |
| BL-078 | Dependency edge entity + migration: DPN-… IDs; source, target, type (Blocks/DependsOn/RelatesTo) | EPIC-12 | Dependencies | 1 | S | P2 | BL-002 |
| BL-079 | Cross-stream dependency flag: visual highlight on topic detail when source + target affect different streams | EPIC-12 | Dependencies | 1 | S | P2 | BL-078, BL-024 |
| BL-080 | Per-topic dependency list: inbound + outbound edges on topic detail page | EPIC-12 | Dependencies | 1 | S | P2 | BL-078, BL-034 |

### Group I: Audit Search, Notifications, Traceability

| BL-ID | Work Item | EPIC | Module | Phase | Size | Priority | Depends-On |
|---|---|---|---|---|---|---|---|
| BL-081 | Audit log search UI: Auditor/Administrator search by entity type, entity ID, actor, action, date range; paginated | EPIC-16 | Audit&Records | 1 | M | P3 | BL-066 |
| BL-082 | Notification event catalog: all PH-1 events wired to INotificationChannel dispatch (topic status, agenda published, vote open/close, MoM published, action reminder/overdue/escalation, risk escalation) | EPIC-13 | Notifications | 1 | M | P3 | BL-052, BL-007 |
| BL-083 | Traceability relationship entity + migration: typed edges between any two artifacts; types enum | EPIC-15 | Search&Traceability | 1 | S | P2 | BL-002 |
| BL-084 | Traceability panel: upstream + downstream typed edges on every artifact detail page | EPIC-15 | Search&Traceability | 1 | M | P2 | BL-083 |
| BL-085 | Typed edge CRUD API: create/read/delete; relationship type validated against enum | EPIC-15 | Search&Traceability | 1 | S | P2 | BL-083 |
| BL-086 | SQL Server FTS setup: EN + AR word-breakers configured; FTS index on topics, decisions, MoMs (title, description, rationale, content columns) | EPIC-15 | Search&Traceability | 1 | M | P2 | BL-002 |
| BL-087 | Global search bar: accessible from every page; FTS across topics/decisions/MoMs; results grouped by artifact type with ID/title/excerpt/status/deep-link | EPIC-15 | Search&Traceability | 1 | M | P3 | BL-086 |

### Group J: Dashboards

| BL-ID | Work Item | EPIC | Module | Phase | Size | Priority | Depends-On |
|---|---|---|---|---|---|---|---|
| BL-088 | Committee dashboard: backlog summary by status/urgency, next meeting + agenda link, action counts, last 5 decisions | EPIC-14 | Reporting | 1 | M | P3 | BL-028, BL-043, BL-069, BL-057 |
| BL-089 | Secretary dashboard: triage queue, pending MoM approvals, overdue actions requiring escalation, aging topics | EPIC-14 | Reporting | 1 | M | P3 | BL-031, BL-053, BL-072 |
| BL-090 | Chairman dashboard: votes pending chairman approval, escalated risks, escalated actions, topics deferred ≥2 times | EPIC-14 | Reporting | 1 | M | P3 | BL-064, BL-077, BL-072 |

---

## Phase 2 — Governance Expansion

| BL-ID | Work Item | EPIC | Module | Phase | Size | Priority | Depends-On |
|---|---|---|---|---|---|---|---|
| BL-101 | Tarseem container: FastAPI wrapper around tarseem.generate(); internal HTTP endpoint; wired in Docker Compose | EPIC-18 | Diagrams | 2 | M | P2 | BL-001 |
| BL-102 | Diagram entity + migration: DGM-… IDs; JSON spec column; spec hash; engine version; status | EPIC-18 | Diagrams | 2 | S | P2 | BL-002 |
| BL-103 | Diagram render pipeline: Hangfire job → Tarseem sidecar → store artifacts via IFileStore; record spec hash | EPIC-18 | Diagrams | 2 | M | P2 | BL-101, BL-102, BL-006 |
| BL-104 | Diagram error surface: Tarseem error list (code, path, message, hint) displayed to author; no silent failures | EPIC-18 | Diagrams | 2 | S | P2 | BL-103 |
| BL-105 | Diagram version history + export: spec diff; all format downloads (SVG/PNG/PDF/draw.io/PPTX) | EPIC-18 | Diagrams | 2 | M | P2 | BL-103 |
| BL-106 | ADR entity + migration: ADR-… in-app IDs; MADR-lite template fields; lifecycle status | EPIC-17 | Governance | 2 | S | P2 | BL-002 |
| BL-107 | ADR creation + lifecycle API + UI: MADR-lite form; status transitions; supersession chain; immutability | EPIC-17 | Governance | 2 | M | P2 | BL-106 |
| BL-108 | ADR repository view: FTS (add ADR content to FTS index); filterable by status/date/author/stream | EPIC-17 | Governance | 2 | M | P2 | BL-107, BL-086 |
| BL-109 | Decision→ADR promotion: pre-fill from decision; bidirectional traceability link | EPIC-17 | Governance | 2 | S | P2 | BL-107, BL-057, BL-085 |
| BL-110 | Architecture Invariant entity + migration: AIV-… IDs; category, scope, statement, rationale, owner; lifecycle | EPIC-17 | Governance | 2 | S | P2 | BL-002 |
| BL-111 | Invariant creation + lifecycle + violation recording API + UI; invariant list with violation count | EPIC-17 | Governance | 2 | M | P2 | BL-110 |
| BL-112 | Webex adapter: INotificationChannel implementation for Adaptive Cards (v1.3, ≤80KB); 429 back-off via Hangfire retry | EPIC-13 | Notifications | 2 | M | P2 | BL-052, BL-006 |
| BL-113 | Webex recording metadata retrieval: Hangfire job post-meeting; store title/duration/downloadUrl in meeting record | EPIC-06 | Meetings | 2 | M | P2 | BL-043, BL-006 |
| BL-114 | Webex transcript retrieval: Webex Transcripts API + snippets; stored linked to meeting record; access-restricted | EPIC-06 | Meetings | 2 | M | P2 | BL-113 |
| BL-115 | Research Mission entity + migration: RMS-… IDs; title, description, linked topic, Keystone package ref, status | EPIC-19 | Research | 2 | S | P2 | BL-002 |
| BL-116 | Keystone import tool: parse Keystone package manifest → Finding (FND-…), Recommendation (REC-…), Risk (RSK-…) entities | EPIC-19 | Research | 2 | L | P2 | BL-115, BL-076 |
| BL-117 | Research Mission detail page + traceability links | EPIC-19 | Research | 2 | M | P3 | BL-116, BL-085 |
| BL-118 | Wiki page entity + migration: DOC-… IDs; Markdown content, category, versioned | EPIC-19 | Knowledge | 2 | S | P2 | BL-002 |
| BL-119 | Wiki editor UI: Markdown editor with preview; version history; diff view; add to FTS index | EPIC-19 | Knowledge | 2 | M | P2 | BL-118, BL-086 |
| BL-120 | Template entity + migration: TPL-… IDs; Markdown template with placeholder fields; artifact type tag | EPIC-19 | Knowledge | 2 | S | P2 | BL-002 |
| BL-121 | Template selection at artifact creation: pre-fills description/content field; editable | EPIC-19 | Knowledge | 2 | S | P2 | BL-120 |
| BL-122 | Dependency graph visualization: Tarseem dependency family; SQL graph traversal → JSON spec → render | EPIC-12 | Dependencies | 2 | M | P2 | BL-103, BL-078 |
| BL-123 | Impact analysis query: transitive blocked work via SQL graph traversal; configurable depth; navigable result list | EPIC-15 | Search&Traceability | 2 | M | P2 | BL-083 |
| BL-124 | Notification user preferences: per-user per-event-type opt-in/out page | EPIC-13 | Notifications | 2 | S | P3 | BL-052 |
| BL-125 | Notification digest Hangfire job: daily/weekly summary to opted-in users | EPIC-13 | Notifications | 2 | S | P3 | BL-124, BL-006 |
| BL-126 | Guest/Presenter time-boxed invitation: Secretary invites external presenter; view-only scoped to meeting/topic; auto-expire | EPIC-03 | Membership | 2 | M | P3 | BL-022, BL-021 |
| BL-127 | Conflict-of-interest flag per voter per vote: declaration recorded; no auto-exclusion | EPIC-09 | Decisions | 2 | S | P3 | BL-060 |
| BL-128 | Timeline/Gantt-lite view: topic date bars (created→target→scheduled→decided); pan/zoom | EPIC-05 | Topics | 2 | L | P3 | BL-028 |
| BL-129 | Per-stream report: topics/decisions/actions/risks by stream; filterable by date + status | EPIC-14 | Reporting | 2 | M | P2 | BL-028, BL-057, BL-069, BL-076 |
| BL-130 | Decision history report + CSV export; action completion rate trend chart; CSV/PNG export for all reports | EPIC-14 | Reporting | 2 | M | P2 | BL-057, BL-069 |
| BL-131 | Transitive impact analysis UI: display result list from BL-123 with navigable artifact links | EPIC-15 | Search&Traceability | 2 | S | P2 | BL-123 |
| BL-132 | Audit log export: CSV/JSON by date range + entity filter; Auditor/Administrator only | EPIC-16 | Audit&Records | 2 | S | P3 | BL-066 |
| BL-133 | Retention policy configuration: configurable period per entity type; no auto-purge in v1; Admin UI | EPIC-16 | Audit&Records | 2 | S | P3 | BL-066 |
| BL-134 | Diagram attachment to topics/ADRs/decisions via traceability relationship model | EPIC-18 | Diagrams | 2 | S | P2 | BL-103, BL-085 |
| BL-135 | Risk escalation notification: when risk status = Escalated, notify Secretary + Chairman | EPIC-11 | Risks | 2 | S | P3 | BL-077, BL-052 |
| BL-136 | Topic conversion workflow: convert topic to/from ADR/Research Mission; typed link created; Converted status | EPIC-04 | Topics | 2 | M | P3 | BL-030, BL-106 |
| BL-137 | Topic Reopen: Secretary reopens Closed/Rejected topic with recorded reason; re-enters triage | EPIC-04 | Topics | 2 | S | P3 | BL-030 |

---

## Phase 3 — Research & Knowledge

| BL-ID | Work Item | EPIC | Module | Phase | Size | Priority | Depends-On |
|---|---|---|---|---|---|---|---|
| BL-201 | Transcript FTS index: SQL Server FTS on transcript content; keyword search restricted to Chairman/Secretary/Auditor | EPIC-20 | Meetings | 3 | M | P2 | BL-114, BL-086 |
| BL-202 | AI candidate extraction: Secretary-triggered; LLM endpoint configurable; returns draft candidates; approval gate enforced; off by default | EPIC-20 | Meetings | 3 | L | P2 | BL-201 |
| BL-203 | AI extraction feature flag + privacy gate: Admin activation only; data-residency confirmation stored; activation log | EPIC-20 | Platform | 3 | S | P1 | BL-202 |
| BL-204 | Email adapter: implements INotificationChannel via SMTP; zero change to dispatch call site; activated by config | EPIC-20 | Notifications | 3 | M | P2 | BL-052 |
| BL-205 | KPI/health dashboard: avg topic-to-decision days by type, action SLA compliance %, backlog age distribution, vote-to-ratification time; configurable thresholds | EPIC-20 | Reporting | 3 | L | P2 | BL-088, BL-057, BL-069 |
| BL-206 | Traceability matrix CSV export: topic + all linked artifacts by type and relationship | EPIC-15 | Search&Traceability | 3 | M | P3 | BL-083 |
| BL-207 | Invariant exception request workflow: submit → Secretary review → Chairman approval | EPIC-17 | Governance | 3 | M | P3 | BL-111 |
| BL-208 | Bulk topic operations: Secretary defers/reassigns multiple topics at once | EPIC-04 | Topics | 3 | M | P4 | BL-030 |

---

## Build-Order Rationale

1. **Foundation first (BL-001–BL-016):** Nothing else can be built without containers, migrations, config, observability, Hangfire, MinIO, and i18n/RTL scaffolding. These are PH-1 P1 blockers with no value-bearing features.
2. **Identity before features (BL-017–BL-027):** RBAC + ABAC must be wired before any feature endpoint is exposed; streams must exist before topics can reference them.
3. **Audit log before vote/decision (BL-066–BL-067):** Append-only audit log and hash chain are a data-integrity prerequisite, not a reporting afterthought. Immutability must be in place before the first vote is cast.
4. **Topics before meetings (BL-028–BL-036):** Meetings reference Scheduled topics. Backlog views are high-value and can be built before meetings.
5. **In-app notification center (BL-052) before publish/notification events:** All notification dispatch goes through INotificationChannel; the concrete in-app adapter must exist before any event fires.
6. **Traceability edges before panels (BL-083–BL-085):** Relationship entity and CRUD must exist before detail-page panels can render.
7. **Voting after decisions (BL-057 before BL-060):** Votes are attached to decision records; decision entity is the anchor.
8. **PH-2 starts with Tarseem container (BL-101):** All diagram features, dependency graph, and Keystone diagrams depend on the render sidecar.

---

## Traceability

- Each BL-### → EPIC-## in `docs/38-epics-and-features.md`.
- BL-### → FR-### via EPIC FR lists (see `docs/07-functional-requirements.md`).
- BL-### phase assignments → `docs/36-roadmap.md` phase scope tables.
- Priority sequencing derives from dependency graph here and from value analysis in `docs/36-roadmap.md §Phase 1 Acceptance Criteria`.
- Blockers and risks on specific items → `docs/41-raid.md` (RISK-### + DEP-###).
