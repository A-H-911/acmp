# 06 — Scope and Out-of-Scope

**Purpose:** Defines what ACMP will build, organized by canonical module and delivery phase, and explicitly delineates what is excluded and why. Covers Deliverables 8 and 9.

---

## In-Scope Capabilities

Capabilities are organized by canonical module (README §B) and phase. Phase definitions follow `docs/planning/roadmap.md`:

- **Phase 1 (MVP):** Core governance loop — topic intake → backlog → agenda → meeting → voting → decision → action tracking. Identity/membership. Basic reporting. Notifications (in-app notification center only).
- **Phase 2:** ADRs, Architecture Invariants, Risk management, Dependency graph, Keystone research integration, Tarseem diagram management, Template management, Knowledge/wiki, advanced Traceability graph UI, enhanced dashboards.
- **Phase 3:** Advanced reporting/KPI, MoM transcript AI extraction, advanced search (full-text transcript search), audit export, external data-source integrations (if approved), optional UX enhancements (guided onboarding, saved views, bulk operations).

### Capability Catalog

| # | Capability | Module | Phase | Notes |
|---|---|---|---|---|
| 1 | OIDC login via Keycloak; adapter for existing internal auth service during transition | Platform (Identity) | 1 | ADR-0004; no public self-registration |
| 2 | User provisioning by Administrator (invite/create); no self-registration | Membership | 1 | Secretary/Admin-initiated |
| 3 | Global RBAC role assignment: Chairman, Secretary, Member, Reviewer, Auditor, Administrator, Submitter, Guest/Presenter | Membership | 1 | README §C; full matrix in `docs/domain/permission-role-matrix.md` |
| 4 | Per-topic ABAC capabilities: Owner, Assignee/Contributor, Presenter | Membership | 1 | Relationship-based, not global role |
| 5 | Stream membership management (which users belong to which streams) | Membership | 1 | Required for scope/filter by stream |
| 6 | Committee member directory (profile, role, stream membership, contact) | Membership | 1 | Read-only view for committee members |
| 7 | Guest/Presenter access: time-boxed invitation, limited view | Membership | 2 | Phase 2: invite external presenters |
| 8 | Topic submission by Submitter or Member: title, description, type, source, scope, urgency, affected streams/systems, attachments | Topics | 1 | brief §6.2; digest §3 |
| 9 | Topic intake triage by Secretary: accept/reject/defer/convert, assign owner, set type and urgency | Topics | 1 | Status: Draft→Submitted→Triage→Accepted|Rejected|Deferred |
| 10 | Topic edit by owner (while in Draft/Submitted/Triage) | Topics | 1 | Editable pre-acceptance; locked after scheduling |
| 11 | Topic backlog list view with sorting, filtering (status, type, urgency, stream, phase, owner, date range) | Topics | 1 | brief §6.3 |
| 12 | Topic backlog table view (dense, all fields as columns, inline status badge) | Topics | 1 | brief §6.3 |
| 13 | Topic backlog kanban view (status columns, drag-and-drop reorder within column) | Topics | 1 | DnD via @dnd-kit (ADR-0012); keyboard-accessible alternative |
| 14 | Topic backlog calendar view (scheduled-date-based, weekly/monthly) | Topics | 1 | brief §6.3 |
| 15 | Topic backlog timeline/Gantt-lite view (created→target→scheduled→decided date bars) | Topics | 2 | brief §6.3; deferred from MVP for complexity |
| 16 | Topic drag-and-drop reprioritization across all backlog views | Topics | 1 | @dnd-kit; priority stored as explicit ordinal |
| 17 | Topic aging indicator (visual warning when topic has been in same status beyond SLA thresholds by urgency) | Topics | 1 | brief §6.3; urgency SLA table in `docs/domain/topic-taxonomy.md` |
| 18 | Topic detail page: all fields, history, linked artifacts, traceability graph excerpt | Topics | 1 | Full field list from digest §3 |
| 19 | Attach files/presentations to topic (upload to S3-compatible store; metadata in SQL) | Topics | 1 | `IFileStore` abstraction; open decision OQ on provider |
| 20 | Topic comments/discussion thread (internal, not public) | Topics | 1 | brief §6 |
| 21 | Topic conversion: convert topic to/from ADR, Research Mission, or sub-topic | Topics | 2 | Status: Converted; brief §E |
| 22 | Topic template selection on creation (by type) | Topics | 2 | Depends on Template module (Phase 2) |
| 23 | Bulk status update for Secretary (e.g., defer multiple topics at once) | Topics | 3 | Phase 3 as convenience feature |
| 24 | Agenda creation for a meeting: select topics from backlog, set order, time-box each item | Meetings | 1 | AGN-YYYY-### IDs |
| 25 | Agenda drag-and-drop reorder | Meetings | 1 | @dnd-kit |
| 26 | Carry-over items from previous agenda to new agenda (auto-suggest unfinished items) | Meetings | 1 | brief §6.4 |
| 27 | Assign presenter per agenda item | Meetings | 1 | Per-topic ABAC: Presenter role |
| 28 | Publish agenda to committee members (notification via adapter) | Meetings | 1 | Triggers in-app notification (v1); Webex card = Phase 2 |
| 29 | Meeting record creation (MTG-YYYY-###): date, time, type, mode, attendees, agenda ref | Meetings | 1 | brief §6.5 |
| 30 | Attendance tracking: mark present/absent/remote per member per meeting | Meetings | 1 | Required for quorum calculation |
| 31 | Live meeting notes per agenda item (free-text, Markdown) | Meetings | 1 | brief §6.5 |
| 32 | MoM (Minutes of Meeting) generation: compile attendance, agenda items, notes, decisions, actions into structured document | Meetings | 1 | MIN-YYYY-### IDs; brief §6.5 |
| 33 | MoM versioning and approval workflow (Secretary drafts → Chairman/reviewers approve → published) | Meetings | 1 | brief §6.5 |
| 34 | MoM distribution to committee members on approval (in-app notification; Webex card = Phase 2) | Meetings | 1 | brief §6.5 |
| 35 | Webex meeting recording link/metadata retrieval and storage in meeting record | Meetings | 2 | Webex Recordings API; digest §5.3 |
| 36 | Webex meeting transcript retrieval and storage (requires Webex Assistant enabled by host) | Meetings | 2 | Constrained by Webex Assistant requirement; digest §5.3 |
| 37 | Transcript full-text search (indexed in SQL Server FTS) | Meetings | 3 | Phase 3; depends on transcript availability |
| 38 | AI candidate extraction from transcripts: suggested action items and decision summaries for human review | Meetings | 3 | Candidates only; human approval required (P-05); brief §6.5 |
| 39 | Decision record creation (DECN-YYYY-###): outcome, rationale, conditions, authority, linked topic, linked vote | Decisions | 1 | README §E; full outcome list |
| 40 | Decision outcome selection from canonical list (Approved, ConditionallyApproved, Rejected, MoreInfoRequired, FeedbackProvided, EnhancementsRequired, DesignChangesRequired, ResearchRequired, Deferred, Escalated, Converted) | Decisions | 1 | Immutable once issued (ADR-0009) |
| 41 | Decision rationale capture: alternatives considered, conditions (for conditional approval), supersedes reference | Decisions | 1 | brief §6.6 |
| 42 | Decision supersession: create superseding decision linked to original; original marked Superseded (not deleted) | Decisions | 1 | ADR-0009; immutability |
| 43 | Decision-to-ADR promotion: convert a decision into a formal ADR | Decisions | 2 | Links Decision→ADR in traceability graph |
| 44 | Voting configuration per topic: eligible voters, voting options, quorum threshold, abstention allowed, anonymity flag | Decisions | 1 | ADR-0010; brief §6.7 |
| 45 | Vote recording: each eligible voter casts a vote (or abstains); immutable after submission | Decisions | 1 | VOTE-… IDs; ADR-0009 |
| 46 | Quorum check: system enforces quorum before closing vote | Decisions | 1 | ADR-0010 |
| 47 | Chairman final approval/override: chairman confirms, overrides, or abstains from override — recorded explicitly | Decisions | 1 | Chairman has stronger authority (digest §3) |
| 48 | Conflict-of-interest flag per voter per vote (recorded, does not auto-exclude but is visible) | Decisions | 2 | brief §6.7 |
| 49 | Voting is always attributed (no anonymity in v1); each ballot recorded against the voter | Decisions | 1 | ADR-0010 |
| 50 | Vote audit trail: full immutable record of all votes, timestamps, and chairman action (visible to Auditor/Chairman always) | Decisions | 1 | ADR-0009 |
| 51 | Action creation (ACT-…): title, owner, due date, priority, linked topic/decision/risk | Actions | 1 | brief §6.8 |
| 52 | Action status tracking: Open→InProgress→Blocked→Completed→Verified; side states Cancelled, Overdue (derived) | Actions | 1 | README §E |
| 53 | Action progress update by owner (free-text notes, % complete) | Actions | 1 | brief §6.8 |
| 54 | Overdue action detection: derive Overdue when due date passed and status not Completed/Verified/Cancelled | Actions | 1 | Hangfire background job |
| 55 | Action reminder notification to owner (configurable days-before-due) | Actions | 1 | Hangfire + notification adapter |
| 56 | Action escalation notification to Secretary/Chairman when overdue beyond threshold | Actions | 1 | Hangfire + notification adapter |
| 57 | Action completion validation by Secretary or Chairman (transition to Verified) | Actions | 1 | brief §6.8 |
| 58 | Action dashboard: open/overdue/blocked actions across all topics with owner filter | Actions | 1 | brief §6.8 |
| 59 | Risk record creation (RSK-…): title, description, likelihood, impact, owner, linked topic/decision | Risks | 1 | brief §6.9 |
| 60 | Risk status tracking: Open→Mitigating→Closed; side states Accepted, Escalated | Risks | 1 | README §E |
| 61 | Risk mitigation plan capture | Risks | 1 | brief §6.9 |
| 62 | Risk escalation to committee | Risks | 2 | Notification + status change |
| 63 | Dependency record creation (DPN-…): source entity → target entity, type (blocks/depends-on/relates-to), description | Dependencies | 1 | brief §6.10 |
| 64 | Dependency graph visualization (Tarseem dependency family) | Dependencies | 2 | Renders JSON spec via Tarseem sidecar |
| 65 | Cross-stream dependency detection: flag when a topic/action depends on work in another stream | Dependencies | 1 | Required field: affected streams on topic |
| 66 | Blocked-work impact analysis: given a blocked dependency, surface all downstream topics/actions | Dependencies | 2 | SQL graph traversal (ADR-0008) |
| 67 | ADR creation (in-app ADR-… IDs, distinct from planning-package ADR-####): title, status, context, decision, consequences, alternatives | Governance | 2 | MADR-lite template (digest §5.5); brief §6.11 |
| 68 | ADR status lifecycle: Draft→Proposed→Approved→(Superseded|Deprecated) | Governance | 2 | README §E |
| 69 | ADR supersession: link superseding ADR to superseded one | Governance | 2 | ADR-0009; immutability |
| 70 | ADR repository view: searchable, filterable list; full-text search on content | Governance | 2 | SQL FTS |
| 71 | ADR template management (MADR-lite; editable by Secretary/Admin) | Governance | 2 | Depends on Template module |
| 72 | Architecture Invariant creation (AIV-…): category, scope, statement, rationale, owner | Governance | 2 | brief §6.12 |
| 73 | Invariant status lifecycle: Draft→Proposed→Active→(Retired|Superseded) | Governance | 2 | README §E |
| 74 | Invariant violation tracking: record a violation event, link to topic/decision, track remediation | Governance | 2 | brief §6.12 |
| 75 | Invariant exception request and approval workflow | Governance | 3 | Complex workflow; defer from Phase 2 unless prioritized |
| 76 | Research Mission import from Keystone package (RMS-…): structured artifacts — findings, recommendations, decisions | Research | 2 | ADR-0007; digest §5.2 |
| 77 | Research Mission detail page: linked Keystone package ref, imported manifest, finding/recommendation list | Research | 2 | brief §6.14 |
| 78 | Finding (FND-…) and Recommendation (REC-…) record management | Research | 2 | First-class entities; linkable in traceability graph |
| 79 | Research Mission → Topic/Decision traceability links | Research | 2 | Traceability graph |
| 80 | Template creation and management (TPL-…): Markdown templates by artifact type | Knowledge | 2 | brief §6.13; used by Topics, ADRs, MoMs |
| 81 | Wiki / knowledge base: Markdown pages (DOC-…), versioned, cross-linked, organized by category | Knowledge | 2 | brief §6.14; Markdown with history |
| 82 | Wiki search (SQL FTS on content) | Knowledge | 2 | brief §6.14 |
| 83 | Diagram creation from JSON spec (DGM-…): spec stored as version-controlled JSON; rendered via Tarseem sidecar | Diagrams | 2 | ADR-0006; digest §5.1 |
| 84 | Diagram re-render on spec update; artifact (SVG/PNG/PDF/drawio/pptx) regenerated with spec hash | Diagrams | 2 | Hangfire job; artifacts as attachments |
| 85 | Diagram attachment to Topic/ADR/Decision via relationship model | Diagrams | 2 | Traceability graph |
| 86 | Diagram export download (SVG, PNG, PDF, draw.io, PPTX) | Diagrams | 2 | Tarseem exports; digest §5.1 |
| 87 | Diagram version history: spec diffs, previous artifact retrieval | Diagrams | 2 | JSON spec as source of truth |
| 88 | Notification dispatch via `INotificationChannel` abstraction: **in-app notification center (v1)**; Webex Adaptive Cards adapter (Phase 2); email deferred until SMTP relay available | Notifications | 1 | ADR-0005; no org notification platform |
| 89 | Notification event catalog: topic status changes, vote open/close, action reminders, MoM published, action overdue, escalation | Notifications | 1 | brief §6.17; `docs/domain/notification-strategy.md` |
| 90 | User notification preferences (per-user per-event opt-in/out) | Notifications | 2 | Phase 2: preference management |
| 91 | Notification digest (daily/weekly summary) via Hangfire scheduled job | Notifications | 2 | brief §6.17 |
| 92 | Committee dashboard: open topics by status/urgency, upcoming meeting, open actions, overdue actions, recent decisions | Reporting | 1 | brief §6.16; high priority |
| 93 | Secretary dashboard: triage queue, pending MoMs, overdue actions requiring escalation | Reporting | 1 | brief §6.16 |
| 94 | Chairman dashboard: vote pending approval, escalated risks, aging topics | Reporting | 1 | brief §6.16 |
| 95 | Per-stream reporting: topics, decisions, actions, risks affecting a given stream | Reporting | 2 | brief §6.16 |
| 96 | Decision history report: all decisions in date range, filterable by outcome and stream | Reporting | 2 | brief §6.16 |
| 97 | Action completion rate over time (trend chart) | Reporting | 2 | brief §6.16 |
| 98 | KPI dashboard: committee health indicators (aging backlog, decision turnaround, action SLA compliance) | Reporting | 3 | `docs/domain/metrics-kpi-catalog.md` |
| 99 | Full-text search across topics, decisions, ADRs, wiki, MoMs (SQL FTS) | Search&Traceability | 1 | ADR-0011 |
| 100 | Typed traceability graph: navigate upstream/downstream links from any artifact | Search&Traceability | 1 | ADR-0008; `docs/domain/search-and-traceability.md` |
| 101 | Impact analysis query: given artifact X, surface all artifacts that depend on it (graph traversal) | Search&Traceability | 2 | SQL graph traversal |
| 102 | Traceability matrix export (CSV/Excel) for a topic or decision | Search&Traceability | 3 | brief §6.18 |
| 103 | Append-only audit log for all state transitions on governed entities; immutable | Audit&Records | 1 | ADR-0009; `docs/domain/audit-and-records.md` |
| 104 | Audit log search/filter by entity, action, user, date range (Auditor role only) | Audit&Records | 1 | brief §6.18 |
| 105 | Structured logging (Serilog→self-hosted Seq) and distributed tracing (OpenTelemetry) | Audit&Records | 1 | README §A observability |
| 106 | ASP.NET Core health checks (liveness/readiness) | Platform | 1 | Containerized deployment requirement |
| 107 | API documentation (OpenAPI/Swagger) | Platform | 1 | digest §4 stack constraint |
| 108 | EN/AR locale switching; full RTL layout (logical CSS + `dir` attribute) | Platform | 1 | ADR-0012; first-class (P-07) |
| 109 | Light/dark theme | Platform | 1 | ADR-0012 |
| 110 | File attachment storage via `IFileStore` abstraction (S3-compatible/Blob; metadata in SQL) | Platform | 1 | README §A; concrete provider = open decision |
| 111 | Background job management via app-owned Hangfire on ACMP's own SQL Server schema (reminders, escalations, digests, render jobs) | Platform | 1 | README §A; not the org's Hangfire instance |
| 112 | Database migrations (EF Core code-first) with rollback capability | Platform | 1 | digest §4 |

---

## Out-of-Scope (Explicit Exclusions)

Each exclusion has a canonical rationale. These are not deferred — they are rejected for v1 and v2 unless a future phase decision explicitly reverses them.

| # | Item | Rationale |
|---|---|---|
| OOS-01 | **Generic project / sprint management** (sprint boards, velocity, capacity planning, team burndown, resource allocation, Jira-like issue tracking for non-governance work) | Off-mission by definition (P-01). These belong in stream PM tools. Including them turns ACMP into a second Jira and dilutes governance focus. See `docs/domain/product-vision-and-principles.md` Anti-Goals. |
| OOS-02 | **Diagramming engine** (custom canvas, shape library, drag-and-drop diagram builder, SVG/canvas renderer) | Tarseem handles all required diagram families (ADR-0006; digest §5.1). Building a renderer duplicates existing investment and is off-mission. |
| OOS-03 | **Research methodology / planning workflow engine** (custom stage-gate research workflows, hypothesis trees, experiment tracking beyond what Keystone produces) | Keystone is the companion authoring workflow (ADR-0007; digest §5.2). ACMP imports Keystone outputs as structured artifacts; it does not replicate the methodology. |
| OOS-04 | **Meeting / video conferencing platform** (video rooms, screen sharing, chat, breakout rooms, participant audio) | Webex handles meetings. ACMP integrates with Webex APIs for metadata, recordings, and notifications. |
| OOS-05 | **Mobile-native app** (iOS/Android native application) | Committee use is desktop/tablet. A responsive web app is sufficient. Native mobile adds platform-specific maintenance cost without proportionate benefit for the user base. |
| OOS-06 | **Public self-registration or anonymous access** (any unauthenticated access to committee data, public topic submission portals) | This is a sensitive internal governance tool. All access is provisioned via OIDC/Keycloak (ADR-0004). Public access violates the security model. |
| OOS-07 | **Real-time collaborative editing** (simultaneous multi-user editing with operational transform or CRDT, e.g., Google Docs-style) | The committee lifecycle (draft → review → approve) does not require simultaneous editing. OT/CRDT adds major architectural complexity inconsistent with P-02. Revisit in Phase 3 only if demand is demonstrated. |
| OOS-08 | **Workflow / BPM engine** (BPMN modeling, configurable process designer, runtime workflow engine) | The committee's workflows are known, stable, and finite. Hard-coded state machines in the domain model (README §E) are simpler, more auditable, and less error-prone than a general BPM runtime. |
| OOS-09 | **SSO / IdP management** (user directory, credential storage, MFA configuration, identity provider administration) | ACMP is an OIDC relying party only (ADR-0004). Identity is managed in Keycloak/the org's existing auth service. |
| OOS-10 | **Data warehouse / BI platform** (OLAP cubes, ETL pipelines, ad-hoc SQL for end users, Power BI/Tableau embedding) | Reporting via SQL Server read models + columnstore is sufficient at this scale (digest §5.6). The org's existing BI stack can consume ACMP data via views if advanced analytics are needed — that is a future integration, not a v1 build. |
| OOS-11 | **Email / SMS delivery infrastructure** (own SMTP server, SMS gateway) in v1 | Email delivery is deferred to a later phase (when an SMTP relay is available). v1 delivers in-app notifications only. The `INotificationChannel` abstraction allows email to be added as an adapter without code changes. ACMP does not use the org's notification platform (CON-001; ADR-0005). |
| OOS-12 | **External partner / government integration portals** (APIs for external partners to submit topics, read decisions, or consume data) | Out of scope for v1. The governed *subject matter* includes external partners, but the committee tool itself is internal. External-facing capabilities are a Phase 3 candidate with a separate security review. |
| OOS-13 | **Kubernetes / service mesh / message broker** (Kubernetes orchestration, Istio/Linkerd, RabbitMQ/Kafka as ACMP's own infrastructure) | The org already runs these but ACMP does not need them. A modular monolith on Docker Compose / a single container is sufficient (P-02; digest §4). |
| OOS-14 | **Custom search infrastructure** (Elasticsearch cluster, vector search, semantic search engine) in v1 | SQL Server FTS is sufficient for v1 scale (ADR-0011; digest §5.6). If search needs grow, stand up the platform's own self-hosted search (e.g., an OpenSearch container, app-owned) — never the org's ELK. |
| OOS-15 | **Real-time push / WebSocket infrastructure** (live dashboard auto-refresh, live voting ticker, WebSocket hub) | Page refresh or short polling is sufficient for the committee's usage pattern (tens of users, not thousands). WebSocket infrastructure is complexity inconsistent with P-02 and P-03. |

---

## Deferred / Manual-for-Now

Items that are in scope conceptually but are intentionally manual or deferred past Phase 1, pending validation or organizational readiness.

| # | Item | Status | Condition to Promote |
|---|---|---|---|
| D-01 | Timeline/Gantt-lite view for backlog | Manual (use calendar/list views) in Phase 1; automated in Phase 2 | Phase 2 start |
| D-02 | Webex recording/transcript retrieval and storage | Manual (secretary pastes link/uploads) in Phase 1; automated via Webex API in Phase 2 | Webex Assistant enabled org-wide and API access confirmed (OQ ref `docs/decisions/open-decision-register.md`) |
| D-03 | AI candidate extraction from transcripts | Manual (secretary reviews transcript and creates actions/decisions) in Phase 1–2; AI-assisted in Phase 3 | Transcripts available in machine-readable form (depends on D-02) |
| D-04 | Conflict-of-interest tracking per vote | Manual (honor system, noted in MoM) in Phase 1; system-tracked in Phase 2 | Phase 2 |
| D-05 | Keystone research mission import | Manual (secretary creates Research Mission record with Keystone package ref) in Phase 1; structured import in Phase 2 | Keystone package schema stable; import tool built |
| D-06 | Invariant exception request workflow | Manual process outside system in Phase 1–2; in-app workflow in Phase 3 | Demand demonstrated post-Phase 2 |
| D-07 | Traceability matrix export (CSV/Excel) | Manual (Auditor exports audit log and assembles) in Phase 1–2; automated export in Phase 3 | Phase 3 |
| D-08 | Notification user preferences | All-or-nothing per event type in Phase 1; per-user per-event preferences in Phase 2 | Phase 2 |
| D-09 | File attachment storage sizing and ops configuration | Provider is settled as self-hosted **MinIO** (S3-compatible, app-owned). Phase 1 uses `IFileStore` abstraction backed by MinIO; production volume/sizing is an ops configuration detail. | Ops configuration at deploy time |
| D-10 | Bulk topic operations (defer multiple, reassign owner) | Manual (one at a time) in Phase 1–2; bulk operations in Phase 3 | Phase 3 |

---

## Traceability

- In-scope capabilities → `docs/requirements/functional.md` (each capability maps to ≥1 FR)
- Out-of-scope rationale → `docs/domain/product-vision-and-principles.md §Anti-Goals` (anti-goals stated there; operationalized here)
- Phase definitions → `docs/planning/roadmap.md`
- Open decisions affecting scope → `docs/decisions/open-decision-register.md` (OQ items referenced above)
- Settled technology decisions constraining scope → `README.md §A` and `docs/adrs/adr-0001` through `AD