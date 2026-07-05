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
- **PR1 backend reads — DONE**, branch `feat/P12-pr1-report-reads`, commit `b67fd47` (unpushed). 4 reads: `GetDecisions`
  register `GET /api/decisions` (status/limit; `?topic=` branches to per-topic), `GetVotes` register `GET /api/votes`
  (status; `?topic=` branch), `GetMinutesAwaiting` `GET /api/minutes` (no meeting = cross-meeting InReview queue),
  `Topic.TimesDeferred` counter (domain change — incremented in `Defer()`, on backlog projection; migration
  `Topics_AddTimesDeferred`, single non-null col default 0). +6 tests. All gates green (Domain 188/App 679/ArchUnit
  40/Integration 17/Api 140; format clean; coverage 99.80% per-file pass). **NO AC flips** (backend enablement).
  Also re-indexed ADR-0021/0022 in adr/README (DI-01 hygiene). **Next: push + open PR, monitor CI, operator merge GO.**
- **PR2 (FE, next)** — role Dashboard at `/` (3 variants: Secretary/coordinator, Chairman, Member) from these
  registers + existing reads. **Closes AC-064/065/066.** Live `.dc.html` VR. Open decisions for PR2: fallback dashboard
  for the other 5 roles (default → Committee dashboard, flag it); AC-064 (any member) content ≠ design's personalized
  "member" card set → compose AC-required committee data using design card patterns + flag design→behavior additions;
  "mentions" card has no backing system → honest-empty/omit.
- **PR3 (FE)** — Reports shell: wrap P10g `/reports` into full design IA (6 view-tabs, filters, CSV export, data-states,
  add `columns`+`stack` renderers); honest-empty for time-series cards. Live VR.

**Right-sizing = [[ADR-0022]]** (client-side aggregation over thin registers; NO columnstore read-model layer; NO chart
library — CSS primitives; **resolves OQ-022**). Deferred + flagged: server PDF export, Hangfire-scheduled reports,
time-series/advanced dashboards (docs/27 DB-13/14/22/23/24) → PH-3; Research dashboard DB-18 → PH-2. CSV = only v1 export.

AC→data map: AC-064 (committee/any-member: backlog by status+urgency ✅, next meeting ✅, action counts ✅, overdue ✅,
last-5 issued decisions ← new register). AC-065 (secretary: Triage count ✅, MoMs-awaiting ← new register, overdue-beyond-
escalation ✅, SLA-breach list via `slaBreached`/`ageDays` ✅). AC-066 (chairman: votes-awaiting ← new register, escalated
risks ✅, escalated actions = interpret overdue>threshold, Deferred≥2 ← new `TimesDeferred`). See [[p10-risks-deps-traceability-plan]] for the P10g reuse surface.
