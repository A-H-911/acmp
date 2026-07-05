# 27 — Reporting & Dashboards (Deliverable 35)

**Purpose:** Specify all ACMP dashboards, their widgets, data sources, drill-downs, role-based access, and phased delivery — covering all governance reporting areas mandated by brief §6.16.

---

## 1. Reporting Architecture

### 1.1 Data Layer

| Layer | Mechanism | Rationale |
|---|---|---|
| Transactional read | Standard EF Core queries on normalized OLTP tables | Low-cardinality lookups (single topic, single meeting) |
| Reporting read models | Dedicated SQL views + columnstore-indexed denormalized summary tables, refreshed via Hangfire background jobs | Dashboard aggregations, KPI calculations; columnstore delivers scan-heavy analytics efficiently on right-sized SQL Server |
| Export | Server-side CSV (streaming `IAsyncEnumerable`) + PDF (server-rendered HTML→PDF via a lightweight library `[unverified: e.g., PuppeteerSharp or QuestPDF]`) | Avoids third-party SaaS; self-contained (CON-001) |
| Scheduled reports | Hangfire recurring jobs (app-owned) → write output to MinIO → surface download link via in-app notification | Uses app-owned Hangfire, no org scheduler |
| Chart rendering | Client-side chart library (Recharts `[unverified]` or equivalent React charting lib compatible with React 18/TS) | Lightweight; server sends JSON data payloads; RTL/EN/AR labels handled via `react-i18next` |

### 1.2 Role-Based Report Views

Four primary report personas, each with a default dashboard composition:

| Persona | Role(s) | Default focus |
|---|---|---|
| **Executive** | Chairman | Summary KPIs, decision throughput, risk exposure, governance maturity — action-oriented, minimal noise |
| **Committee** | Member, Reviewer | My actions, upcoming meetings, agenda readiness, voting status, research progress |
| **Secretary** | Secretary | Backlog health, aging, bottlenecks, agenda prep status, overdue actions, pending approvals |
| **Audit** | Auditor | ADR lifecycle, voting patterns, decision traceability, attendance compliance, invariant violations |

Each dashboard is a composable page of widgets. Users may not reconfigure dashboards in v1 (Phase 1); saved custom filter sets are the personalization surface.

### 1.3 Common Features (all dashboards)

- **Interactive filtering:** all widgets respond to a shared filter bar (date range, stream, topic type, urgency, status). Filters persist in URL query string (deep-linkable).
- **Drill-down:** every aggregate metric is clickable — clicking a number navigates to the filtered list of contributing records.
- **Export:** each widget has a "Export CSV" and a page-level "Export PDF" action. PDF is a server-rendered snapshot of the current filtered view.
- **Refresh:** real-time refresh is not required (≤20 users, low-traffic); data is stale-while-revalidate with a 60-second polling interval `[unverified: interval TBD]`. A manual "Refresh" button triggers an immediate re-fetch.
- **Empty states:** every widget shows a purposeful empty state (e.g., "No overdue actions — well done") rather than a blank chart.
- **Localization:** all labels, dates (Gregorian, localized format), and numbers localized EN/AR; RTL chart axis mirroring.

---

## 2. Dashboard Catalog

> Each dashboard entry: **Purpose · Audience · Key Widgets/Visuals · Drill-down · Filters · Phase.**
> Widget notation: `[chart-type: description]`.

---

### DB-01 — Executive Summary Dashboard

**Purpose:** Single-screen governance health snapshot for the Chairman; actionable signal over noise.

**Audience:** Chairman (primary); Secretary (read).

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Governance maturity score | Gauge (0–100) + trend sparkline | Computed KPI-018; read model |
| Backlog size + WIP | Stat cards: total backlog, in-committee, overdue | Topic status counts |
| Decision throughput (rolling 4 weeks) | Line chart: decisions per week | Reporting read model |
| Open critical risks | Stat card + badge | Risk.Status=Open, Severity=Critical |
| Pending chairman approvals | Stat card + list | Vote.Status=Closed, awaiting ratification |
| Action overdue count | Stat card (red if >0) | Action.DueDate<today, Status≠Completed |
| ADR coverage rate | Donut: decisions with ADR / total decisions | Reporting read model |
| Top 3 bottleneck streams | Horizontal bar: avg topic age per stream | Topic aging read model |

**Drill-down:** Each stat card links to the relevant filtered list (e.g., "Open critical risks" → `/risks?status=Open&severity=Critical`).

**Filters:** Date range (default: last 30 days); Stream.

**Phase:** Phase 1.

---

### DB-02 — Secretary Operations Dashboard

**Purpose:** Daily operational view for the Secretary — what needs action now and what is at risk.

**Audience:** Secretary.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Backlog by status | Stacked bar: count per status stage | Topic.Status |
| Topic aging heatmap | Table: topics × age buckets (0–7d, 8–14d, 15–30d, >30d) per stream | Topic aging read model |
| Upcoming meetings (next 14 days) | Timeline list: meeting + agenda readiness status | Meeting + Agenda |
| Agenda readiness | Gauge per upcoming meeting: % agenda items with presenter + materials confirmed | AgendaItem completeness |
| Pending approvals queue | List: items awaiting secretary action (triage, MoM approval, agenda publish) | Workflow state across modules |
| Overdue actions | List with owner + days overdue | Action.DueDate<today |
| Topics without owners | Stat card | Topic.OwnerId=null, Status∈{Submitted,Triage} |
| Recent submissions (last 7 days) | List | Topic.CreatedAt |

**Drill-down:** "Topics without owners" → `/backlog?owner=unassigned`; "Overdue actions" → `/actions?status=Overdue`.

**Filters:** Date range; Stream; Topic Type; Urgency.

**Phase:** Phase 1.

---

### DB-03 — Committee Member Dashboard

**Purpose:** Personalized view for each Member — what requires their participation.

**Audience:** Member, Reviewer.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| My open actions | List with due date + status | Action.AssigneeId=me |
| Upcoming meetings I attend | List + time-to-next | CommitteeMembership + Meeting |
| Open votes requiring my input | Stat card + list | Vote.Status=Open, eligible voter |
| Topics I own / am assigned to | List by status | Topic.OwnerId=me OR Assignee=me |
| Recently published decisions | List (last 5) | Decision.Status=Published |
| My action completion rate | Stat: completed/total assigned (last 30d) | Action read model |

**Drill-down:** Each list item links to the artifact detail page.

**Filters:** Date range.

**Phase:** Phase 1.

---

### DB-04 — Backlog Status Dashboard

**Purpose:** Full-picture backlog health — size, age, composition, WIP limits.

**Audience:** Chairman, Secretary.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Backlog funnel | Funnel chart: Draft→Submitted→Triage→Accepted→Prepared→Scheduled | Topic status counts |
| Topics by type | Donut: ResearchDiscovery / ArchitectureDecision / EnhancementInnovation / GovernanceStandardization | Topic.TopicType |
| Topics by stream | Horizontal bar | Topic.AffectedStreams |
| Topics by urgency | Donut: Normal / Urgent / Critical | Topic.Urgency |
| Aging distribution | Box-and-whisker or histogram: days in current status | Topic aging read model |
| WIP (topics InCommittee simultaneously) | Stat card | Topic.Status=InCommittee count |
| Stale topics (no update >14d) | List | Topic.UpdatedAt<14d ago, active |

**Drill-down:** Each chart segment → filtered backlog list.

**Filters:** Date range (creation); Stream; Topic Type; Urgency; Status.

**Phase:** Phase 1.

---

### DB-05 — Topic Aging Dashboard

**Purpose:** Surface topics at risk of expiring SLAs or stalling in a single status.

**Audience:** Chairman, Secretary.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Age by status (median + P90) | Bar chart per status | Topic aging read model |
| Topics exceeding SLA | Table: topic ID, title, stream, status, days overdue vs. SLA | SLA thresholds per Urgency |
| Time-in-status trend (rolling 8 weeks) | Line chart: avg days per status per week | Reporting read model |
| Oldest topics | Table: top 10 by total age (days since Submitted) | Topic |

**Drill-down:** Topic rows → `/topics/{id}`.

**Filters:** Stream; Topic Type; Urgency; Status.

**Phase:** Phase 1 (basic aging list); Phase 3 (SLA trend analysis).

---

### DB-06 — Upcoming Meetings Dashboard

**Purpose:** Forward-look at scheduled and planning-stage meetings.

**Audience:** Chairman, Secretary, Member.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Meeting calendar (next 6 weeks) | Calendar view: meetings by date/time | Meeting.ScheduledDate |
| Agenda readiness per meeting | Progress bar per meeting: confirmed items / total items | AgendaItem |
| Quorum forecast | Stat per meeting: confirmed attendees / quorum threshold | Attendance RSVP (Phase 2; v1 = manual) |
| Carryover topics | List: topics marked for carry-over from prior meeting | AgendaItem.Status=CarriedOver |

**Drill-down:** Meeting card → `/meetings/{id}/agenda`.

**Filters:** Date range.

**Phase:** Phase 1.

---

### DB-07 — Agenda Readiness Dashboard

**Purpose:** Per-meeting readiness drill-down for the Secretary before each session.

**Audience:** Secretary, Chairman.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Agenda item checklist per meeting | Table: item, presenter confirmed, materials uploaded, time-box set, status | AgendaItem |
| Time-box utilization | Stacked bar: allocated time vs. available meeting time | AgendaItem.TimeBoxMinutes |
| Items missing presenter | List | AgendaItem.PresenterId=null |
| Items missing materials | List | AgendaItem without Attachment |

**Drill-down:** Items → `/meetings/{id}/agenda-item/{itemId}`.

**Filters:** Meeting (select specific meeting).

**Phase:** Phase 1.

---

### DB-08 — Decision Status Dashboard

**Purpose:** Pipeline view of committee decisions — from vote through ratification to ADR publication.

**Audience:** Chairman, Secretary, Auditor.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Decision outcomes distribution | Donut: Approved / ConditionallyApproved / Rejected / MoreInfoRequired / Deferred / other | Decision.Outcome |
| Decisions pending chairman ratification | List | Vote.Status=Closed, Decision.Status≠Published |
| Conditional decisions with open conditions | List | DecisionCondition.Status=Open |
| Recently published decisions (last 30d) | List | Decision.PublishedAt |
| Decisions by stream | Bar | Decision.AffectedStreams |

**Drill-down:** Decision row → `/decisions/{id}`.

**Filters:** Date range; Stream; Outcome; Topic Type.

**Phase:** Phase 1.

---

### DB-09 — Pending Approvals Dashboard

**Purpose:** All items awaiting a gate-keeper action in one place.

**Audience:** Chairman (ratifications), Secretary (triage, MoM approval, agenda publish).

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Chairman queue: votes awaiting ratification | List + days pending | Vote.Status=Closed, awaiting chairman action |
| Secretary queue: topics awaiting triage | List + submission date | Topic.Status=Submitted |
| MoM drafts awaiting approval | List | MinutesOfMeeting.Status=Draft |
| ADRs awaiting committee approval | List | ADR.Status=Proposed |
| Invariant proposals awaiting approval | List | Invariant.Status=Proposed |

**Drill-down:** Each queue item → artifact detail page with relevant action button.

**Filters:** None mandatory; queue is always "current state."

**Phase:** Phase 1.

---

### DB-10 — Overdue Actions Dashboard

**Purpose:** Actions past their due date requiring escalation attention.

**Audience:** Chairman, Secretary, Auditor.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Overdue count + severity | Stat cards: total overdue, by urgency | Action.DueDate<today, Status≠Completed |
| Overdue by assignee | Bar chart | Action.AssigneeId |
| Overdue by stream | Bar | Action.Topic.AffectedStreams |
| Overdue list | Table: action ID, title, owner, due date, days overdue, related topic | Action read model |
| Escalation history | List of escalation events | AuditEvent for action escalation |

**Drill-down:** Action row → `/actions/{id}`.

**Filters:** Stream; Assignee; Days overdue threshold.

**Phase:** Phase 1.

---

### DB-11 — Risk Exposure Dashboard

**Purpose:** Current risk register health and trend.

**Audience:** Chairman, Secretary, Auditor.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Risk matrix (severity × likelihood) | Bubble chart or heatmap grid | Risk.Severity, Risk.Likelihood |
| Open risks by stream | Bar | Risk.AffectedStreams |
| Risk trend (rolling 8 weeks) | Line: open / mitigating / closed per week | Risk read model |
| Critical open risks | Table with status + owner + topic | Risk.Severity=Critical, Status=Open |
| Risks without mitigation plans | Stat card | Risk.Mitigations.Count=0, Status=Open |

**Drill-down:** Risk row → `/risks/{id}`.

**Filters:** Stream; Severity; Status; Date range.

**Phase:** Phase 1.

---

### DB-12 — Cross-Stream Dependencies Dashboard

**Purpose:** Visualize blocking dependencies and cross-stream impact.

**Audience:** Chairman, Secretary, Member.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Dependency count by status | Stat cards: Pending / Active / Resolved / Blocked | Dependency.Status |
| Blocked topics | List: topic + blocking dependency + blocking topic | Dependency traversal |
| Cross-stream dependency map | Table (Phase 1) / interactive graph (Phase 2 via Tarseem) | Relationship edges |
| Circular dependency warnings | Alert list | Graph cycle detection (background job) |

**Drill-down:** Dependency row → `/dependencies/{id}`; topic row → `/topics/{id}`.

**Filters:** Stream; Status.

**Phase:** Phase 1 (table); Phase 2 (graph visualization via Tarseem).

---

### DB-13 — Topic Throughput Dashboard

**Purpose:** Velocity and flow analysis — how many topics the committee processes per period.

**Audience:** Chairman, Secretary.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Topics decided per week (rolling 12 weeks) | Line chart | Decision read model |
| Topics submitted vs. decided per month | Grouped bar | Topic intake vs. output |
| Throughput by type | Stacked bar: topic type composition per period | Decision + Topic.TopicType |
| Carry-over rate | Line: % carried over per meeting | AgendaItem.Status=CarriedOver / total |

**Drill-down:** Bar segment → filtered decision list for that period.

**Filters:** Date range; Stream; Topic Type.

**Phase:** Phase 1 (basic); Phase 3 (advanced trend modeling).

---

### DB-14 — Decision Lead Time Dashboard

**Purpose:** Measure end-to-end time from topic submission to decision, and identify where time is lost.

**Audience:** Chairman, Secretary.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Median lead time (submission→decision) | Stat card + trend | Topic.SubmittedAt → Decision.PublishedAt |
| Lead time breakdown by stage | Stacked bar: avg days per status transition | Status transition timestamps |
| Lead time by type | Box chart per topic type | Topic.TopicType × lead time |
| Lead time trend (rolling 12 weeks) | Line | Reporting read model |
| P90 outliers | Table: topics at >P90 lead time | |

**Drill-down:** Topic row → `/topics/{id}`.

**Filters:** Date range; Stream; Topic Type; Urgency.

**Phase:** Phase 3 (requires historical data accumulation).

---

### DB-15 — ADR Lifecycle Dashboard

**Purpose:** Track ADR creation, approval, and supersession health.

**Audience:** Chairman, Secretary, Auditor.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| ADRs by status | Donut: Draft / Proposed / Approved / Superseded / Deprecated | ADR.Status |
| ADR coverage | Stat: % decisions with a linked approved ADR | Relationship + Decision |
| ADRs due for review (>1 year since approval) | List | ADR.ApprovedAt < 1 year ago, not Superseded |
| Recently approved ADRs (last 30d) | List | ADR.ApprovedAt |
| ADRs by stream | Bar | ADR.AffectedStreams |

**Drill-down:** ADR row → `/decisions/adrs/{id}`.

**Filters:** Stream; Status; Date range.

**Phase:** Phase 1.

---

### DB-16 — Voting Patterns Dashboard

**Purpose:** Voting analytics — participation, consensus strength, dissent patterns (always attributed per ADR-0010).

**Audience:** Chairman, Auditor.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Quorum compliance rate | Stat + trend | Vote.QuorumMet / total votes |
| Vote outcome distribution | Donut: unanimous / majority / split | Vote result analysis |
| Abstention rate | Line: abstentions / eligible votes per period | Vote.Abstentions |
| Voting participation by member | Bar: % votes participated | Vote read model (attributed) |
| Chairman override/final-approval use | Stat + list | AuditEvent type=ChairmanOverride |

**Drill-down:** Member bar → their voting record list.

**Filters:** Date range; Stream; Topic Type.

**Phase:** Phase 1.

---

### DB-17 — Attendance Dashboard

**Purpose:** Meeting attendance and quorum compliance tracking.

**Audience:** Chairman, Secretary, Auditor.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Attendance rate per member (rolling 12 weeks) | Bar | Attendance.Present / meetings held |
| Quorum compliance per meeting | Line with threshold marker | Attendance quorum check |
| Absent members per meeting | Heatmap: member × meeting | Attendance |
| Average attendance % trend | Line | Reporting read model |

**Drill-down:** Member cell → their attendance record.

**Filters:** Date range; Member.

**Phase:** Phase 1.

---

### DB-18 — Research Progress Dashboard

**Purpose:** Track research missions from initiation through finding publication and conversion to topics/decisions.

**Audience:** Chairman, Secretary, Member.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Research missions by status | Donut | ResearchMission.Status |
| Missions overdue | List | ResearchMission.TargetDate<today, not complete |
| Findings published (last 30d) | Stat | Finding.PublishedAt |
| Recommendations converted to topics | Stat + list | Relationship: recommendation→topic |
| Research conversion rate | Stat: missions → resulting decisions / total | Reporting read model |

**Drill-down:** Mission → `/research/missions/{id}`.

**Filters:** Date range; Mission Status.

**Phase:** Phase 2.

---

### DB-19 — Implementation Follow-Up Dashboard

**Purpose:** Track action completion and verify that decisions are being implemented.

**Audience:** Chairman, Secretary.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Action completion rate (last 30d) | Stat + gauge | Actions completed / total assigned |
| Actions by status | Stacked bar | Action.Status |
| Decision → action traceability gaps | List: decisions with no linked actions | Relationship traversal |
| Actions by stream | Bar | Action.AffectedStreams (via topic) |
| Verified completions vs. self-reported | Stat | Action.Status=Verified vs. Completed |

**Drill-down:** Action row → `/actions/{id}`.

**Filters:** Date range; Stream; Assignee.

**Phase:** Phase 1.

---

### DB-20 — Topic Sources Dashboard

**Purpose:** Understand where topics originate to identify governance drivers.

**Audience:** Chairman, Secretary.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Topics by source type | Donut: member-submitted / stream-requested / secretary-created / external | Topic.SourceType |
| Topics by originating stream | Bar | Topic.OriginStream |
| Submitter activity | Bar: topics submitted per Submitter | Topic.SubmittedByUserId |
| Source → outcome analysis | Grouped bar: source type × decision outcome | Topic + Decision |

**Filters:** Date range; Stream; Topic Type.

**Phase:** Phase 1.

---

### DB-21 — Stream Activity Dashboard

**Purpose:** Per-stream governance activity and burden.

**Audience:** Chairman, Secretary, Stream Directors (via Member access).

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Topics per stream (active) | Bar | Topic.AffectedStreams, active statuses |
| Decisions affecting each stream | Bar | Decision.AffectedStreams |
| Actions by stream | Bar | Action (via Topic.AffectedStreams) |
| Cross-stream dependency volume | Matrix table: stream × stream dependency count | Dependency.SourceStream × TargetStream |

**Filters:** Stream; Date range.

**Phase:** Phase 1.

---

### DB-22 — Bottlenecks Dashboard

**Purpose:** Identify where topics stall, which roles create delays, and which meetings are overloaded.

**Audience:** Chairman, Secretary.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Average time-in-status per stage | Horizontal bar: median days in each status | Topic status transition log |
| Topics stalled in same status >14d | List with status + last-updated | Topic |
| Agenda overload indicator | Bar: agenda items per meeting vs. time available | Agenda + Meeting.DurationMinutes |
| Carry-over accumulation trend | Line: carried-over items per meeting | AgendaItem |

**Filters:** Date range; Stream.

**Phase:** Phase 3 (requires sufficient historical data).

---

### DB-23 — Repeated Architecture Issues Dashboard

**Purpose:** Surface recurring problems — same pattern, same risk, same stream — indicating deeper systemic issues.

**Audience:** Chairman, Secretary.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Topics tagged to same invariant violation | Grouped list by Invariant | Relationship: topic→invariant |
| Recurring risk categories | Tag cloud / bar: Risk.Category frequency | Risk |
| Decisions reversed or superseded | List: Decision→SupersededBy chain | Decision.SupersededBy + ADR.SupersededBy |
| Most-affected systems (by topic count) | Bar | Topic.AffectedSystems |

**Filters:** Date range; Stream; Invariant.

**Phase:** Phase 3.

---

### DB-24 — Governance Maturity Dashboard

**Purpose:** Measure the committee's institutional governance quality over time.

**Audience:** Chairman, Auditor.

**Key widgets:**

| Widget | Visual | Data source |
|---|---|---|
| Governance maturity index (KPI-018) | Gauge + component breakdown | Composite KPI read model |
| ADR coverage over time | Line: % decisions with ADRs | Reporting read model |
| Invariant violations trend | Line | Invariant violation log |
| Decision traceability completeness | Stat: % decisions linked to ≥1 work item | Relationship |
| MoM timeliness | Stat: % MoMs published within SLA | MinutesOfMeeting.PublishedAt − Meeting.Date |

**Filters:** Date range.

**Phase:** Phase 3 (KPI composite requires history).

---

## 3. Scheduled Reports

| Report name | Content | Cadence | Recipients | Delivery |
|---|---|---|---|---|
| Weekly committee digest | DB-01 snapshot: KPIs, overdue actions, upcoming meeting | Weekly (Monday 08:00) | Chairman, Secretary | In-app notification with PDF attachment in MinIO |
| Backlog health report | DB-04 full export | Weekly | Secretary | In-app |
| Monthly governance summary | DB-24 + DB-16 + DB-15 | Monthly (1st of month) | Chairman, Auditor | In-app |
| Overdue escalation report | DB-10 filtered to critical | Daily at 07:00 (if any overdue) | Chairman, Secretary | In-app |

Scheduled reports are Hangfire recurring jobs (CRON). Output is stored in MinIO; a download link is surfaced via in-app notification (no email in v1, see ADR-0005).

---

## 4. Phase Mapping

| Phase | Dashboards delivered |
|---|---|
| **Phase 1** | DB-01, DB-02, DB-03, DB-04, DB-05 (basic), DB-06, DB-07, DB-08, DB-09, DB-10, DB-11, DB-12 (table), DB-15, DB-16, DB-17, DB-19, DB-20, DB-21; Scheduled reports |
| **Phase 2** | DB-18 (Research), DB-12 graph view (Tarseem dependency visualization); Quorum RSVP forecast |
| **Phase 3** | DB-13 (advanced), DB-14, DB-22, DB-23, DB-24 (maturity composite); AI-assisted anomaly flagging `[unverified]` |

---

## 5. Implementation Notes

- Read models are SQL views (or materialized via background refresh) on the same SQL Server instance; no separate analytics DB (ADR-0003).
- Columnstore indexes on reporting tables enable efficient full-table scans for aggregation queries.
- All dashboard data endpoints are versioned REST endpoints (`/api/v1/reports/...`) returning JSON; charts are client-rendered.
- Role authorization on all reporting endpoints enforces same RBAC as the UI (an Auditor cannot see Member-only data via the API).
- Export endpoints stream large result sets to avoid memory pressure; PDF is generated server-side.
- Chart library choice `[unverified]` — validate Recharts RTL/AR axis support before finalizing; alternative candidates: Apache ECharts, Nivo.

---

## Traceability

| Link | Reference |
|---|---|
| Functional requirements | `docs/requirements/functional.md` §Reporting |
| KPIs | `docs/domain/metrics-kpi-catalog.md` (KPI-## catalog) |
| Notification delivery | `docs/domain/notification-strategy.md` (scheduled report notifications) |
| Data model | `docs/domain/data-architecture.md` (read model tables) |
| ADR-0003 | SQL Server + columnstore (no separate analytics DB) |
| ADR-0005 | In-app only in v1; no email delivery |
| ADR-0012 | React 18 + TS frontend; chart lib selection |
| CON-001 | Self-contained: Hangfire (app-owned), MinIO storage, no org BI platform |
| Brief §6.16 | All 22 reporting areas enumerated in DB-01 through DB-24 |
