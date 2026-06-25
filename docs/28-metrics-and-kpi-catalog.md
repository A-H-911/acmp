# 28 — Metrics & KPI Catalog (Deliverable 36)

**Purpose:** Define every ACMP KPI — precise calculation, source data, thresholds, cadence, and the governance decisions each metric should drive — so the committee can measure platform value and process health objectively.

> All KPI identifiers follow the planning-package scheme `KPI-##` (README §F). Targets are **recommendations** (`[unverified]` where no empirical baseline exists for this committee); the secretary should tune them after the first 3-month operational period.

---

## 1. Catalog Structure

Each entry:

| Field | Description |
|---|---|
| **ID** | `KPI-##` |
| **Name** | Short human-readable label (EN) |
| **Purpose** | What governance behavior this metric should drive |
| **Definition / Calculation** | Precise formula; reference to entities from `docs/11-domain-model.md` |
| **Source data** | Tables / read models queried |
| **Target / Threshold** | Green / Amber / Red thresholds `[unverified]` |
| **Cadence** | How often recalculated; display granularity |
| **Audience** | Roles who act on this metric |
| **Drives decision / action** | The governance behavior change expected when the metric is outside threshold |

---

## 2. Backlog Health

### KPI-01 — Backlog Size

| Field | Value |
|---|---|
| **Purpose** | Detect backlog overload; signal need for triage cadence increase or WIP limit enforcement |
| **Definition** | Count of `Topic` records where `Status ∈ {Submitted, Triage, Accepted, Prepared}` at the measurement timestamp |
| **Source data** | `Topic` table, reporting read model snapshot |
| **Target** | Green ≤15 · Amber 16–25 · Red >25 `[unverified]` |
| **Cadence** | Daily snapshot; displayed on DB-02 and DB-04 |
| **Audience** | Chairman, Secretary |
| **Drives** | Chairman/Secretary schedule extra triage sessions; raise `RISK-` for backlog pressure |

---

### KPI-02 — WIP Count (In-Committee)

| Field | Value |
|---|---|
| **Purpose** | Ensure the committee does not context-switch across too many simultaneous active topics |
| **Definition** | Count of `Topic` where `Status = InCommittee` at the measurement timestamp |
| **Source data** | `Topic` |
| **Target** | Green ≤5 · Amber 6–8 · Red >8 `[unverified]` |
| **Cadence** | Real-time (or 60s stale-while-revalidate on dashboard) |
| **Audience** | Chairman, Secretary |
| **Drives** | Secretary moves excess topics to Accepted/Deferred before scheduling new InCommittee items |

---

### KPI-03 — Backlog Aging (Median Days in Status)

| Field | Value |
|---|---|
| **Purpose** | Surface average stall time across the backlog; identify which status stage creates the most delay |
| **Definition** | For each `Status` in {Submitted, Triage, Accepted, Prepared}: median of (`NOW() - Topic.StatusEnteredAt`) in days, for topics currently in that status |
| **Source data** | Topic status-transition log (AuditEvent or dedicated `TopicStatusHistory` table) |
| **Target** | Per-status SLA targets (set by Secretary; recommended baselines): Submitted ≤3d · Triage ≤5d · Accepted ≤21d · Prepared ≤7d `[unverified]` |
| **Cadence** | Daily; displayed as bar chart on DB-05 |
| **Audience** | Chairman, Secretary |
| **Drives** | Secretary identifies which stage is bottlenecked; escalates with Chairman to clear queue |

---

### KPI-04 — Stale Topic Rate

| Field | Value |
|---|---|
| **Purpose** | Detect topics that are active but not progressing |
| **Definition** | (Count of `Topic` with `UpdatedAt < NOW() - 14 days` AND `Status ∈ {Submitted, Triage, Accepted, Prepared}`) / (Total active backlog count) × 100% |
| **Source data** | `Topic.UpdatedAt`, reporting read model |
| **Target** | Green <10% · Amber 10–25% · Red >25% `[unverified]` |
| **Cadence** | Weekly |
| **Audience** | Secretary |
| **Drives** | Secretary contacts owners of stale topics for status update; triggers automated nudge notifications |

---

## 3. Throughput

### KPI-05 — Decision Throughput

| Field | Value |
|---|---|
| **Purpose** | Measure committee productivity; ensure governance output keeps pace with intake |
| **Definition** | Count of `Decision` records where `Status = Published` AND `PublishedAt` is within the measurement period (week / month) |
| **Source data** | `Decision`, reporting read model |
| **Target** | Weekly: Green ≥2 · Amber 1 · Red 0 (assuming weekly meetings) `[unverified]` |
| **Cadence** | Weekly rolling (displayed as 12-week line chart on DB-13) |
| **Audience** | Chairman, Secretary |
| **Drives** | Chairman assesses whether meeting format/frequency needs adjustment; Secretary reviews agenda preparation quality |

---

### KPI-06 — Topic Intake Rate

| Field | Value |
|---|---|
| **Purpose** | Track governance demand growth; ensure intake does not permanently exceed throughput |
| **Definition** | Count of `Topic` records with `CreatedAt` in the measurement period (week / month) |
| **Source data** | `Topic` |
| **Target** | Contextual; flag when Intake Rate > Decision Throughput (KPI-05) for 3+ consecutive weeks `[unverified]` |
| **Cadence** | Weekly |
| **Audience** | Chairman, Secretary |
| **Drives** | Secretary adjusts triage criteria; Chairman considers raising meeting cadence or adding extraordinary sessions |

---

### KPI-07 — Carry-Over Rate

| Field | Value |
|---|---|
| **Purpose** | Detect agenda preparation failures; excess carry-over signals overloaded agendas or insufficient preparation |
| **Definition** | (Count of `AgendaItem` with `Status = CarriedOver` for a given meeting) / (Total `AgendaItem` count for that meeting) × 100% |
| **Source data** | `AgendaItem` |
| **Target** | Green ≤10% · Amber 11–25% · Red >25% `[unverified]` |
| **Cadence** | Per-meeting; rolling 8-week trend on DB-13 |
| **Audience** | Chairman, Secretary |
| **Drives** | Secretary reduces agenda item count or extends time-boxes; Chairman reviews readiness criteria before scheduling |

---

## 4. Decision Lead Time & Cycle Time

### KPI-08 — End-to-End Decision Lead Time

| Field | Value |
|---|---|
| **Purpose** | Measure committee responsiveness from submission to decision; primary governance SLA |
| **Definition** | For decisions published in the period: median of (`Decision.PublishedAt - Topic.SubmittedAt`) in calendar days |
| **Source data** | `Topic.SubmittedAt`, `Decision.PublishedAt`; joined via `Topic.DecisionId` |
| **Target** | Green ≤30d · Amber 31–60d · Red >60d `[unverified]` |
| **Cadence** | Monthly rolling (requires minimum 10 decisions for statistical meaning; suppress display until then) |
| **Audience** | Chairman |
| **Drives** | If Red: Chairman initiates process improvement — reduce triage backlog, improve agenda density, reduce carry-over |

---

### KPI-09 — Decision Cycle Time (In-Committee to Published)

| Field | Value |
|---|---|
| **Purpose** | Measure committee deliberation efficiency (excludes backlog wait) |
| **Definition** | For decisions published in the period: median of (`Decision.PublishedAt - Topic.InCommitteeAt`) in calendar days |
| **Source data** | Topic status-transition log (`StatusEnteredAt` for `InCommittee`), `Decision.PublishedAt` |
| **Target** | Green ≤7d · Amber 8–14d · Red >14d `[unverified]` |
| **Cadence** | Monthly rolling |
| **Audience** | Chairman, Secretary |
| **Drives** | Long cycle time → Chairman investigates multi-session deferrals; Secretary improves pre-meeting preparation |

---

## 5. Action Health

### KPI-10 — Action Completion Rate

| Field | Value |
|---|---|
| **Purpose** | Measure follow-through on committee decisions; a committee that decides but does not complete actions loses credibility |
| **Definition** | (Count of `Action` where `Status ∈ {Completed, Verified}` AND `DueDate` in the measurement period) / (Count of `Action` with `DueDate` in the measurement period) × 100% |
| **Source data** | `Action.Status`, `Action.DueDate` |
| **Target** | Green ≥90% · Amber 75–89% · Red <75% `[unverified]` |
| **Cadence** | Monthly (too volatile weekly); displayed as trend on DB-19 |
| **Audience** | Chairman, Secretary |
| **Drives** | If Red: Chairman escalates with assignees' stream directors; Secretary tightens reminder cadence |

---

### KPI-11 — Action Overdue Rate

| Field | Value |
|---|---|
| **Purpose** | Detect systemic failure to close actions on time |
| **Definition** | (Count of `Action` where `DueDate < NOW()` AND `Status ∉ {Completed, Verified, Cancelled}`) / (Total `Action` where `Status ∉ {Cancelled}`) × 100% |
| **Source data** | `Action.Status`, `Action.DueDate` |
| **Target** | Green <5% · Amber 5–15% · Red >15% `[unverified]` |
| **Cadence** | Daily snapshot; real-time DB-10 |
| **Audience** | Chairman, Secretary |
| **Drives** | Immediate escalation notifications; Chairman discusses in next meeting |

---

### KPI-12 — Verified Completion Rate

| Field | Value |
|---|---|
| **Purpose** | Distinguish self-reported completion from independently verified completion |
| **Definition** | (Count of `Action` with `Status = Verified`) / (Count of `Action` with `Status ∈ {Completed, Verified}`) × 100% |
| **Source data** | `Action.Status` |
| **Target** | Green ≥70% verified · Amber 40–69% · Red <40% `[unverified]` |
| **Cadence** | Monthly |
| **Audience** | Chairman, Secretary |
| **Drives** | If Red: Secretary reinforces verification step; Chairman may require peer verification for critical actions |

---

## 6. Risk

### KPI-13 — Open Critical Risk Count

| Field | Value |
|---|---|
| **Purpose** | Ensure critical risks are actively mitigated, not forgotten |
| **Definition** | Count of `Risk` where `Severity = Critical` AND `Status = Open` (i.e., not yet Mitigating or Closed) |
| **Source data** | `Risk.Severity`, `Risk.Status` |
| **Target** | Green 0 · Amber 1–2 · Red ≥3 `[unverified]` |
| **Cadence** | Daily; stat card on DB-01 and DB-11 |
| **Audience** | Chairman |
| **Drives** | Any critical open risk → immediate agenda slot for risk response planning |

---

### KPI-14 — Risks Without Mitigation Plans

| Field | Value |
|---|---|
| **Purpose** | Ensure every open risk has an active mitigation, not just acknowledgement |
| **Definition** | Count of `Risk` where `Status ∈ {Open, Mitigating}` AND `Mitigations.Count = 0` |
| **Source data** | `Risk`, `Mitigation` |
| **Target** | Green 0 · Amber 1–3 · Red >3 `[unverified]` |
| **Cadence** | Weekly |
| **Audience** | Secretary, Chairman |
| **Drives** | Secretary assigns risk owners and requests mitigation plans; Secretary creates linked Actions |

---

## 7. Participation & Quorum

### KPI-15 — Attendance Rate

| Field | Value |
|---|---|
| **Purpose** | Ensure committee is sufficiently attended; low attendance undermines decision legitimacy |
| **Definition** | (Sum of `Attendance.Present = true` across all meetings in period) / (Sum of `CommitteeMembership.IsVotingEligible` × meetings held in period) × 100% |
| **Source data** | `Attendance`, `CommitteeMembership`, `Meeting` |
| **Target** | Green ≥80% · Amber 65–79% · Red <65% `[unverified]` |
| **Cadence** | Per-meeting; rolling 12-week trend on DB-17 |
| **Audience** | Chairman |
| **Drives** | If trending Red: Chairman reviews meeting schedule/format; may switch to bi-weekly (CON: currently weekly) |

---

### KPI-16 — Quorum Compliance Rate

| Field | Value |
|---|---|
| **Purpose** | Validate that votes were taken with legitimate quorum |
| **Definition** | (Count of `Vote` where `QuorumMet = true`) / (Total `Vote` records where `Status ∈ {Closed, Ratified}`) × 100% |
| **Source data** | `Vote.QuorumMet` |
| **Target** | Green 100% · Amber 90–99% · Red <90% `[unverified]` — note: any non-quorate vote is an audit event |
| **Cadence** | Per-vote; monthly aggregate on DB-16 |
| **Audience** | Chairman, Auditor |
| **Drives** | Any non-quorate vote triggers a review; persistent Red → meeting format change |

---

## 8. ADR & Governance Coverage

### KPI-17 — ADR Coverage Rate

| Field | Value |
|---|---|
| **Purpose** | Ensure decisions that should be formalized as ADRs are being recorded |
| **Definition** | (Count of `Decision` where `Status = Published` AND a `Relationship` edge of type `recorded-as` points to an `ADR` with `Status ∈ {Proposed, Approved}`) / (Count of `Decision` where `Status = Published` AND `TopicType = ArchitectureDecision`) × 100% |
| **Source data** | `Decision`, `Relationship` (type=recorded-as), `ADR`, `Topic.TopicType` |
| **Target** | Green ≥90% · Amber 70–89% · Red <70% `[unverified]` |
| **Cadence** | Monthly; stat on DB-15 and DB-01 |
| **Audience** | Chairman, Auditor |
| **Drives** | Secretary identifies decisions missing ADRs and creates ADR drafting actions |

---

### KPI-18 — Governance Maturity Index

| Field | Value |
|---|---|
| **Purpose** | Composite index quantifying the committee's overall governance quality; intended for trend monitoring, not comparison to external benchmarks |
| **Definition** | Weighted composite score (0–100): `(KPI-17_score × 0.25) + (KPI-10_score × 0.20) + (KPI-08_score × 0.15) + (KPI-16_score × 0.15) + (KPI-15_score × 0.10) + (InvariantCoverage_score × 0.10) + (MoMTimeliness_score × 0.05)` where each component score = min(100, (actual/target) × 100). Weights are configurable `[unverified]`. |
| **Source data** | Composite of KPI-08, KPI-10, KPI-15, KPI-16, KPI-17 read models + `MinutesOfMeeting.PublishedAt - Meeting.Date` for MoM timeliness |
| **Target** | Green ≥80 · Amber 60–79 · Red <60 `[unverified]` — calibrate after 6 months of data |
| **Cadence** | Monthly; displayed as gauge on DB-01 and DB-24 |
| **Audience** | Chairman |
| **Drives** | Trend is more important than absolute value; declining trend over 3 months triggers process review |

---

### KPI-19 — Invariant Violation Rate

| Field | Value |
|---|---|
| **Purpose** | Detect architecture decay — systems repeatedly violating standing invariants |
| **Definition** | Count of new `InvariantViolation` records created in the measurement period where `Status = Active` |
| **Source data** | `InvariantViolation` (linked to `Invariant` aggregate) |
| **Target** | Green 0 · Amber 1–2 · Red >2 per period `[unverified]` |
| **Cadence** | Monthly |
| **Audience** | Chairman, Auditor |
| **Drives** | Repeated violations of same invariant → dedicated architectural remediation topic |

---

## 9. Research & Knowledge

### KPI-20 — Research Conversion Rate

| Field | Value |
|---|---|
| **Purpose** | Ensure research missions generate actionable outputs, not just documentation |
| **Definition** | (Count of `ResearchMission` where `Status = Completed` AND at least one `Recommendation` linked via `Relationship` type `informs` to a `Topic` that reached `Status ∈ {Decided, Closed}`) / (Count of `ResearchMission` where `Status = Completed`) × 100% |
| **Source data** | `ResearchMission`, `Recommendation`, `Relationship`, `Topic` |
| **Target** | Green ≥60% · Amber 40–59% · Red <40% `[unverified]` — new metric; baseline unknown |
| **Cadence** | Quarterly |
| **Audience** | Chairman, Secretary |
| **Drives** | Low rate → Secretary reviews whether research missions are scoped to actionable questions |

---

### KPI-21 — MoM Publication Timeliness

| Field | Value |
|---|---|
| **Purpose** | Ensure minutes are published promptly while memory is fresh and actions are visible |
| **Definition** | Median of (`MinutesOfMeeting.PublishedAt - Meeting.EndedAt`) in hours, for MoMs published in the period |
| **Source data** | `MinutesOfMeeting.PublishedAt`, `Meeting.EndedAt` |
| **Target** | Green ≤48h · Amber 49–72h · Red >72h `[unverified]` |
| **Cadence** | Per-meeting; monthly median trend |
| **Audience** | Secretary, Chairman |
| **Drives** | Slow publication → Secretary adjusts MoM drafting process; may trigger in-meeting note-taking improvements |

---

## 10. KPI Summary Reference Table

| ID | Name | Owner | Cadence | Dashboard |
|---|---|---|---|---|
| KPI-01 | Backlog Size | Secretary | Daily | DB-02, DB-04 |
| KPI-02 | WIP Count (InCommittee) | Secretary | Real-time | DB-02 |
| KPI-03 | Backlog Aging (Median per Status) | Secretary | Daily | DB-05 |
| KPI-04 | Stale Topic Rate | Secretary | Weekly | DB-04 |
| KPI-05 | Decision Throughput | Chairman | Weekly | DB-01, DB-13 |
| KPI-06 | Topic Intake Rate | Secretary | Weekly | DB-13 |
| KPI-07 | Carry-Over Rate | Secretary | Per-meeting | DB-13 |
| KPI-08 | End-to-End Decision Lead Time | Chairman | Monthly | DB-14 |
| KPI-09 | Decision Cycle Time (In-Committee) | Chairman | Monthly | DB-14 |
| KPI-10 | Action Completion Rate | Secretary | Monthly | DB-19 |
| KPI-11 | Action Overdue Rate | Secretary | Daily | DB-10 |
| KPI-12 | Verified Completion Rate | Secretary | Monthly | DB-19 |
| KPI-13 | Open Critical Risk Count | Chairman | Daily | DB-01, DB-11 |
| KPI-14 | Risks Without Mitigation | Secretary | Weekly | DB-11 |
| KPI-15 | Attendance Rate | Chairman | Per-meeting | DB-17 |
| KPI-16 | Quorum Compliance Rate | Chairman, Auditor | Per-vote | DB-16 |
| KPI-17 | ADR Coverage Rate | Auditor | Monthly | DB-15 |
| KPI-18 | Governance Maturity Index | Chairman | Monthly | DB-01, DB-24 |
| KPI-19 | Invariant Violation Rate | Auditor | Monthly | DB-24 |
| KPI-20 | Research Conversion Rate | Secretary | Quarterly | DB-18 |
| KPI-21 | MoM Publication Timeliness | Secretary | Per-meeting | DB-17 |

---

## 11. Vanity Metrics We Deliberately Exclude

The following metrics are explicitly excluded because they measure activity, not governance quality. Including them risks gaming behavior, distraction, and false assurance.

| Excluded metric | Why excluded |
|---|---|
| **Total topics ever submitted** | Raw cumulative count says nothing about governance health; grows monotonically regardless of quality |
| **Total login count / session count** | Platform engagement is not the goal; governance outcomes are. A silent committee that decides well is better than an active one that does not |
| **Total comments posted** | Comment volume is activity noise; high volume may indicate confusion, not value |
| **Total attachments uploaded** | File count is not a proxy for decision quality |
| **Total notifications sent** | Notification volume is a system activity metric; high volume may indicate too many reminders, a sign of poor action follow-through |
| **Total ADRs ever created** | Without coverage rate (KPI-17) and approval rate, raw ADR count is meaningless |
| **Time-on-platform per user** | ACMP is a tool, not a destination; minimal time on platform achieving governance outcomes is a feature |
| **Number of dashboard views** | Consumption of reports is not equivalent to acting on them |

These are excluded from all default dashboards and the governance maturity index. If a specific audit need requires activity counts, they are available in raw export only.

---

## 12. Threshold Calibration Note

All targets above are marked `[unverified]` — they are derived from general governance best practice and right-sizing for ≤20 users. The secretary **must** baseline-measure all KPIs for the first 90 days of operation before enforcing thresholds. Thresholds should be reviewed and confirmed (or adjusted) at the 3-month and 6-month post-launch governance reviews. KPI threshold configurations are stored as application configuration (editable by Administrator/Secretary); no redeploy required to adjust a threshold.

---

## Traceability

| Link | Reference |
|---|---|
| Dashboard definitions | `docs/27-reporting-and-dashboards.md` (each KPI is surfaced in at least one DB-##) |
| Domain entities | `docs/11-domain-model.md` (Topic, Decision, Action, Risk, Vote, etc.) |
| Status models | `README.md §E` (Topic, Action, ADR, Risk, Vote status enumerations) |
| Acceptance criteria | `docs/40-acceptance-criteria.md` (KPI thresholds are acceptance gate inputs) |
| ADR-0003 | SQL Server columnstore for KPI read model queries |
| Brief §6.16 | KPI catalog directly supports all 22 reporting areas |
