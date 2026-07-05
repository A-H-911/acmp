---
name: p12-dashboards-reports-plan
description: "P12 Dashboards & Reports — sliced plan, right-sizing ADR, and PR1 (backend reads) DONE."
metadata: 
  node_type: memory
  type: project
  originSessionId: 80e5add4-aec4-4128-96d8-df3f56c45121
---

P12 (Dashboards & Reports) design SoT = `ACMP Dashboards & Reports.dc.html` = **two surfaces** behind a top-bar
toggle: role **Dashboard** (`/`, pending AC-064/065/066 = FR-135/136/137) + tabbed **Reports** (`/reports`, no AC —
reuses P10g `rpt-*` card renderers: bars/columns/stack/stat/matrix). The toggle reconciles to existing nav
(`home` + `reports` already separate items) → no toggle built. Role "Viewing as…" tabs = design PREVIEW affordance;
live app renders the current user's Keycloak role.

**Operator GO (2026-07-05):** build thin registers (not columnstore); start with PR1 backend; record right-sizing ADR.

**Slice plan (GO-gated, 1 PR each):**
- **PR1 backend reads — MERGED** to main (PR #93, squash `20a451b`, all 4 CI green, 2026-07-05). 4 reads: `GetDecisions`
  register `GET /api/decisions` (status/limit; `?topic=` branches to per-topic), `GetVotes` register `GET /api/votes`
  (status; `?topic=` branch), `GetMinutesAwaiting` `GET /api/minutes` (no meeting = cross-meeting InReview queue),
  `Topic.TimesDeferred` counter (domain change — incremented in `Defer()`, on backlog projection; migration
  `Topics_AddTimesDeferred`, single non-null col default 0). +6 tests. All gates green (Domain 188/App 679/ArchUnit
  40/Integration 17/Api 140; format clean; coverage 99.80% per-file pass). **NO AC flips** (backend enablement).
  Also re-indexed ADR-0021/0022 in adr/README (DI-01 hygiene). **Next: push + open PR, monitor CI, operator merge GO.**
- **PR2 (FE) — BUILT + all FE gates green** (branch `feat/P12-pr2-role-dashboards`). **AC-064/065/066 → Met.** New
  `features/dashboard/`: pure `dashboardAgg.ts` (AC-carrying: backlog by bucket+urgency, `nextScheduledMeeting`,
  `actionStatusCounts`, `overdueBeyondThreshold` [ESCALATION_THRESHOLD_DAYS=3, shared AC-065+066], `deferredAtLeastTwice`,
  `slaBreached`) + `dashboardCards.tsx` (DashCard/SegmentBar/StatTiles/KeyList/DashState) + `RoleDashboard.tsx`
  (orchestrator + 3 variants) + `dashboard.css`. `pages/DashboardPage.tsx` now re-exports RoleDashboard. Role pick =
  **Chairman > Secretary > else Committee** (F-19 = role-exclusive, one AC each; "Viewing as" tabs = preview only, not
  built). Tests: `dashboardAgg.test`(11) + `RoleDashboard.test`(14); 802 vitest green, per-file cov (global 99.80%),
  i18n parity 1364, oxlint, build. **Reconciliations (guardrail #14, flagged):** personalized member cards (my
  topics/actions/votes, Mentions) NOT rendered = design extras not AC-required (AC-064 committee-WIDE) + Mentions no
  backing; "escalated actions"=overdue-beyond-threshold per AC-065's own definition (Actions have no Escalated status);
  votes carry no title on wire. **Deleted orphaned `components/ui/Card.tsx`** (only consumer was the old placeholder →
  0% cov). **Committed `eeebc41`, PR #94 — all 4 CI checks green (incl. e2e). Live `.dc.html` pixel-VR PASS**
  (`e2e/p12-dashboard-vr.spec.ts`: real login + API seed; 3 variants × EN-light + AR-dark, pixel-faithful; RTL/dark
  correct; Escalated-actions card proves AC-066 threshold end-to-end; empty cards = fresh-stack seeding limits).
  **Stale-bundle quirk did NOT bite this time** — `e2e:up --build` served the fresh bundle (verified served JS
  contained `dash-greeting`/`secretaryQueue`, no `Role-tailored`). Only remaining: **operator squash-merge GO.**
- **PR3 (FE) — BUILT + all FE gates green** (branch `feat/P12-pr3-reports-shell`). Full Reports IA over 6 view-tabs
  (executive/committee/stream/decisions/actions/audit), NO AC. `reportViews.ts` (pure `buildView`→`ReportCard[]` +
  `viewToCsv`) + `ReportsPage.tsx` rewrite (tabs + Stream filter + CSV export + states + bars/columns/stack/stat/matrix/
  empty renderers). ~16 REAL snapshot cards (aging = histogram of CURRENT ageDays → earns the `columns` renderer;
  reuses P10g matrix/by-stream + dashboardAgg.backlogByBucket); added `columns`+`stack` renderers + `Zone`+='sched'.
  **Honest-empty (flagged): `trend`** (per-week/quarter series, no history — ADR-0022 PH-3) **+ `seam`** (attendance
  not on MeetingSummary; vote attribution not on VoteSummary). **KILLED (advisor): DATA state-tabs** (preview
  affordance) **+ Period filter** (dishonest w/o time series); shipped only the real Stream filter. CSV = current-view
  rows, no PDF. Gates: tsc, 815 vitest (+33), per-file cov 100% on new files/99.80% global, i18n 1456, oxlint, build.
  **REMAINING: live `.dc.html` pixel-VR (`e2e/p12-reports-vr.spec.ts` — executive+committee tabs EN-light+AR-dark) →
  commit/PR/CI → merge GO. ★ P12 COMPLETE after this. ★** Removed superseded `p10g-reports-vr.spec.ts`.

**Right-sizing = [[ADR-0022]]** (client-side aggregation over thin registers; NO columnstore read-model layer; NO chart
library — CSS primitives; **resolves OQ-022**). Deferred + flagged: server PDF export, Hangfire-scheduled reports,
time-series/advanced dashboards (docs/27 DB-13/14/22/23/24) → PH-3; Research dashboard DB-18 → PH-2. CSV = only v1 export.

AC→data map: AC-064 (committee/any-member: backlog by status+urgency ✅, next meeting ✅, action counts ✅, overdue ✅,
last-5 issued decisions ← new register). AC-065 (secretary: Triage count ✅, MoMs-awaiting ← new register, overdue-beyond-
escalation ✅, SLA-breach list via `slaBreached`/`ageDays` ✅). AC-066 (chairman: votes-awaiting ← new register, escalated
risks ✅, escalated actions = interpret overdue>threshold, Deferred≥2 ← new `TimesDeferred`). See [[p10-risks-deps-traceability-plan]] for the P10g reuse surface.
