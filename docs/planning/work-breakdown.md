---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Work Breakdown — ACMP

The Keystone work-breakdown structure for ACMP. Every deliverable is a `WBS-N` group (one per epic) decomposed into `WBS-N.N` leaves (one per backlog item). This is the source-of-truth mapping from the ACMP-native `EPIC-##` / `BL-###` identifiers onto the governed `WBS-N[.N]` scheme, so the [traceability matrix](../validation/traceability-matrix.md) can cite a WBS id in its work-item column for every requirement.

## Scheme

- **Groups** — `WBS-N` where `N` = the epic number: `WBS-1` ↔ `EPIC-01`, `WBS-2` ↔ `EPIC-02`, … `WBS-20` ↔ `EPIC-20`.
- **Leaves** — `WBS-N.N`, one per `BL-###` backlog item, numbered in delivery order within the group.
- **BL → WBS crosswalk** is carried inline: every leaf ends with `(BL-### / EPIC-##)`, so the former ids stay traceable both ways.
- **Realizes** — each leaf cites the FR(s) it actually delivers. PH-1 leaves cite their epic's PH-1 FR set; a deferred (PH-2/PH-3) leaf cites the specific deferred FR it realizes. The group heading carries the epic's full FR envelope verbatim from [38-epics-and-features.md](work-breakdown.md).

**Totals:** 20 WBS groups · 135 WBS leaves (135 `BL-###` items: 90 in Phase 1, 37 in Phase 2, 8 in Phase 3). Backlog-number gaps (BL-091–100, BL-138–200) are intentional phase-block reservations, not missing work.

## EPIC → WBS crosswalk

| EPIC | Title | WBS group | Leaves | Phase |
|---|---|---|---|---|
| EPIC-01 | Platform Foundation | WBS-1 | WBS-1.1 – WBS-1.16 | 1 |
| EPIC-02 | Identity & Access Management | WBS-2 | WBS-2.1 – WBS-2.6 | 1 |
| EPIC-03 | Membership & User Management | WBS-3 | WBS-3.1 – WBS-3.6 | 1–2 |
| EPIC-04 | Topic Intake & Lifecycle | WBS-4 | WBS-4.1 – WBS-4.12 | 1–3 |
| EPIC-05 | Backlog Management | WBS-5 | WBS-5.1 – WBS-5.7 | 1–2 |
| EPIC-06 | Agenda & Meeting Management | WBS-6 | WBS-6.1 – WBS-6.11 | 1–2 |
| EPIC-07 | Minutes of Meeting (MoM) | WBS-7 | WBS-7.1 – WBS-7.4 | 1 |
| EPIC-08 | Decision Management | WBS-8 | WBS-8.1 – WBS-8.4 | 1 |
| EPIC-09 | Voting Engine | WBS-9 | WBS-9.1 – WBS-9.7 | 1–2 |
| EPIC-10 | Action Tracking | WBS-10 | WBS-10.1 – WBS-10.7 | 1 |
| EPIC-11 | Risk Management | WBS-11 | WBS-11.1 – WBS-11.3 | 1–2 |
| EPIC-12 | Dependency Management | WBS-12 | WBS-12.1 – WBS-12.4 | 1–2 |
| EPIC-13 | Notifications & Alerts | WBS-13 | WBS-13.1 – WBS-13.5 | 1–2 |
| EPIC-14 | Dashboards & Reporting | WBS-14 | WBS-14.1 – WBS-14.5 | 1–2 |
| EPIC-15 | Search & Traceability | WBS-15 | WBS-15.1 – WBS-15.8 | 1–3 |
| EPIC-16 | Audit & Records | WBS-16 | WBS-16.1 – WBS-16.5 | 1–2 |
| EPIC-17 | Governance — ADRs & Invariants | WBS-17 | WBS-17.1 – WBS-17.7 | 2–3 |
| EPIC-18 | Tarseem Diagram Management | WBS-18 | WBS-18.1 – WBS-18.6 | 2 |
| EPIC-19 | Research & Keystone Integration + Knowledge | WBS-19 | WBS-19.1 – WBS-19.7 | 2 |
| EPIC-20 | AI & Advanced Analytics | WBS-20 | WBS-20.1 – WBS-20.5 | 3 |

---

## WBS-1 — Platform Foundation

**Epic FR envelope:** FR-001, FR-003–FR-015.

- **WBS-1.1** Docker Compose stack: ACMP app + SQL Server + Seq + MinIO + health probes. Realizes FR-001, FR-003–FR-015. (BL-001 / EPIC-01).
- **WBS-1.2** EF Core project setup: DbContext, migrations infrastructure, apply-on-startup / CLI, rollback doc. Realizes FR-001, FR-003–FR-015. (BL-002 / EPIC-01).
- **WBS-1.3** Configuration management: secrets via env vars; no secrets in image; `IOptions<>` binding per module. Realizes FR-001, FR-003–FR-015. (BL-003 / EPIC-01).
- **WBS-1.4** Serilog structured logging → self-hosted Seq; correlation ID + masked user ID middleware. Realizes FR-001, FR-003–FR-015. (BL-004 / EPIC-01).
- **WBS-1.5** OpenTelemetry: traces + metrics for all HTTP requests and Hangfire jobs. Realizes FR-001, FR-003–FR-015. (BL-005 / EPIC-01).
- **WBS-1.6** App-owned Hangfire: own SQL schema; retry/failure queue; Administrator dashboard. Realizes FR-001, FR-003–FR-015. (BL-006 / EPIC-01).
- **WBS-1.7** SQL outbox pattern: transactional publish + Hangfire delivery consumer. Realizes FR-001, FR-003–FR-015. (BL-007 / EPIC-01).
- **WBS-1.8** MinIO `IFileStore` implementation: S3-compatible; pre-signed time-limited URLs; metadata entity. Realizes FR-001, FR-003–FR-015. (BL-008 / EPIC-01).
- **WBS-1.9** ASP.NET Core health checks: liveness + readiness; Docker Compose HEALTHCHECK wired. Realizes FR-001, FR-003–FR-015. (BL-009 / EPIC-01).
- **WBS-1.10** OpenAPI/Swagger document: auto-generated; ProblemDetails RFC 7807 error model. Realizes FR-001, FR-003–FR-015. (BL-010 / EPIC-01).
- **WBS-1.11** React + Vite + TypeScript scaffold: module layout, route guards, error boundaries, typed API client. Realizes FR-001, FR-003–FR-015. (BL-011 / EPIC-01).
- **WBS-1.12** react-i18next: EN/AR locale files; locale switcher; persisted locale; no lost unsaved data on switch. Realizes FR-001, FR-003–FR-015. (BL-012 / EPIC-01).
- **WBS-1.13** RTL layout: CSS logical properties throughout; `dir` toggled; RTL smoke test. Realizes FR-001, FR-003–FR-015. (BL-013 / EPIC-01).
- **WBS-1.14** Light/dark theme: CSS variables; preference persisted; applied on load. Realizes FR-001, FR-003–FR-015. (BL-014 / EPIC-01).
- **WBS-1.15** Unsaved-work guard: navigation-away confirmation on dirty forms; reusable hook. Realizes FR-001, FR-003–FR-015. (BL-015 / EPIC-01).
- **WBS-1.16** Localized validation errors: ProblemDetails in active locale; frontend error display. Realizes FR-001, FR-003–FR-015. (BL-016 / EPIC-01).

## WBS-2 — Identity & Access Management

**Epic FR envelope:** FR-001–FR-002, FR-016, FR-018, FR-022, FR-024.

- **WBS-2.1** OIDC authorization-code + PKCE flow: Keycloak integration; token validation middleware; silent renew; logout. Realizes FR-001–FR-002, FR-016, FR-018, FR-022, FR-024. (BL-017 / EPIC-02).
- **WBS-2.2** Auth service adapter: config-driven switch between Keycloak and legacy org auth. Realizes FR-001–FR-002, FR-016, FR-018, FR-022, FR-024. (BL-018 / EPIC-02).
- **WBS-2.3** Keycloak claim parser: group/realm-role claims → ACMP canonical roles. Realizes FR-001–FR-002, FR-016, FR-018, FR-022, FR-024. (BL-019 / EPIC-02).
- **WBS-2.4** ASP.NET Core policy-based authorization: role policies per endpoint; 403 on unauthorized. Realizes FR-001–FR-002, FR-016, FR-018, FR-022, FR-024. (BL-020 / EPIC-02).
- **WBS-2.5** Per-topic ABAC capability resolver: Owner/Assignee/Presenter relationship checks. Realizes FR-001–FR-002, FR-016, FR-018, FR-022, FR-024. (BL-021 / EPIC-02).
- **WBS-2.6** Role-aware navigation: frontend hides unauthorized nav items and action buttons. Realizes FR-001–FR-002, FR-016, FR-018, FR-022, FR-024. (BL-027 / EPIC-02).

## WBS-3 — Membership & User Management

**Epic FR envelope:** FR-016–FR-021; FR-023 (PH-2).

- **WBS-3.1** User entity + migration: name, email, streams, deactivated flag; seeded on first-login from OIDC claims. Realizes FR-016–FR-021. (BL-022 / EPIC-03).
- **WBS-3.2** Administrator: create user (name, email, stream assignment); no self-registration endpoint. Realizes FR-016–FR-021. (BL-023 / EPIC-03).
- **WBS-3.3** Stream entity + migration; stream assignment multi-select; stream filter context propagated. Realizes FR-016–FR-021. (BL-024 / EPIC-03).
- **WBS-3.4** User deactivation: blocks login; historical records intact and attributed. Realizes FR-016–FR-021. (BL-025 / EPIC-03).
- **WBS-3.5** Member directory UI: paginated active users with name, role, streams, email. Realizes FR-016–FR-021. (BL-026 / EPIC-03).
- **WBS-3.6** Guest/Presenter time-boxed invitation: view-only scoped to meeting/topic; auto-expire. Realizes FR-023 (PH-2). (BL-126 / EPIC-03).

## WBS-4 — Topic Intake & Lifecycle

**Epic FR envelope:** FR-025–FR-029, FR-038–FR-040, FR-042–FR-044; FR-030, FR-036, FR-041, FR-045 (PH-2). Note: envelope FR-036 (timeline view) is realized under WBS-5.7; FR-041 (topic templates) under WBS-19.6 / WBS-19.7.

- **WBS-4.1** Topic entity + migration: all fields; `TOP-YYYY-###` ID generation. Realizes FR-025–FR-029, FR-038–FR-040, FR-042–FR-044. (BL-028 / EPIC-04).
- **WBS-4.2** Topic submission API + UI: required-field validation (EN+AR); attachment upload via `IFileStore`. Realizes FR-025–FR-029, FR-038–FR-040, FR-042–FR-044. (BL-029 / EPIC-04).
- **WBS-4.3** Topic status state machine: transitions enforced; immutable rejection/deferral event recording. Realizes FR-025–FR-029, FR-038–FR-040, FR-042–FR-044. (BL-030 / EPIC-04).
- **WBS-4.4** Topic triage workflow: Secretary accept/reject (mandatory rationale)/defer (mandatory reason + date). Realizes FR-025–FR-029, FR-038–FR-040, FR-042–FR-044. (BL-031 / EPIC-04).
- **WBS-4.5** Topic edit lock: Owner/Secretary in Draft/Submitted/Triage; post-Accepted metadata-only for Secretary. Realizes FR-025–FR-029, FR-038–FR-040, FR-042–FR-044. (BL-032 / EPIC-04).
- **WBS-4.6** Topic comment thread: timestamped, attributed, immutable after post. Realizes FR-025–FR-029, FR-038–FR-040, FR-042–FR-044. (BL-033 / EPIC-04).
- **WBS-4.7** Topic detail page: all fields, status history with actors/timestamps, linked artifacts, traceability excerpt. Realizes FR-025–FR-029, FR-038–FR-040, FR-042–FR-044. (BL-034 / EPIC-04).
- **WBS-4.8** Aging indicator: background Hangfire SLA evaluation; visual badge + Secretary notification. Realizes FR-025–FR-029, FR-038–FR-040, FR-042–FR-044. (BL-035 / EPIC-04).
- **WBS-4.9** Prepared + Scheduled status transitions: Secretary marks Prepared; schedules to a meeting. Realizes FR-025–FR-029, FR-038–FR-040, FR-042–FR-044. (BL-036 / EPIC-04).
- **WBS-4.10** Topic conversion workflow: convert to/from ADR/Research Mission; typed link; Converted status. Realizes FR-030 (PH-2). (BL-136 / EPIC-04).
- **WBS-4.11** Topic Reopen: Secretary reopens Closed/Rejected topic with recorded reason; re-enters triage. Realizes FR-045 (PH-2). (BL-137 / EPIC-04).
- **WBS-4.12** Bulk topic operations: Secretary defers/reassigns multiple topics at once. Realizes D-10 (bulk topic operations — deferred item, no dedicated FR; PH-3). (BL-208 / EPIC-04).

## WBS-5 — Backlog Management

**Epic FR envelope:** FR-031–FR-035, FR-037–FR-038; FR-036 (PH-2).

- **WBS-5.1** Backlog list view: badges, status, owner, streams, dates; server-side sort/filter. Realizes FR-031–FR-035, FR-037–FR-038. (BL-037 / EPIC-05).
- **WBS-5.2** Backlog table/dense view: all fields as columns; user-configurable column visibility/order. Realizes FR-031–FR-035, FR-037–FR-038. (BL-038 / EPIC-05).
- **WBS-5.3** Backlog kanban view: status columns; `@dnd-kit` DnD within/between columns with permission check. Realizes FR-031–FR-035, FR-037–FR-038. (BL-039 / EPIC-05).
- **WBS-5.4** Backlog calendar view: topics by scheduled meeting date; monthly + weekly layout. Realizes FR-031–FR-035, FR-037–FR-038. (BL-040 / EPIC-05).
- **WBS-5.5** DnD reprioritization: explicit ordinal stored; keyboard-accessible move-up/down alternative (WCAG 2.2 AA). Realizes FR-031–FR-035, FR-037–FR-038. (BL-041 / EPIC-05).
- **WBS-5.6** Aging indicators on all backlog views: badge, color, tooltip with SLA deadline. Realizes FR-031–FR-035, FR-037–FR-038. (BL-042 / EPIC-05).
- **WBS-5.7** Timeline/Gantt-lite view: topic date bars; pan/zoom. Realizes FR-036 (PH-2). (BL-128 / EPIC-05).

## WBS-6 — Agenda & Meeting Management

**Epic FR envelope:** FR-046–FR-052, FR-056, FR-061; FR-057–FR-058 (PH-2).

- **WBS-6.1** Meeting record entity + migration: `MTG-YYYY-###`; date, time, type, mode, agenda reference. Realizes FR-046–FR-052, FR-056, FR-061. (BL-043 / EPIC-06).
- **WBS-6.2** Agenda entity + migration: `AGN-YYYY-###`; ordered items with time-box, presenter assignment. Realizes FR-046–FR-052, FR-056, FR-061. (BL-044 / EPIC-06).
- **WBS-6.3** Agenda creation UI: select Scheduled topics; DnD + keyboard alternative; time-box; presenter assign. Realizes FR-046–FR-052, FR-056, FR-061. (BL-045 / EPIC-06).
- **WBS-6.4** Carry-over auto-suggest: unresolved items from previous meeting surfaced on new agenda. Realizes FR-046–FR-052, FR-056, FR-061. (BL-046 / EPIC-06).
- **WBS-6.5** Agenda publish: API + Hangfire notification dispatch; in-app notification to all members with deep link. Realizes FR-046–FR-052, FR-056, FR-061. (BL-047 / EPIC-06).
- **WBS-6.6** Attendance tracking: Secretary marks Present/Absent/Remote per member; quorum context. Realizes FR-046–FR-052, FR-056, FR-061. (BL-048 / EPIC-06).
- **WBS-6.7** Live meeting notes: free-text Markdown per agenda item; auto-save debounce ≤2s. Realizes FR-046–FR-052, FR-056, FR-061. (BL-049 / EPIC-06).
- **WBS-6.8** Manual recording attachment: Secretary pastes link or uploads file to meeting record. Realizes FR-046–FR-052, FR-056, FR-061. (BL-050 / EPIC-06).
- **WBS-6.9** Meeting calendar view: past + upcoming meetings with status badges. Realizes FR-046–FR-052, FR-056, FR-061. (BL-051 / EPIC-06).
- **WBS-6.10** Webex recording metadata retrieval: Hangfire post-meeting; store title/duration/downloadUrl. Realizes FR-057 (PH-2). (BL-113 / EPIC-06).
- **WBS-6.11** Webex transcript retrieval: Transcripts API + snippets; stored linked to meeting; access-restricted. Realizes FR-058 (PH-2). (BL-114 / EPIC-06).

## WBS-7 — Minutes of Meeting (MoM)

**Epic FR envelope:** FR-053–FR-055; FR-059–FR-060 (PH-3, realized under WBS-20).

- **WBS-7.1** MoM entity + migration: `MIN-YYYY-###`; versioned content; approval state. Realizes FR-053–FR-055. (BL-053 / EPIC-07).
- **WBS-7.2** MoM generation: compile attendance + agenda items + notes + decisions + actions into structured Markdown. Realizes FR-053–FR-055. (BL-054 / EPIC-07).
- **WBS-7.3** MoM versioning + approval workflow: Secretary drafts → Reviewer/Chairman annotate → Chairman approves → locked. Realizes FR-053–FR-055. (BL-055 / EPIC-07).
- **WBS-7.4** MoM distribution: in-app notification to all committee members on approval with deep link. Realizes FR-053–FR-055. (BL-056 / EPIC-07).

## WBS-8 — Decision Management

**Epic FR envelope:** FR-062–FR-067, FR-069; FR-068 (PH-2, realized under WBS-17.4).

- **WBS-8.1** Decision entity + migration: `DECN-YYYY-###`; outcome enum, rationale, alternatives, conditions, authority, effective date. Realizes FR-062–FR-067, FR-069. (BL-057 / EPIC-08).
- **WBS-8.2** Decision creation API + UI: canonical outcome dropdown; alternatives; conditions; downstream link required before Issued. Realizes FR-062–FR-067, FR-069. (BL-058 / EPIC-08).
- **WBS-8.3** Decision immutability enforcement: content locked once Issued; corrections via superseding decision; supersession chain. Realizes FR-062–FR-067, FR-069. (BL-059 / EPIC-08).
- **WBS-8.4** Decision history list: filterable by outcome, topic, stream, date range, chairman. Realizes FR-062–FR-067, FR-069. (BL-068 / EPIC-08).

## WBS-9 — Voting Engine

**Epic FR envelope:** FR-070–FR-075, FR-077–FR-078; FR-076 (PH-2).

- **WBS-9.1** Vote entity + migration: `VOTE-…`; eligible voters, options, quorum threshold, abstention flag; lifecycle status. Realizes FR-070–FR-075, FR-077–FR-078. (BL-060 / EPIC-09).
- **WBS-9.2** Vote configuration + open UI: Secretary configures; vote opens; in-app notification to eligible voters. Realizes FR-070–FR-075, FR-077–FR-078. (BL-061 / EPIC-09).
- **WBS-9.3** Vote casting: one-vote-per-voter enforcement at API layer; abstention option; live aggregate display. Realizes FR-070–FR-075, FR-077–FR-078. (BL-062 / EPIC-09).
- **WBS-9.4** Quorum enforcement: vote cannot close if present-voters < quorum; check from attendance record. Realizes FR-070–FR-075, FR-077–FR-078. (BL-063 / EPIC-09).
- **WBS-9.5** Chairman approval: Confirm / Override (+ mandatory reason) / Abstain-from-override; immutable record. Realizes FR-070–FR-075, FR-077–FR-078. (BL-064 / EPIC-09).
- **WBS-9.6** Vote audit: all choices + timestamps + chairman action in append-only log; immutable after Ratified. Realizes FR-070–FR-075, FR-077–FR-078. (BL-065 / EPIC-09).
- **WBS-9.7** Conflict-of-interest flag per voter per vote: declaration recorded; no auto-exclusion. Realizes FR-076 (PH-2). (BL-127 / EPIC-09).

## WBS-10 — Action Tracking

**Epic FR envelope:** FR-079–FR-088.

- **WBS-10.1** Action entity + migration: `ACT-…`; title, owner (required), due date (required), description, priority; linked to topic/decision/risk. Realizes FR-079–FR-088. (BL-069 / EPIC-10).
- **WBS-10.2** Action creation API: required-field enforcement (owner + due date) at API + UI. Realizes FR-079–FR-088. (BL-070 / EPIC-10).
- **WBS-10.3** Action status tracking: Open → InProgress → Blocked → Completed; Verified; Cancelled (mandatory reason). Realizes FR-079–FR-088. (BL-071 / EPIC-10).
- **WBS-10.4** Overdue derivation: background Hangfire daily job; derived status displayed. Realizes FR-079–FR-088. (BL-072 / EPIC-10).
- **WBS-10.5** Action reminder Hangfire job: notification to owner N days before due (default 3). Realizes FR-079–FR-088. (BL-073 / EPIC-10).
- **WBS-10.6** Action escalation Hangfire job: notification to Secretary + Chairman when overdue > threshold (default 2d). Realizes FR-079–FR-088. (BL-074 / EPIC-10).
- **WBS-10.7** Actions dashboard: open/blocked/overdue across all topics; filterable by owner, stream, topic, due date, status. Realizes FR-079–FR-088. (BL-075 / EPIC-10).

## WBS-11 — Risk Management

**Epic FR envelope:** FR-089–FR-091, FR-093; FR-092 (PH-2).

- **WBS-11.1** Risk entity + migration: `RSK-…`; title, description, likelihood, impact, owner, mitigation plan; linked to topic/action. Realizes FR-089–FR-091, FR-093. (BL-076 / EPIC-11).
- **WBS-11.2** Risk creation + status lifecycle API + UI: mitigation plan required before Close; Accepted status with rationale. Realizes FR-089–FR-091, FR-093. (BL-077 / EPIC-11).
- **WBS-11.3** Risk escalation notification: notify Secretary + Chairman when risk status = Escalated. Realizes FR-092 (PH-2). (BL-135 / EPIC-11).

## WBS-12 — Dependency Management

**Epic FR envelope:** FR-094–FR-095, FR-098; FR-096–FR-097 (PH-2). Note: envelope FR-096 (impact analysis query) is realized under WBS-15.6.

- **WBS-12.1** Dependency edge entity + migration: `DPN-…`; source, target, type (Blocks / DependsOn / RelatesTo). Realizes FR-094–FR-095, FR-098. (BL-078 / EPIC-12).
- **WBS-12.2** Cross-stream dependency flag: visual highlight when source + target affect different streams. Realizes FR-094–FR-095, FR-098. (BL-079 / EPIC-12).
- **WBS-12.3** Per-topic dependency list: inbound + outbound edges on topic detail page. Realizes FR-094–FR-095, FR-098. (BL-080 / EPIC-12).
- **WBS-12.4** Dependency graph visualization: Tarseem dependency family; SQL graph traversal → JSON spec → render. Realizes FR-097 (PH-2). (BL-122 / EPIC-12).

## WBS-13 — Notifications & Alerts

**Epic FR envelope:** FR-129–FR-132; FR-133–FR-134 (PH-2).

- **WBS-13.1** In-app notification center: bell icon, list, mark-as-read, deep links; `INotificationChannel` + in-app adapter. Realizes FR-129–FR-132. (BL-052 / EPIC-13).
- **WBS-13.2** Notification event catalog: all PH-1 events wired to `INotificationChannel` dispatch. Realizes FR-129–FR-132. (BL-082 / EPIC-13).
- **WBS-13.3** Webex adapter: `INotificationChannel` Adaptive Cards (v1.3, ≤80KB); 429 back-off via Hangfire retry. Realizes FR-129–FR-132 (PH-2 adapter). (BL-112 / EPIC-13).
- **WBS-13.4** Notification user preferences: per-user per-event-type opt-in/out page. Realizes FR-133 (PH-2). (BL-124 / EPIC-13).
- **WBS-13.5** Notification digest Hangfire job: daily/weekly summary to opted-in users. Realizes FR-134 (PH-2). (BL-125 / EPIC-13).

## WBS-14 — Dashboards & Reporting

**Epic FR envelope:** FR-135–FR-137; FR-138–FR-140, FR-142 (PH-2); FR-141 (PH-3, realized under WBS-20.5).

- **WBS-14.1** Committee dashboard: backlog summary by status/urgency, next meeting + agenda link, action counts, last 5 decisions. Realizes FR-135. (BL-088 / EPIC-14).
- **WBS-14.2** Secretary dashboard: triage queue, pending MoM approvals, overdue actions requiring escalation, aging topics. Realizes FR-136. (BL-089 / EPIC-14).
- **WBS-14.3** Chairman dashboard: votes pending approval, escalated risks, escalated actions, topics deferred ≥2 times. Realizes FR-137. (BL-090 / EPIC-14).
- **WBS-14.4** Per-stream report: topics/decisions/actions/risks by stream; filterable by date + status. Realizes FR-138 (PH-2). (BL-129 / EPIC-14).
- **WBS-14.5** Decision history report + CSV export; action completion trend chart; CSV/PNG export for all reports. Realizes FR-139–FR-140, FR-142 (PH-2). (BL-130 / EPIC-14).

## WBS-15 — Search & Traceability

**Epic FR envelope:** FR-143–FR-147; FR-148 (PH-2); FR-149 (PH-3).

- **WBS-15.1** Traceability relationship entity + migration: typed edges between any two artifacts; types enum. Realizes FR-143–FR-147. (BL-083 / EPIC-15).
- **WBS-15.2** Traceability panel: upstream + downstream typed edges on every artifact detail page. Realizes FR-143–FR-147. (BL-084 / EPIC-15).
- **WBS-15.3** Typed edge CRUD API: create/read/delete; relationship type validated against enum. Realizes FR-143–FR-147. (BL-085 / EPIC-15).
- **WBS-15.4** SQL Server FTS setup: EN + AR word-breakers; FTS index on topics, decisions, MoMs. Realizes FR-143–FR-147. (BL-086 / EPIC-15).
- **WBS-15.5** Global search bar: accessible from every page; FTS across topics/decisions/MoMs; results grouped by type. Realizes FR-143–FR-147. (BL-087 / EPIC-15).
- **WBS-15.6** Impact analysis query: transitive blocked work via SQL graph traversal; configurable depth; navigable results. Realizes FR-148 (PH-2). (BL-123 / EPIC-15).
- **WBS-15.7** Transitive impact analysis UI: display result list with navigable artifact links. Realizes FR-148 (PH-2). (BL-131 / EPIC-15).
- **WBS-15.8** Traceability matrix CSV export: topic + all linked artifacts by type and relationship. Realizes FR-149 (PH-3). (BL-206 / EPIC-15).

## WBS-16 — Audit & Records

**Epic FR envelope:** FR-150–FR-153; FR-154–FR-155 (PH-2).

- **WBS-16.1** Audit log entity + migration: append-only; entity type, ID, action, actor, UTC timestamp, before/after JSON, correlation ID; no UPDATE/DELETE. Realizes FR-150–FR-153. (BL-066 / EPIC-16).
- **WBS-16.2** Audit hash chain: SHA-256 chained hash over vote + issued-decision records; chain verifiable. Realizes FR-150–FR-153. (BL-067 / EPIC-16).
- **WBS-16.3** Audit log search UI: Auditor/Administrator search by entity type, ID, actor, action, date range; paginated. Realizes FR-150–FR-153. (BL-081 / EPIC-16).
- **WBS-16.4** Audit log export: CSV/JSON by date range + entity filter; Auditor/Administrator only. Realizes FR-154 (PH-2). (BL-132 / EPIC-16).
- **WBS-16.5** Retention policy configuration: configurable period per entity type; no auto-purge in v1; Admin UI. Realizes FR-155 (PH-2). (BL-133 / EPIC-16).

## WBS-17 — Governance — ADRs & Invariants

**Epic FR envelope:** FR-099–FR-109; FR-110 (PH-3).

- **WBS-17.1** ADR entity + migration: in-app `ADR-…` IDs; MADR-lite template fields; lifecycle status. Realizes FR-099–FR-109. (BL-106 / EPIC-17).
- **WBS-17.2** ADR creation + lifecycle API + UI: MADR-lite form; status transitions; supersession chain; immutability. Realizes FR-099–FR-109. (BL-107 / EPIC-17).
- **WBS-17.3** ADR repository view: FTS on ADR content; filterable by status/date/author/stream. Realizes FR-099–FR-109. (BL-108 / EPIC-17).
- **WBS-17.4** Decision→ADR promotion: pre-fill from decision; bidirectional traceability link. Realizes FR-099–FR-109 (incl. FR-068 promotion link). (BL-109 / EPIC-17).
- **WBS-17.5** Architecture Invariant entity + migration: `AIV-…`; category, scope, statement, rationale, owner; lifecycle. Realizes FR-099–FR-109. (BL-110 / EPIC-17).
- **WBS-17.6** Invariant creation + lifecycle + violation recording API + UI; invariant list with violation count. Realizes FR-099–FR-109. (BL-111 / EPIC-17).
- **WBS-17.7** Invariant exception request workflow: submit → Secretary review → Chairman approval. Realizes FR-110 (PH-3). (BL-207 / EPIC-17).

## WBS-18 — Tarseem Diagram Management

**Epic FR envelope:** FR-121–FR-128.

- **WBS-18.1** Tarseem container: FastAPI wrapper around `tarseem.generate()`; internal HTTP endpoint; wired in Docker Compose. Realizes FR-121–FR-128. (BL-101 / EPIC-18).
- **WBS-18.2** Diagram entity + migration: `DGM-…`; JSON spec column; spec hash; engine version; status. Realizes FR-121–FR-128. (BL-102 / EPIC-18).
- **WBS-18.3** Diagram render pipeline: Hangfire job → Tarseem sidecar → store artifacts via `IFileStore`; record spec hash. Realizes FR-121–FR-128. (BL-103 / EPIC-18).
- **WBS-18.4** Diagram error surface: Tarseem error list (code, path, message, hint) shown to author; no silent failures. Realizes FR-121–FR-128. (BL-104 / EPIC-18).
- **WBS-18.5** Diagram version history + export: spec diff; all format downloads (SVG/PNG/PDF/draw.io/PPTX). Realizes FR-121–FR-128. (BL-105 / EPIC-18).
- **WBS-18.6** Diagram attachment to topics/ADRs/decisions via traceability relationship model. Realizes FR-121–FR-128. (BL-134 / EPIC-18).

## WBS-19 — Research & Keystone Integration + Knowledge

**Epic FR envelope:** FR-111–FR-120.

- **WBS-19.1** Research Mission entity + migration: `RMS-…`; title, description, linked topic, Keystone package ref, status. Realizes FR-111–FR-120. (BL-115 / EPIC-19).
- **WBS-19.2** Keystone import tool: parse package manifest → Finding (`FND-…`), Recommendation (`REC-…`), Risk (`RSK-…`). Realizes FR-111–FR-120. (BL-116 / EPIC-19).
- **WBS-19.3** Research Mission detail page + traceability links. Realizes FR-111–FR-120. (BL-117 / EPIC-19).
- **WBS-19.4** Wiki page entity + migration: `DOC-…`; Markdown content, category, versioned. Realizes FR-111–FR-120. (BL-118 / EPIC-19).
- **WBS-19.5** Wiki editor UI: Markdown editor with preview; version history; diff view; add to FTS index. Realizes FR-111–FR-120. (BL-119 / EPIC-19).
- **WBS-19.6** Template entity + migration: `TPL-…`; Markdown template with placeholder fields; artifact type tag. Realizes FR-111–FR-120 (incl. FR-041 topic templates). (BL-120 / EPIC-19).
- **WBS-19.7** Template selection at artifact creation: pre-fills description/content field; editable. Realizes FR-111–FR-120 (incl. FR-041 topic templates). (BL-121 / EPIC-19).

## WBS-20 — AI & Advanced Analytics

**Epic FR envelope:** FR-059–FR-060, FR-110, FR-141, FR-149; email items in FR-130.

- **WBS-20.1** Transcript FTS index: SQL Server FTS on transcript content; search restricted to Chairman/Secretary/Auditor. Realizes FR-059–FR-060. (BL-201 / EPIC-20).
- **WBS-20.2** AI candidate extraction: Secretary-triggered; configurable LLM endpoint; draft candidates; approval gate; off by default. Realizes FR-059–FR-060. (BL-202 / EPIC-20).
- **WBS-20.3** AI extraction feature flag + privacy gate: Admin activation only; data-residency confirmation stored; activation log. Realizes FR-059–FR-060. (BL-203 / EPIC-20).
- **WBS-20.4** Email adapter: `INotificationChannel` via SMTP; zero change to dispatch call site; activated by config. Realizes FR-130 (email items). (BL-204 / EPIC-20).
- **WBS-20.5** KPI/health dashboard: avg topic-to-decision days, action SLA %, backlog age distribution, vote-to-ratification time; configurable thresholds. Realizes FR-141 (PH-3). (BL-205 / EPIC-20).

---

## Traceability

- **EPIC → WBS group:** the crosswalk table above (`WBS-N` ↔ `EPIC-0N`); source epics in [38-epics-and-features.md](work-breakdown.md).
- **BL → WBS leaf:** the inline `(BL-### / EPIC-##)` tag on every leaf; ordered delivery sequence in [execution/backlog.md](../execution/backlog.md).
- **WBS leaf → FR:** the `Realizes FR-###` clause on every leaf; requirement text in [07-functional-requirements.md](../requirements/functional.md).
- **WBS → AC / test:** resolved in the [traceability matrix](../validation/traceability-matrix.md) (G-TRACE gate: every MVP requirement → ≥1 decision, ≥1 work item, ≥1 test).
- Cross-epic realization notes (FR-036 → WBS-5.7, FR-041 → WBS-19.6/19.7, FR-068 → WBS-17.4, FR-096 → WBS-15.6, FR-141 → WBS-20.5) prevent double-counting where an epic's FR envelope is delivered by another group's leaf.
