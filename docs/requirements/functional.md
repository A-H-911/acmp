---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Functional Requirements — ACMP

**Purpose:** Authoritative catalog of all functional requirements (FR-###) for ACMP, grouped by canonical module, with MoSCoW priority, source provenance, and phase. Every row satisfies Keystone gate G-REQ-SRC. Covers Deliverable 10.

**Conventions:**
- **Priority:** M = Must (Phase 1 MVP blocker); S = Should (Phase 2 expected); C = Could (Phase 3 enhancement); W = Won't (explicitly excluded or deferred indefinitely)
- **Phase:** 1 = MVP, 2 = Phase 2, 3 = Phase 3
- Source format: `brief §N.N` refers to the functional scope sections of the project brief; `digest §N.N` refers to the shared context digest; `pain PAIN-##` refers to pain-point catalog in `docs/domain/pain-points.md`; `derived: <reason>` for FRs logically implied by cited sources

---

## Module: Platform (Shared Kernel)

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-001 | The system shall authenticate users via OIDC against Keycloak. No credentials are stored in ACMP. | M | brief §6.1; ADR-0004 | 1 |
| FR-002 | The system shall support an adapter for the org's existing internal auth service during Keycloak migration, with configuration to switch between providers without code change. | M | digest §1; ADR-0004 | 1 |
| FR-003 | The system shall present all UI in English and Arabic; the user may switch locale at any time without losing unsaved data. | M | brief §6; ADR-0012; P-07 | 1 |
| FR-004 | The system shall render all UI in full RTL layout when Arabic locale is selected, using CSS logical properties throughout; no LTR-only layouts exist. | M | brief §6; ADR-0012 | 1 |
| FR-005 | The system shall support light and dark themes, switchable per user preference, persisted across sessions. | M | brief §6; ADR-0012 | 1 |
| FR-006 | The system shall store file attachments in an S3-compatible/Blob store via `IFileStore` abstraction; metadata (filename, size, MIME type, uploader, timestamp, entity link) is stored in SQL Server. | M | README §A; ADR-0003 | 1 |
| FR-007 | The system shall expose structured health check endpoints (liveness, readiness) conforming to ASP.NET Core health check conventions. | M | digest §4; derived: containerized deployment | 1 |
| FR-008 | The system shall expose an OpenAPI/Swagger document for all REST endpoints, accessible in development and staging environments. | M | digest §4 | 1 |
| FR-009 | The system shall emit structured logs (Serilog) to the app-owned self-hosted Seq container; all log entries include correlation ID, user ID (masked per privacy policy), module, and operation. No dependency on the org's ELK/Seq. | M | README §A; digest §4 | 1 |
| FR-010 | The system shall emit distributed traces (OpenTelemetry) for all inbound HTTP requests and background jobs. | M | README §A | 1 |
| FR-011 | All background jobs shall be managed via Hangfire; job status, history, and failures are visible to Administrator. | M | README §A; digest §1 | 1 |
| FR-012 | The system shall run database migrations (EF Core code-first) automatically on startup in non-production environments and via explicit CLI command in production. | M | digest §4 | 1 |
| FR-013 | The system shall externalize all configuration (connection strings, secrets, URLs, feature flags) via environment variables and/or a secrets manager; no secrets in source code or container images. | M | digest §4; derived: security | 1 |
| FR-014 | The system shall display user-facing validation errors in the active locale (EN or AR) with clear, actionable messages; technical error details are never exposed to end users. | M | brief §6 UX; derived: accessibility | 1 |
| FR-015 | The system shall prevent loss of unsaved work: when a user navigates away from a form with unsaved changes, a confirmation prompt shall appear. | M | brief §6 UX | 1 |

---

## Module: Membership

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-016 | The system shall not permit public self-registration. User accounts are created by Administrator or Secretary via invitation or direct provisioning only. | M | brief §6.1; ADR-0004 | 1 |
| FR-017 | Administrator shall be able to create a user account with name, email, and stream membership; the user's global role is sourced from Keycloak group/realm-role claims and mapped to ACMP roles on login. The user receives an in-app notification on first login (email deferred). | M | brief §6.1; ADR-0004 | 1 |
| FR-018 | Global roles (Chairman, Secretary, Member, Reviewer, Auditor, Administrator, Submitter, Guest/Presenter) are sourced from Keycloak group/realm-role claims and mapped to ACMP roles on token validation; ACMP does not store or assign global roles independently. Administrator manages committee membership, stream scope, and per-topic assignments within ACMP. | M | README §C; ADR-0004 | 1 |
| FR-019 | Administrator shall be able to assign a user to one or more streams; stream membership drives scope filtering throughout the application. | M | digest §2; brief §6.1 | 1 |
| FR-020 | Administrator shall be able to deactivate a user account; deactivated users cannot log in but their historical records (votes, authorship) remain intact and attributed. | M | derived: audit integrity; brief §6.1 | 1 |
| FR-021 | The system shall display a committee member directory listing all active users with name, role, stream membership, and email; visible to all authenticated users. | M | brief §6.1 | 1 |
| FR-022 | The system shall support per-topic ABAC capabilities (Owner, Assignee/Contributor, Presenter) assigned independently of global role; a Member can be Presenter on a topic without any global role elevation. | M | README §C; brief §6.1 | 1 |
| FR-023 | The system shall support time-boxed Guest/Presenter access: a secretary can invite an external presenter with view-only access scoped to a specific meeting or topic, expiring automatically after the meeting date. | S | brief §6.1; digest §3 | 2 |
| FR-024 | Every user-facing page shall display only the navigation elements and actions the authenticated user's role is authorized to see; unauthorized actions are hidden, not merely disabled. | M | derived: RBAC/UX; brief §6.1 | 1 |

---

## Module: Topics

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-025 | A user with Submitter, Member, Secretary, or Chairman role shall be able to submit a new topic with: title (required), description (required, Markdown), topic type (required, from canonical taxonomy), urgency (required), source (required), scope (required), affected streams (multi-select, ≥1 required), affected systems/services (free text, optional), and target date (optional). | M | brief §6.2; digest §3 | 1 |
| FR-026 | A submitted topic shall receive a system-generated canonical ID in format `TOP-YYYY-###` (year-scoped sequential) on creation, displayed in all views and communications. | M | README §F | 1 |
| FR-027 | Topic author (Owner or Secretary) shall be able to attach files (presentations, documents, diagrams, data) to a topic; each attachment is stored via `IFileStore` and linked to the topic in the traceability model. | M | brief §6.2; digest §3 | 1 |
| FR-028 | Topic owner shall be able to add free-text comments to a topic; comments are timestamped, attributed, and immutable after posting (corrections via new comment). | M | brief §6.2 | 1 |
| FR-029 | Secretary shall be able to set topic status to Triage, Accept (assigning/confirming owner), Reject (with mandatory rejection rationale), or Defer (with mandatory reason and target re-evaluation date). | M | README §E; brief §6.2 | 1 |
| FR-030 | Secretary shall be able to convert a topic to a different type (e.g., convert a ResearchDiscovery to an ArchitectureDecision post-research), recording the conversion reason and creating a typed link between the original and converted artifact. | S | README §E; brief §6.2 | 2 |
| FR-031 | The backlog shall be displayed as a list view: title, type, urgency badge, status, owner, affected streams, created date, last-updated date; sortable and filterable by all listed fields. | M | brief §6.3 | 1 |
| FR-032 | The backlog shall be displayed as a table/dense view: all topic fields as columns; columns are user-configurable (show/hide, reorder) per session. | M | brief §6.3 | 1 |
| FR-033 | The backlog shall be displayed as a kanban view: columns correspond to topic status values; topics are cards within columns; drag-and-drop within a column reorders priority; drag between columns changes status (with permission check). | M | brief §6.3; ADR-0012 | 1 |
| FR-034 | The backlog kanban and list views shall provide a keyboard-accessible alternative to drag-and-drop for priority reordering (e.g., move-up/move-down controls). | M | brief §6 UX; derived: WCAG 2.2 AA | 1 |
| FR-035 | The backlog shall be displayed as a calendar view showing topics by their scheduled meeting date (monthly and weekly layout). | M | brief §6.3 | 1 |
| FR-036 | The backlog shall be displayable as a timeline/Gantt-lite view showing topic date bars (created → target → scheduled → decided) with pan/zoom. | S | brief §6.3 | 2 |
| FR-037 | Secretary and Chairman shall be able to drag-and-drop topics to reprioritize within the backlog across all views; priority is stored as an explicit ordinal field; tied priorities are broken by submission date. | M | brief §6.3; ADR-0012 | 1 |
| FR-038 | The system shall display an aging indicator on any topic that has remained in the same status beyond the urgency-defined SLA threshold (Critical: 3 days; Urgent: 7 days; Normal: 21 days [unverified — validate with committee]). Aging triggers a visual badge and a background notification to Secretary. | M | brief §6.3; digest §3 | 1 |
| FR-039 | The topic detail page shall display: all topic fields; a complete status history with timestamps and actor; linked artifacts (agenda items, meeting records, decisions, votes, actions, risks, dependencies, ADRs, diagrams, research missions); and a traceability graph excerpt showing upstream and downstream links. | M | brief §6.3; digest §3 | 1 |
| FR-040 | Topic editing shall be permitted for the Owner and Secretary while the topic is in Draft, Submitted, or Triage status. After Accepted, only Secretary may edit metadata; content is locked to prevent retroactive modification of the record. | M | derived: audit integrity; brief §6.2 | 1 |
| FR-041 | The system shall support topic templates (selectable by type on creation): template provides default description structure, required-field checklist, and suggested sections. Templates are managed in the Knowledge module. | S | brief §6.2; brief §6.13 | 2 |
| FR-042 | Secretary shall be able to mark a topic as Prepared (research complete, presentation ready) before scheduling it for a meeting. | M | README §E | 1 |
| FR-043 | Secretary shall be able to schedule a topic to a specific meeting (changing status to Scheduled); a topic can only be scheduled to one future meeting at a time. | M | README §E; brief §6.4 | 1 |
| FR-044 | When a topic is Rejected or Deferred, the rejection/deferral reason and actor are recorded as an immutable event in the topic's history; rejected topics are not deleted. | M | derived: audit integrity; brief §6.2 | 1 |
| FR-045 | Secretary shall be able to Reopen a Closed or Rejected topic with a recorded reason; reopened topics re-enter the triage workflow. | S | README §E | 2 |

---

## Module: Meetings

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-046 | Secretary shall be able to create an agenda (AGN-YYYY-###) for a scheduled meeting: select topics from backlog (status=Scheduled), set presentation order, assign time-box per item (minutes), assign presenter per item. | M | brief §6.4 | 1 |
| FR-047 | Agenda items shall be reorderable by drag-and-drop with keyboard-accessible alternative. | M | brief §6.4; ADR-0012 | 1 |
| FR-048 | The system shall auto-suggest carry-over items from the previous meeting's agenda that were not resolved (status ≠ Decided/Closed) when creating a new agenda. | M | brief §6.4 | 1 |
| FR-049 | Secretary shall be able to publish the agenda to committee members; publication triggers an in-app notification (v1) containing the agenda and any attached materials links. Webex Adaptive Card notification is Phase 2. | M | brief §6.4; ADR-0005 | 1 |
| FR-050 | Secretary shall be able to create a meeting record (MTG-YYYY-###) with: date, start/end time, meeting type (regular/extraordinary/emergency), meeting mode (in-person/remote/hybrid), and a link to the published agenda. | M | brief §6.5 | 1 |
| FR-051 | The system shall support attendance tracking: Secretary marks each committee member as Present, Absent, or Remote for a given meeting; attendance is persisted and used for quorum calculation. | M | brief §6.5; digest §3 | 1 |
| FR-052 | During or after a meeting, Secretary shall be able to add live notes per agenda item (free-text Markdown); notes are auto-saved on typing pause (debounce ≤2s). | M | brief §6.5 | 1 |
| FR-053 | The system shall generate a draft Minutes of Meeting (MoM, MIN-YYYY-###) by compiling: attendance record, agenda items (ordered), per-item notes, decisions issued, and actions created, into a structured Markdown document. | M | brief §6.5 | 1 |
| FR-054 | MoM shall have a versioning and approval workflow: Secretary produces draft; Reviewer(s) or Chairman review and annotate; Chairman approves; approved MoM is locked (immutable content); version history preserved. | M | brief §6.5 | 1 |
| FR-055 | On MoM approval, the system shall send an in-app notification to all committee members with a link to the published MoM (v1); Webex Adaptive Card notification is Phase 2. | M | brief §6.5; ADR-0005 | 1 |
| FR-056 | Secretary shall be able to attach a Webex meeting recording link and/or upload a recording file to the meeting record. | M | brief §6.5; digest §5.3 | 1 |
| FR-057 | The system shall retrieve Webex meeting recording metadata (title, duration, download URL, play URL) via the Webex Recordings API and store it in the meeting record, triggered by a Hangfire job post-meeting. | S | digest §5.3 | 2 |
| FR-058 | The system shall retrieve Webex meeting transcript (speaker-attributed snippets) via the Webex Meeting Transcripts API and store the content linked to the meeting record, subject to Webex Assistant being enabled by the host. | S | digest §5.3 | 2 |
| FR-059 | Stored transcripts shall be indexed in SQL Server FTS and searchable by keyword across all meetings (access restricted to Chairman, Secretary, Auditor). | S | brief §6.5 | 3 |
| FR-060 | The system shall extract AI-candidate action items and decision summaries from a stored transcript and present them to the Secretary as draft proposals requiring explicit human approval; no AI-extracted content enters the record without approval. | C | brief §6.5; P-05 | 3 |
| FR-061 | The system shall display a meeting calendar view showing past and upcoming meetings with status badges. | M | brief §6.5 | 1 |

---

## Module: Decisions

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-062 | Secretary or Chairman shall be able to create a committee decision record (DECN-YYYY-###) linked to a topic, with: outcome (from canonical outcome list), rationale (required), conditions (for ConditionallyApproved), authority actor (Chairman or designated), and effective date. | M | brief §6.6; README §E | 1 |
| FR-063 | The system shall enforce that the outcome field on a decision is selected from the canonical list: Approved, ConditionallyApproved, Rejected, MoreInfoRequired, FeedbackProvided, EnhancementsRequired, DesignChangesRequired, ResearchRequired, Deferred, Escalated, Converted. No free-text outcome field. | M | README §E; ADR-0009 | 1 |
| FR-064 | A decision record shall capture alternatives considered (structured list: alternative name + reason not chosen) to preserve rationale. | M | brief §6.6 | 1 |
| FR-065 | A decision record shall support a "supersedes" link to a prior decision; when a new decision supersedes an older one, the older decision is marked Superseded with a back-link — it is never deleted or edited. | M | ADR-0009; README §E | 1 |
| FR-066 | Once a decision record status is Issued (i.e., voting closed and chairman has acted), the decision content (outcome, rationale, alternatives, conditions) is immutable; corrections require creating a new superseding decision. | M | ADR-0009; P-06 | 1 |
| FR-067 | Secretary shall be able to link a decision to one or more downstream artifacts (ADR, Action, Risk, Invariant) in the traceability model; at least one downstream link is required before a decision is marked Issued. | M | P-11; brief §6.6 | 1 |
| FR-068 | Chairman shall be able to promote a committee decision to an ADR: a new ADR record (ADR-…) is created pre-filled from the decision record; the decision and ADR are bidirectionally linked. | S | brief §6.11; README §E | 2 |
| FR-069 | The system shall display a decision history list filterable by outcome, topic, stream, date range, and chairman. | M | brief §6.6 | 1 |

---

## Module: Decisions — Voting

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-070 | Secretary shall be able to configure a vote on a topic with: eligible voter list (subset of committee members), voting options (Approve/Reject; or Approve/ConditionallyApprove/Reject; or custom), quorum threshold (minimum present voters required), abstention allowed (boolean). Voting is always attributed in v1 (anonymity is out of scope for v1). | M | ADR-0010; brief §6.7 | 1 |
| FR-071 | The system shall enforce that the vote cannot be closed unless the quorum threshold is met (eligible voters present ≥ quorum count). | M | ADR-0010; brief §6.7 | 1 |
| FR-072 | Each eligible voter shall cast exactly one vote or one abstention per vote session; double-voting is prevented by the system. | M | ADR-0010; brief §6.7 | 1 |
| FR-073 | Voting is always attributed in v1: all individual voter choices are recorded and visible to Auditor and Chairman; aggregate totals are also displayed. (Vote anonymity is out of scope for v1.) | M | ADR-0010 | 1 |
| FR-074 | After the vote closes, Chairman shall record one of: Confirm (ratify the majority outcome), Override (select a different outcome with mandatory reason), or Abstain-from-override (accept majority without personal endorsement). Chairman action is recorded explicitly and immutably. | M | ADR-0010; digest §3 | 1 |
| FR-075 | All vote records (each voter's choice, timestamp, chairman action, quorum check result) are stored in the append-only audit log and are immutable after vote close. | M | ADR-0009; brief §6.7 | 1 |
| FR-076 | The system shall support a conflict-of-interest flag per voter per vote: a voter may declare a conflict; the declaration is recorded without automatically excluding the voter (exclusion is a chairman decision). | S | brief §6.7 | 2 |
| FR-077 | The vote status lifecycle shall be: Configured → Open → Closed → Ratified; no backward transitions. | M | README §E | 1 |
| FR-078 | Eligible voters shall receive an in-app notification when a vote is opened on a topic they are eligible for (v1); Webex Adaptive Card notification is Phase 2. | M | brief §6.7; ADR-0005 | 1 |

---

## Module: Actions

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-079 | Any user with Secretary, Chairman, or Member role shall be able to create an action item (ACT-…) linked to a topic, decision, or risk, with: title (required), owner (required, specific user), due date (required), description, priority. | M | brief §6.8 | 1 |
| FR-080 | The system shall prevent creation of an action without an owner and a due date; these are required fields enforced at the API and UI layer. | M | P-13; brief §6.8 | 1 |
| FR-081 | Action owner shall be able to update action status (Open→InProgress→Blocked→Completed) and add progress notes (free-text, timestamped). | M | brief §6.8 | 1 |
| FR-082 | The system shall derive Overdue status for any action where due date < today and status ∉ {Completed, Verified, Cancelled}; this derived state is displayed visually and does not require user action. | M | brief §6.8; README §E | 1 |
| FR-083 | A Hangfire background job shall run daily and send a reminder notification to the action owner N days before the due date (N is configurable per deployment, default 3 days). | M | brief §6.8 | 1 |
| FR-084 | A Hangfire background job shall send an escalation notification to Secretary and Chairman when an action is Overdue beyond a configurable threshold (default 2 days past due). | M | brief §6.8 | 1 |
| FR-085 | Secretary or Chairman shall be able to mark a completed action as Verified; verification is recorded with actor and timestamp. | M | brief §6.8 | 1 |
| FR-086 | Secretary shall be able to cancel an action with a mandatory cancellation reason; cancelled actions are not deleted and remain in the topic's history. | M | derived: audit integrity; brief §6.8 | 1 |
| FR-087 | The actions dashboard shall display all open, blocked, and overdue actions across all topics, filterable by owner, stream, topic, due date range, and status; visible to all committee members. | M | brief §6.8; digest §3 | 1 |
| FR-088 | The system shall display a per-topic action list on the topic detail page showing all actions linked to that topic with current status. | M | brief §6.8 | 1 |

---

## Module: Risks

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-089 | Any user with Secretary, Chairman, or Member role shall be able to create a risk record (RSK-…) linked to a topic or action, with: title (required), description (required), likelihood (High/Medium/Low), impact (High/Medium/Low), owner (required), mitigation plan (optional at creation, required before Close). | M | brief §6.9 | 1 |
| FR-090 | The system shall prevent closing a risk without a recorded mitigation plan or an explicit Accepted status with acceptance rationale. | M | derived: governance; brief §6.9 | 1 |
| FR-091 | Risk status lifecycle shall be: Open → Mitigating → Closed; side states: Accepted (owner and chairman agreed to accept without mitigation), Escalated (elevated to committee for decision). | M | README §E; brief §6.9 | 1 |
| FR-092 | When a risk is Escalated, a notification shall be sent to Secretary and Chairman with the risk summary. | S | brief §6.9 | 2 |
| FR-093 | The topic detail page shall display all risks linked to the topic with current status and likelihood/impact matrix. | M | brief §6.9 | 1 |

---

## Module: Dependencies

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-094 | Any user with Secretary, Chairman, or Member role shall be able to create a dependency edge (DPN-…) between two entities (topic→topic, topic→action, topic→system, action→action) with a typed relationship: Blocks, DependsOn, RelatesTo. | M | brief §6.10 | 1 |
| FR-095 | The system shall flag when a topic's dependency crosses stream boundaries (source entity's affected streams ≠ target entity's affected streams), visually highlighting it as a cross-stream dependency on the topic detail page. | M | brief §6.10; digest §2 | 1 |
| FR-096 | The system shall perform an impact analysis query: given entity X, return all entities transitively blocked by or dependent on X via SQL graph traversal; result is displayed as a list and optionally rendered as a Tarseem dependency diagram. | S | brief §6.10; ADR-0008 | 2 |
| FR-097 | A dependency graph view (rendered via Tarseem dependency family) shall be available on the topic detail page and the dependency module view, showing the local dependency neighborhood of a selected entity. | S | brief §6.10; ADR-0006 | 2 |
| FR-098 | The system shall display a dependency list on the topic detail page showing all inbound (things that depend on this topic) and outbound (things this topic depends on) dependency edges. | M | brief §6.10 | 1 |

---

## Module: Governance — ADRs

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-099 | Secretary or Chairman shall be able to create an ADR (in-app ADR-… ID) using the MADR-lite template: title, status, context, decision, consequences, alternatives (structured list), date. | S | brief §6.11; digest §5.5 | 2 |
| FR-100 | ADR status lifecycle shall be: Draft → Proposed → Approved → (Superseded \| Deprecated). No backward transitions from Approved. | S | README §E; brief §6.11 | 2 |
| FR-101 | A superseded ADR shall link to its superseding ADR; both records are preserved and immutable after the supersession event. | S | ADR-0009; brief §6.11 | 2 |
| FR-102 | The ADR repository shall display a searchable list of all ADRs, filterable by status, date, author, and affected stream; full-text search on ADR content via SQL FTS. | S | brief §6.11 | 2 |
| FR-103 | An ADR shall be linkable to a committee decision (DECN-…), topic (TOP-…), and architecture invariant (AIV-…) in the traceability graph; these links shall appear on the ADR detail page and on the linked entity's detail page. | S | brief §6.11; ADR-0008 | 2 |
| FR-104 | The ADR detail page shall render the ADR content in formatted Markdown; the raw Markdown is downloadable. | S | brief §6.11 | 2 |
| FR-105 | Secretary shall be able to manage ADR templates (TPL-…): create, edit, set as default for ADR type; templates are stored in the Knowledge module. | S | brief §6.13 | 2 |

---

## Module: Governance — Architecture Invariants

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-106 | Secretary or Chairman shall be able to create an Architecture Invariant (AIV-…): category (Security/Performance/Data/Interoperability/Other [unverified — validate categories with committee]), scope (single-stream/multi-stream/platform/org-wide), statement (required), rationale (required), owner (required). | S | brief §6.12 | 2 |
| FR-107 | Invariant status lifecycle shall be: Draft → Proposed → Active → (Retired \| Superseded). | S | README §E; brief §6.12 | 2 |
| FR-108 | The system shall support recording a violation event against an active invariant: violation description, discovering entity (topic, audit, incident), severity, and a link to the topic or action that will remediate it. | S | brief §6.12 | 2 |
| FR-109 | The invariant list shall display all active invariants with violation count; violations are filterable by category, scope, and severity. | S | brief §6.12 | 2 |
| FR-110 | An invariant exception request workflow (submit → Secretary review → Chairman approval) shall be supported as an in-app process. | C | brief §6.12 | 3 |

---

## Module: Research

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-111 | Secretary shall be able to create a Research Mission record (RMS-…): title, description, linked topic, Keystone package reference (URL or path), status. | S | brief §6.14; ADR-0007; digest §5.2 | 2 |
| FR-112 | The system shall import a Keystone package's structured artifacts (findings, recommendations, decisions, risks, acceptance criteria) from a referenced Keystone package and map them to ACMP domain entities: Finding (FND-…), Recommendation (REC-…), Risk (RSK-…). | S | ADR-0007; digest §5.2; deferred out of P15 → future (D-05, 2026-07-12) | 3 |
| FR-113 | Each imported Finding (FND-…) and Recommendation (REC-…) shall be a first-class artifact: individually viewable, linkable in the traceability graph, and associable with decisions and actions. | S | brief §6.14; ADR-0008 | 2 |
| FR-114 | The Research Mission detail page shall display: the Keystone package reference, import status, all imported findings/recommendations/risks, and linked ACMP artifacts (topics, decisions, actions). | S | brief §6.14 | 2 |
| FR-115 | A Research Mission shall be linkable to a topic (as the output of a ResearchDiscovery or ResearchRequired decision); this link shall appear in the traceability graph. | S | brief §6.14; ADR-0008 | 2 |

---

## Module: Knowledge

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-116 | Secretary shall be able to create and manage wiki/knowledge-base pages (DOC-…) in Markdown with: title, category, content, and cross-links to other pages or ACMP artifacts. | S | brief §6.14 | 2 |
| FR-117 | Wiki pages shall be versioned: each save creates an immutable version; previous versions are viewable and diffable. | S | brief §6.14; derived: audit | 2 |
| FR-118 | Wiki content shall be indexed in SQL Server FTS and searchable alongside topics, decisions, and ADRs in the global search. | S | brief §6.14 | 2 |
| FR-119 | Secretary shall be able to create, edit, and delete templates (TPL-…) for artifact types: Topics (by type), MoM, ADR, and Research Mission. Templates are Markdown with placeholder fields. | S | brief §6.13 | 2 |
| FR-120 | Templates shall be selectable at artifact creation time; the template's Markdown content pre-fills the description/content field and is editable by the user. | S | brief §6.13 | 2 |

---

## Module: Diagrams

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-121 | Secretary or Member shall be able to create a diagram (DGM-…) by submitting a JSON spec conforming to the Tarseem v1.0 schema; the spec is stored as the version-controlled source of truth in SQL Server (JSON column). | S | ADR-0006; digest §5.1 | 2 |
| FR-122 | The system shall render a diagram by submitting the JSON spec to the Tarseem sidecar (containerized render endpoint) via a Hangfire background job; the generated artifacts (SVG, PNG, PDF, draw.io, PPTX) are stored via `IFileStore` with the spec hash recorded. | S | ADR-0006; digest §5.1 | 2 |
| FR-123 | On diagram spec update, the system shall queue a re-render job; the previous artifact version is retained and accessible in diagram version history. | S | ADR-0006 | 2 |
| FR-124 | Each generated artifact shall record the Tarseem engine version and spec hash used to produce it, enabling future reproducibility and diff. | S | digest §5.1; ADR-0006 | 2 |
| FR-125 | Tarseem render errors shall be surfaced to the diagram author with the structured error list (code, path, message, hint) returned by `tarseem.generate()`; no silent failures. | S | digest §5.1 | 2 |
| FR-126 | Diagrams shall be attachable to topics (TOP-…), ADRs (ADR-…), and decisions (DECN-…) via the traceability relationship model. | S | ADR-0008; brief §6.15 | 2 |
| FR-127 | All available export formats (SVG, PNG, PDF, draw.io, PPTX) shall be downloadable from the diagram detail page. | S | digest §5.1 | 2 |
| FR-128 | Diagram version history shall display: spec diff between versions, render timestamp, artifact hash, and engine version. | S | ADR-0006 | 2 |

---

## Module: Notifications

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-129 | All outbound notifications shall be dispatched via the `INotificationChannel` abstraction. The **in-app notification center** is the sole concrete implementation in v1. The Webex adapter (Phase 2) and an Email adapter (deferred until SMTP relay available) are planned future implementations. No notification code hard-codes a specific channel; no org notification platform is used (CON-001). | M | ADR-0005 | 1 |
| FR-130 | The system shall send an **in-app notification** for each event in the notification catalog: topic status change, agenda published, vote opened, vote closed, MoM published, action reminder, action overdue, action escalation, risk escalation (v1). Webex Adaptive Card (v1.3 compliant, ≤80KB) notifications are Phase 2; email notifications are deferred. | M | ADR-0005; brief §6.17 | 1 |
| FR-131 | Each notification shall include a deep link to the relevant artifact in ACMP (e.g., clicking a vote-open notification takes the recipient directly to the voting UI for that topic). | M | brief §6.17 | 1 |
| FR-132 | Outbound integration/notification dispatch shall back off and retry transient failures via the background-job/outbox mechanism; delivery failures are logged and surfaced in the admin dashboard. (Webex-specific HTTP 429 + Retry-After handling lands with the Webex adapter in Phase 2.) | M | digest §5.3 | 1 |
| FR-133 | The system shall support a user notification preferences page (per-user, per-event-type: on/off) for all non-mandatory notifications. | S | brief §6.17 | 2 |
| FR-134 | A Hangfire job shall generate and send a daily/weekly digest of pending actions, upcoming meetings, and open votes to opted-in users. | S | brief §6.17 | 2 |

---

## Module: Reporting

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-135 | The committee dashboard (visible to all authenticated users) shall display: topic backlog summary by status and urgency, next scheduled meeting with agenda link, count of open actions by status, count of overdue actions, and the last 5 decisions issued. | M | brief §6.16 | 1 |
| FR-136 | The secretary dashboard shall display: topics in triage queue, pending MoM approvals, overdue actions awaiting escalation, and aging topics beyond SLA. | M | brief §6.16 | 1 |
| FR-137 | The chairman dashboard shall display: votes pending chairman approval, escalated risks, escalated actions, and topics deferred ≥2 times. | M | brief §6.16 | 1 |
| FR-138 | The system shall provide a per-stream report: all topics, decisions, actions, and risks affecting a given stream, filterable by date range and status. | S | brief §6.16 | 2 |
| FR-139 | The system shall provide a decision history report: all decisions in a date range, filterable by outcome and stream, exportable to CSV. | S | brief §6.16 | 2 |
| FR-140 | The system shall provide an action completion rate trend chart (weekly/monthly): open vs. closed vs. overdue over time. | S | brief §6.16 | 2 |
| FR-141 | The system shall provide a KPI/health dashboard with: average topic-to-decision days by type, action SLA compliance %, backlog age distribution, and vote-to-ratification time. | C | brief §6.16; `docs/domain/metrics-kpi-catalog.md` | 3 |
| FR-142 | All tabular reports shall be exportable to CSV; charts shall be exportable to PNG. | S | brief §6.16 | 2 |

---

## Module: Search & Traceability

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-143 | The system shall provide a global search bar, accessible from every page, that searches across: topics (title, description), decisions (rationale), ADRs (content), MoM (content), wiki pages, and (in Phase 3) transcripts; powered by SQL Server FTS. | M | brief §6.18; ADR-0011 | 1 |
| FR-144 | Global search results shall be grouped by artifact type and display: ID, title, matched excerpt, status, and a deep link to the artifact. | M | brief §6.18 | 1 |
| FR-145 | Global search shall support EN and AR queries; SQL Server FTS language configuration shall include both Arabic and English word-breakers. | M | brief §6.18; P-07 | 1 |
| FR-146 | Every artifact (topic, decision, action, risk, dependency, ADR, invariant, diagram, research mission, finding, recommendation) shall expose a traceability panel showing: all typed upstream relationships (what this artifact came from) and downstream relationships (what this artifact produced or affects). | M | ADR-0008; P-11; brief §6.18 | 1 |
| FR-147 | The system shall support creating, reading, and deleting typed traceability edges between any two artifacts in the platform; relationship types include: DerivedFrom, Supersedes, Implements, Resolves, References, Blocks, DependsOn, RelatesTo. | M | ADR-0008; brief §6.18 | 1 |
| FR-148 | The system shall perform a transitive impact analysis query: given artifact X, return all artifacts reachable via any relationship type within a configurable depth limit (default 3 hops); results displayed as a navigable list. | S | ADR-0008; brief §6.18 | 2 |
| FR-149 | The system shall generate a traceability matrix for a topic (TOP-…): a table of the topic and all linked artifacts by type and relationship, exportable to CSV. | C | brief §6.18 | 3 |

---

## Module: Audit & Records

| ID | Requirement | Priority | Source | Phase |
|---|---|---|---|---|
| FR-150 | The system shall maintain an append-only audit log recording every state transition, field update (field name, old value, new value), and user action on governed entities (topics, decisions, votes, actions, risks, dependencies, ADRs, invariants, MoMs). | M | ADR-0009; brief §6.18 | 1 |
| FR-151 | Each audit log entry shall record: entity type, entity ID, action type, actor user ID, timestamp (UTC), before-state (JSON), after-state (JSON), and correlation ID. | M | ADR-0009 | 1 |
| FR-152 | Audit log entries shall be immutable: no API, no UI action, and no database-level process shall be capable of updating or deleting audit log rows in normal operation. | M | ADR-0009; P-06 | 1 |
| FR-153 | Auditor and Administrator roles shall be able to search the audit log by: entity type, entity ID, actor user, action type, and date range; results are paginated. | M | brief §6.18 | 1 |
| FR-154 | The audit log shall be exportable to CSV or JSON for a date range and entity filter; this export is accessible only to Auditor and Administrator. | S | brief §6.18 | 2 |
| FR-155 | Data retention policy: all records are retained; retention is **configurable** so legal can set periods later; **no automatic purge in v1**. Votes, issued decisions, ADRs, and published minutes are immutable. Specific retention periods (e.g., audit log years) to be set by org compliance/legal team [unverified — validate with org]. | S | derived: gov compliance; `docs/domain/audit-and-records.md` | 2 |

---

## Traceability

- All FRs → `docs/domain/scope-and-out-of-scope.md` (in-scope capability catalog rows map 1:many to FRs)
- FR-### → `docs/planning/work-breakdown.md`, `docs/domain/user-stories-mvp.md`, `docs/validation/acceptance-criteria.md` (G-TRACE gate requirement: every MVP FR → ≥1 AC)
- FR-### source provenance satisfies Keystone gate G-REQ-SRC for all rows
- Settled technology constraints referenced per FR → `README.md §A` and `docs/adrs/adr-0001` through `ADR-0012`
- [unverified] items → rai