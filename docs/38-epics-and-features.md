# 38 — Epics and Features

**Purpose:** Defines the EPIC-## hierarchy for ACMP — one epic per major capability area — with goal, module(s), phase, key features, related FRs, and size. Covers Deliverable 46.

**Size key:** S = days; M = 1–2 weeks; L = 3–5 weeks; XL = 6+ weeks.

---

## Epic Index

| EPIC | Title | Module(s) | Phase | Size |
|---|---|---|---|---|
| EPIC-01 | Platform Foundation | Platform | 1 | XL |
| EPIC-02 | Identity & Access Management | Platform / Membership | 1 | L |
| EPIC-03 | Membership & User Management | Membership | 1 | M |
| EPIC-04 | Topic Intake & Lifecycle | Topics | 1 | XL |
| EPIC-05 | Backlog Management | Topics | 1 | L |
| EPIC-06 | Agenda & Meeting Management | Meetings | 1 | L |
| EPIC-07 | Minutes of Meeting (MoM) | Meetings | 1 | M |
| EPIC-08 | Decision Management | Decisions | 1 | L |
| EPIC-09 | Voting Engine | Decisions | 1 | M |
| EPIC-10 | Action Tracking | Actions | 1 | M |
| EPIC-11 | Risk Management | Risks | 1–2 | M |
| EPIC-12 | Dependency Management | Dependencies | 1–2 | M |
| EPIC-13 | Notifications & Alerts | Notifications | 1–2 | M |
| EPIC-14 | Dashboards & Reporting | Reporting | 1–2 | L |
| EPIC-15 | Search & Traceability | Search&Traceability | 1–2 | L |
| EPIC-16 | Audit & Records | Audit&Records | 1–2 | M |
| EPIC-17 | Governance — ADRs & Invariants | Governance | 2 | L |
| EPIC-18 | Tarseem Diagram Management | Diagrams | 2 | L |
| EPIC-19 | Research & Keystone Integration | Research / Knowledge | 2–3 | L |
| EPIC-20 | AI & Advanced Analytics | Meetings / Reporting | 3 | M |

---

## EPIC-01 — Platform Foundation

**Goal:** Stand up the production-grade application skeleton: containerized deployment, configuration management, observability, background jobs, file storage, database migrations, API documentation, and baseline UX infrastructure (i18n, RTL, themes, error handling).

**Modules:** Platform (Shared Kernel)

**Phase:** 1

**Size:** XL

**Key Features:**
- Docker Compose stack: ACMP app + SQL Server + Seq + MinIO containers; health/readiness probes wired.
- EF Core code-first migrations: apply on startup (non-prod) / CLI (prod); rollback documented.
- Configuration: all secrets via environment variables; no secrets in image layers.
- Structured logging: Serilog → self-hosted Seq; correlation ID + masked user ID on every log entry.
- Distributed tracing: OpenTelemetry for all HTTP requests and Hangfire jobs.
- App-owned Hangfire: on ACMP's own SQL schema; dashboard accessible to Administrator; retries, failure queue.
- SQL outbox: durable delivery guarantee for notification and render events.
- MinIO `IFileStore` abstraction: S3-compatible; pre-signed time-limited URLs; metadata in SQL.
- OpenAPI/Swagger: auto-generated; accessible in dev/staging.
- ASP.NET Core health checks: liveness + readiness endpoints.
- React + Vite + TypeScript scaffold: module layout, route guards, error boundaries.
- `react-i18next` EN/AR locale switching: all strings in locale files; locale persisted; switch without losing unsaved data.
- RTL layout: CSS logical properties throughout; `dir` attribute toggled; no LTR-only layouts.
- Light/dark theme: CSS variables; persisted per user session.
- Unsaved-work guard: confirmation prompt on navigation away from dirty forms.
- User-facing validation errors: localized (EN/AR); no technical detail exposed to end users.
- API error model: ProblemDetails RFC 7807; localized messages.

**Related FRs:** FR-001, FR-003–FR-015

---

## EPIC-02 — Identity & Access Management

**Goal:** Authenticate all users via Keycloak OIDC (authorization-code + PKCE); map Keycloak group/realm-role claims to ACMP canonical roles; enforce RBAC on every API endpoint and UI route; support per-topic ABAC capability checks.

**Modules:** Platform (Identity), Membership

**Phase:** 1

**Size:** L

**Key Features:**
- OIDC authorization-code + PKCE flow against Keycloak (ADR-0004).
- Auth service adapter: configuration-driven switch between Keycloak and org's legacy auth service during migration.
- Keycloak claim parser: maps group/realm-role claims → `Chairman | Secretary | Member | Reviewer | Auditor | Administrator | Submitter | Guest/Presenter`.
- Token validation middleware: signature, expiry, audience, issuer checks on every API request.
- Role-policy enforcement: ASP.NET Core policy-based authorization; unauthorized actions return 403 (never 401 after login).
- Per-topic ABAC: capability resolver checks `Owner | Assignee/Contributor | Presenter` relationship on the artifact; composable with global role policy.
- No public self-registration endpoint: `POST /register` does not exist.
- Role-aware navigation: frontend hides unauthorized nav items and action buttons (hidden, not disabled).
- Session management: token refresh via OIDC silent renew; session expiry redirects to login (no lost data).

**Related FRs:** FR-001–FR-002, FR-016, FR-018, FR-022, FR-024

---

## EPIC-03 — Membership & User Management

**Goal:** Enable Administrators to provision, manage, and deactivate committee members; define stream membership; surface the member directory.

**Modules:** Membership

**Phase:** 1

**Size:** M

**Key Features:**
- User provisioning: Administrator creates user (name, email, streams); global role sourced from Keycloak claims on first login.
- Stream assignment: multi-select streams per user; drives scope filtering throughout app.
- User deactivation: blocks login; preserves historical records (votes, authorship) intact and attributed.
- Member directory: paginated list of active members with name, role, stream membership, email; visible to all authenticated users.
- In-app notification on first login (no email in v1).
- Phase 2 extension: Guest/Presenter time-boxed invitation (FR-023).

**Related FRs:** FR-016–FR-021, FR-023 (PH-2)

---

## EPIC-04 — Topic Intake & Lifecycle

**Goal:** Allow authorized users to submit topics, manage their full lifecycle from Draft through Closed/Decided, and maintain a complete immutable status history with attachments and comments.

**Modules:** Topics

**Phase:** 1

**Size:** XL

**Key Features:**
- Topic submission form: title, description (Markdown), type (4 canonical types), urgency, source, scope, affected streams (≥1), affected systems/services, target date.
- System-generated canonical ID: `TOP-YYYY-###` on creation.
- File attachment upload: via `IFileStore`; metadata in SQL; linked to topic in traceability model.
- Comment thread: timestamped, attributed; immutable after posting.
- Triage workflow: Secretary accepts (assigns/confirms owner), rejects (mandatory rationale), or defers (mandatory reason + re-evaluation date).
- Status state machine: `Draft → Submitted → Triage → Accepted → Prepared → Scheduled → InCommittee → Decided → Closed`; side states `Rejected, Deferred, Reopened, Converted`.
- Edit lock: Owner/Secretary editable in Draft/Submitted/Triage; post-Accepted only Secretary edits metadata; content locked.
- Rejection/deferral immutability: reason + actor recorded as immutable event in history.
- Topic detail page: all fields, status history with timestamps + actors, linked artifacts, traceability graph excerpt.
- Aging indicator: visual badge + secretary notification when topic exceeds urgency-based SLA (Critical: 3d, Urgent: 7d, Normal: 21d [unverified — OQ-SLA-URGENCY]).
- Prepared + Scheduled status transitions (Secretary).
- Phase 2 extensions: topic conversion workflow (FR-030), topic templates (FR-041), Reopen (FR-045), Gantt-lite view (FR-036).

**Related FRs:** FR-025–FR-029, FR-038–FR-040, FR-042–FR-044; FR-030, FR-036, FR-041, FR-045 (PH-2)

---

## EPIC-05 — Backlog Management

**Goal:** Give secretarys and members multiple views of the topic backlog with drag-and-drop reprioritization, filtering, and urgency-aware visual indicators.

**Modules:** Topics

**Phase:** 1

**Size:** L

**Key Features:**
- List view: title, type, urgency badge, status, owner, affected streams, created date, last-updated; sortable/filterable by all fields.
- Table/dense view: all topic fields as columns; user-configurable column visibility/order per session.
- Kanban view: status columns; topic cards; DnD within column = priority reorder; DnD between columns = status change (with permission check); `@dnd-kit` (ADR-0012).
- Calendar view: scheduled-date-based; weekly/monthly layout; meeting calendar integration.
- DnD reprioritization: explicit ordinal stored; tied priorities broken by submission date.
- Keyboard-accessible alternative to DnD: move-up/move-down controls (WCAG 2.2 AA, FR-034).
- Aging indicators visible on all views (badge, color, tooltip with SLA deadline).
- Phase 2 extension: timeline/Gantt-lite view (FR-036).
- Phase 3 extension: bulk topic operations (D-10).

**Related FRs:** FR-031–FR-035, FR-037–FR-038; FR-036 (PH-2)

---

## EPIC-06 — Agenda & Meeting Management

**Goal:** Enable secretarys to create and publish meeting agendas, record meeting metadata, track attendance, and capture live meeting notes.

**Modules:** Meetings

**Phase:** 1

**Size:** L

**Key Features:**
- Agenda creation (AGN-YYYY-###): select Scheduled topics, set order (DnD + keyboard alternative), time-box per item, assign presenter.
- Carry-over auto-suggest: unresolved agenda items from previous meeting surfaced on new agenda creation.
- Agenda publish: in-app notification to all committee members with deep link; Webex Adaptive Card = Phase 2.
- Meeting record (MTG-YYYY-###): date, start/end time, type (regular/extraordinary/emergency), mode (in-person/remote/hybrid), agenda reference.
- Attendance tracking: Secretary marks each member Present/Absent/Remote; persisted; used for quorum.
- Live notes per agenda item: free-text Markdown; auto-saved on typing pause (debounce ≤2s).
- Meeting calendar view: past + upcoming meetings with status badges.
- Manual recording attachment: Secretary pastes link or uploads recording file.
- Phase 2 extension: Webex recording metadata retrieval via API (FR-057); transcript retrieval (FR-058).

**Related FRs:** FR-046–FR-052, FR-056, FR-061; FR-057–FR-058 (PH-2)

---

## EPIC-07 — Minutes of Meeting (MoM)

**Goal:** Automate MoM generation from meeting data, support versioned review/approval, and distribute approved minutes to the committee.

**Modules:** Meetings

**Phase:** 1

**Size:** M

**Key Features:**
- MoM generation (MIN-YYYY-###): compile attendance, agenda items (ordered), per-item notes, decisions, and actions into structured Markdown document.
- Versioning: each save creates an immutable version; previous versions viewable.
- Approval workflow: Secretary drafts → Reviewer(s)/Chairman review and annotate → Chairman approves → content locked (immutable).
- Distribution: in-app notification to all committee members on approval with deep link to published MoM; Webex Adaptive Card = Phase 2.
- Phase 3 extension: AI candidate extraction from transcript (FR-060).

**Related FRs:** FR-053–FR-055; FR-059–FR-060 (PH-3)

---

## EPIC-08 — Decision Management

**Goal:** Record committee decisions with canonical outcomes, full rationale capture, supersession chain, and downstream traceability links; enforce immutability once issued.

**Modules:** Decisions

**Phase:** 1

**Size:** L

**Key Features:**
- Decision record (DECN-YYYY-###): outcome (canonical list enforced), rationale (required), conditions (for ConditionallyApproved), authority actor, effective date.
- Canonical outcome enforcement: dropdown from 11 fixed outcomes; no free-text field.
- Alternatives considered: structured list (alternative name + reason not chosen).
- Supersession: new decision links to superseded one; superseded marked Superseded, never deleted.
- Immutability: decision content locked once Issued; corrections via new superseding decision.
- Downstream link requirement: ≥1 downstream artifact link required before marking Issued.
- Decision history list: filterable by outcome, topic, stream, date range, chairman.
- Phase 2 extension: Decision→ADR promotion (FR-068).

**Related FRs:** FR-062–FR-067, FR-069; FR-068 (PH-2)

---

## EPIC-09 — Voting Engine

**Goal:** Provide an attributed, quorum-enforced voting mechanism with chairman approval, immutable audit trail, and full vote lifecycle management.

**Modules:** Decisions (Voting)

**Phase:** 1

**Size:** M

**Key Features:**
- Vote configuration: eligible voter list, voting options (Approve/Reject; or Approve/ConditionallyApprove/Reject; or custom), quorum threshold, abstention flag. Always attributed in v1.
- Quorum enforcement: vote cannot close if eligible-voters-present < quorum threshold.
- One-vote-per-voter enforcement: double-voting prevented at API layer.
- Vote lifecycle: `Configured → Open → Closed → Ratified`; no backward transitions.
- Chairman action: Confirm (ratify majority), Override (select different outcome + mandatory reason), or Abstain-from-override; recorded explicitly and immutably.
- Vote audit: all individual choices, timestamps, and chairman action in append-only audit log; immutable after close.
- In-app notification to eligible voters on vote open.
- Phase 2 extension: conflict-of-interest flag per voter per vote (FR-076).

**Related FRs:** FR-070–FR-075, FR-077–FR-078; FR-076 (PH-2)

---

## EPIC-10 — Action Tracking

**Goal:** Create, track, remind, escalate, and verify action items linked to topics/decisions/risks; derive Overdue state automatically; provide a cross-topic actions dashboard.

**Modules:** Actions

**Phase:** 1

**Size:** M

**Key Features:**
- Action creation (ACT-…): title, owner (required), due date (required), description, priority; linked to topic/decision/risk.
- API-layer enforcement: owner + due date are required; no action without them.
- Status tracking: `Open → InProgress → Blocked → Completed`; Verified (by Secretary/Chairman); Cancelled (with mandatory reason); Overdue derived.
- Overdue derivation: background derived state; due date < today and status ∉ {Completed, Verified, Cancelled}.
- Progress notes: free-text, timestamped, by owner.
- Reminder job (Hangfire): notification to owner N days before due (N configurable, default 3).
- Escalation job (Hangfire): notification to Secretary + Chairman when Overdue > threshold (default 2 days past due).
- Completion validation: Secretary/Chairman marks Completed → Verified with actor + timestamp.
- Cancellation: Secretary cancels with mandatory reason; record retained.
- Actions dashboard: all open/blocked/overdue across all topics; filterable by owner, stream, topic, date range, status.
- Per-topic action list on topic detail page.

**Related FRs:** FR-079–FR-088

---

## EPIC-11 — Risk Management

**Goal:** Record, track, and escalate risks linked to topics and actions; enforce mitigation plan before close; surface risk status on topic detail page.

**Modules:** Risks

**Phase:** 1 (basic); 2 (escalation notification)

**Size:** M

**Key Features:**
- Risk record (RSK-…): title, description, likelihood (H/M/L), impact (H/M/L), owner (required), mitigation plan, linked topic/action.
- Mitigation plan required: system prevents Close without mitigation plan or explicit Accepted status + rationale.
- Status lifecycle: `Open → Mitigating → Closed`; side states `Accepted`, `Escalated`.
- Per-topic risk list: on topic detail page with likelihood/impact matrix view.
- Phase 2 extension: escalation notification to Secretary/Chairman when risk is Escalated (FR-092).

**Related FRs:** FR-089–FR-091, FR-093; FR-092 (PH-2)

---

## EPIC-12 — Dependency Management

**Goal:** Model typed dependency edges between governance artifacts; flag cross-stream dependencies; provide impact analysis and graph visualization.

**Modules:** Dependencies

**Phase:** 1 (basic edges); 2 (graph, impact analysis)

**Size:** M

**Key Features:**
- Dependency edge (DPN-…): source → target, type (`Blocks | DependsOn | RelatesTo`), description; between topics, actions, systems.
- Cross-stream flag: visual highlight when source and target entities affect different streams.
- Per-topic dependency list: inbound + outbound edges on topic detail page.
- Phase 2 extensions: impact analysis query (FR-096) — transitive blocked work via SQL graph traversal; dependency graph visualization via Tarseem dependency family (FR-097).

**Related FRs:** FR-094–FR-095, FR-098; FR-096–FR-097 (PH-2)

---

## EPIC-13 — Notifications & Alerts

**Goal:** Dispatch all committee notifications via the INotificationChannel abstraction; deliver in-app notification center in v1; add Webex Adaptive Card adapter in PH-2; support user preferences and digest in PH-2.

**Modules:** Notifications

**Phase:** 1 (in-app center); 2 (Webex adapter, preferences, digest)

**Size:** M

**Key Features:**
- `INotificationChannel` abstraction: single dispatch point; concrete implementations are plug-in adapters.
- In-app notification center: bell icon + notification list; deep-link per notification; mark-as-read.
- Event catalog (v1): topic status change, agenda published, vote opened, vote closed, MoM published, action reminder, action overdue, action escalation, risk escalation.
- Deep links: every notification navigates to the relevant artifact.
- SQL outbox: durable delivery (notification published to outbox within transaction; Hangfire job delivers).
- Webex 429 back-off: Hangfire retry with Retry-After header honoring (abstraction layer; needed even in v1 for future-proofing).
- Phase 2 extensions:
  - Webex Adaptive Card adapter (v1.3 compliant, ≤80KB cards, ≤10 image links per card).
  - Notification user preferences: per-user per-event opt-in/out.
  - Daily/weekly digest Hangfire job.
- Email adapter: deferred until SMTP relay confirmed (Phase 3).

**Related FRs:** FR-129–FR-132; FR-133–FR-134 (PH-2)

---

## EPIC-14 — Dashboards & Reporting

**Goal:** Provide role-tailored dashboards (committee/secretary/chairman) in PH-1; expand with per-stream, decision history, and action trend reports in PH-2; KPI health dashboard in PH-3.

**Modules:** Reporting

**Phase:** 1 (role dashboards); 2 (expanded reports); 3 (KPI)

**Size:** L

**Key Features:**
- Committee dashboard: backlog summary by status/urgency, next meeting + agenda link, open/overdue action counts, last 5 decisions.
- Secretary dashboard: triage queue, pending MoM approvals, overdue actions requiring escalation, aging topics.
- Chairman dashboard: votes pending approval, escalated risks, escalated actions, topics deferred ≥2 times.
- Phase 2 extensions:
  - Per-stream report: topics/decisions/actions/risks by stream, filterable by date + status.
  - Decision history report: date range, outcome, stream filters; CSV export.
  - Action completion rate trend chart (weekly/monthly): open vs. closed vs. overdue over time.
  - All tabular reports: CSV export; all charts: PNG export.
- Phase 3 extension: KPI/health dashboard — avg topic-to-decision days by type, action SLA compliance %, backlog age distribution, vote-to-ratification time.

**Related FRs:** FR-135–FR-137; FR-138–FR-140, FR-142 (PH-2); FR-141 (PH-3)

---

## EPIC-15 — Search & Traceability

**Goal:** Provide bilingual full-text search across all governed artifacts and a typed traceability graph that lets any user navigate upstream/downstream relationships from any artifact.

**Modules:** Search & Traceability

**Phase:** 1 (global search + traceability panel); 2 (impact analysis); 3 (matrix export)

**Size:** L

**Key Features:**
- Global search bar on every page: SQL Server FTS across topics, decisions, ADRs, MoMs, wiki pages.
- EN + AR queries: FTS language configuration for both word-breakers.
- Results grouped by artifact type: ID, title, excerpt, status, deep link.
- Traceability panel on every artifact: typed upstream + downstream relationships.
- Typed relationship management: create/read/delete edges; types: `DerivedFrom, Supersedes, Implements, Resolves, References, Blocks, DependsOn, RelatesTo`.
- Phase 2 extension: transitive impact analysis query (configurable depth, default 3 hops); navigable result list.
- Phase 3 extension: traceability matrix CSV export for a topic.

**Related FRs:** FR-143–FR-147; FR-148 (PH-2); FR-149 (PH-3)

---

## EPIC-16 — Audit & Records

**Goal:** Maintain an append-only, immutable audit log for all governed entities; provide search and export; enforce no-purge retention in v1.

**Modules:** Audit & Records

**Phase:** 1 (log + search); 2 (export + retention config)

**Size:** M

**Key Features:**
- Append-only audit log: every state transition + field update (entity type, ID, action, actor, timestamp UTC, before-state JSON, after-state JSON, correlation ID).
- Immutability enforcement: no UPDATE/DELETE on audit rows in code; DB-level constraints or trigger backup.
- Hash chain: SHA-256 chained hash over vote and issued-decision records (RISK-003 mitigation; ADR-0009).
- Auditor/Administrator: search audit log by entity type, entity ID, actor, action type, date range; paginated.
- Structured logging (Serilog → Seq) and OTel traces visible to Administrator.
- Phase 2 extensions:
  - Audit log CSV/JSON export for date range + entity filter (Auditor/Administrator only).
  - Retention policy configuration: configurable period per entity type; no auto-purge in v1.

**Related FRs:** FR-150–FR-153; FR-154–FR-155 (PH-2)

---

## EPIC-17 — Governance — ADRs & Invariants

**Goal:** Provide an in-app ADR repository with full lifecycle and supersession, and an Architecture Invariant registry with violation tracking.

**Modules:** Governance

**Phase:** 2

**Size:** L

**Key Features:**
- ADR creation (ADR-… in-app IDs): MADR-lite template; title, status, context, decision, consequences, alternatives, date.
- ADR lifecycle: `Draft → Proposed → Approved → (Superseded | Deprecated)`.
- ADR supersession: immutable back-link; both records preserved.
- ADR repository: searchable list (SQL FTS on content), filterable by status/date/author/stream.
- ADR Markdown rendering + raw download.
- Decision→ADR promotion: pre-fill from decision record; bidirectional link.
- ADR template management in Knowledge module.
- Architecture Invariant (AIV-…): category, scope, statement, rationale, owner.
- Invariant lifecycle: `Draft → Proposed → Active → (Retired | Superseded)`.
- Violation recording: description, discovering entity, severity, remediation link.
- Invariant list: active invariants with violation count; filterable by category/scope/severity.
- Traceability links: ADR ↔ decision ↔ topic ↔ invariant in graph.
- Phase 3 extension: invariant exception request workflow (FR-110).

**Related FRs:** FR-099–FR-109; FR-110 (PH-3)

---

## EPIC-18 — Tarseem Diagram Management

**Goal:** Enable creation and management of architecture diagrams via Tarseem JSON spec; render via containerized sidecar; version specs; export artifacts; attach to governed artifacts.

**Modules:** Diagrams

**Phase:** 2

**Size:** L

**Key Features:**
- Diagram (DGM-…): JSON spec submission conforming to Tarseem v1.0 schema; spec stored as version-controlled JSON column in SQL Server.
- Render pipeline: Hangfire job submits spec to Tarseem sidecar (FastAPI wrapper or CLI); stores generated SVG/PNG/PDF/draw.io/PPTX via `IFileStore`; records spec hash + engine version.
- Re-render on spec update: previous artifact version retained in history.
- Error surface: Tarseem error list (code, path, message, hint) shown to diagram author; no silent failures.
- Export download: all Tarseem export formats from diagram detail page.
- Version history: spec diff between versions, render timestamp, artifact hash, engine version.
- Attachment to topics (TOP-…), ADRs (ADR-…), decisions (DECN-…) via traceability relationship model.
- Dependency graph visualization (EPIC-12 PH-2 items) rendered via Tarseem dependency family.

**Related FRs:** FR-121–FR-128

---

## EPIC-19 — Research & Keystone Integration + Knowledge Base

**Goal:** Enable Research Missions with optional Keystone package import; provide a wiki/knowledge base and template management.

**Modules:** Research, Knowledge

**Phase:** 2 (PH-3 for advanced analytics)

**Size:** L

**Key Features:**
- Research Mission (RMS-…): title, description, linked topic, Keystone package reference, status.
- Keystone import: parse structured package artifacts → Finding (FND-…), Recommendation (REC-…), Risk (RSK-…); map to ACMP domain entities.
- Finding + Recommendation as first-class artifacts: individually viewable, linkable in traceability graph.
- Research Mission detail page: Keystone package reference, import status, findings/recommendations/risks, linked ACMP artifacts.
- Research Mission → Topic/Decision traceability links.
- Wiki pages (DOC-…): Markdown, title, category, cross-links; versioned (each save = immutable version; diff view).
- Wiki FTS: indexed in SQL Server FTS alongside other artifacts.
- Templates (TPL-…): create/edit/delete for Topics (by type), MoM, ADR, Research Mission; Markdown with placeholder fields; selectable at artifact creation.

**Related FRs:** FR-111–FR-120

---

## EPIC-20 — AI & Advanced Analytics

**Goal:** Add human-reviewed AI candidate extraction from transcripts; email notification channel; KPI health dashboard; and traceability matrix export.

**Modules:** Meetings, Notifications, Reporting, Search & Traceability

**Phase:** 3

**Size:** M

**Key Features:**
- AI candidate extraction: Secretary-triggered; LLM endpoint configurable; returns candidate action items and decision summaries as draft proposals; no content enters the record without explicit Secretary approval (P-05, OWASP LLM01).
- Feature flag: AI extraction is OFF by default; Admin must activate; activation requires privacy/data-residency review sign-off.
- Transcript FTS: keyword search across indexed transcripts (Chairman/Secretary/Auditor access).
- Email adapter: implements `INotificationChannel`; activated when SMTP relay confirmed; zero code change to notification dispatch.
- KPI/health dashboard: avg topic-to-decision days by type, action SLA compliance %, backlog age distribution, vote-to-ratification time; configurable threshold baselines.
- Traceability matrix CSV export: table of topic + all linked artifacts by type and relationship.
- Invariant exception workflow (FR-110, if not delivered in PH-2).
- Bulk topic operations for Secretary (D-10).

**Related FRs:** FR-059–FR-060, FR-110, FR-141, FR-149; email items in FR-130

---

## Traceability

- Each EPIC-## → `docs/37-implementation-backlog.md` (BL-### items within each epic).
- Each EPIC-## → FR-### cited above → `docs/07-functional-requirements.md`.
- Phase assignments → `docs/36-roadmap.md` (phase scope tables).
- EPIC-## → `docs/39-user-stories-mvp.md` (US-### decomposition for PH-1 epics).
- EPIC-## → `docs/40-acceptance-criteria.md` (AC-### per epic per phase).
