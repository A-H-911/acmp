# ADR-0022: Reporting is client-side aggregation over thin registers — no columnstore read-model layer, no chart library (extends ADR-0001, ADR-0003; right-sizes docs/27 §5)

- Status: Accepted
- Date: 2026-07-05
- Deciders: Architecture Committee execution (operator GO 2026-07-05; secretary to ratify)
- Extends / right-sizes: ADR-0001 (modular monolith), ADR-0003 (SQL Server + columnstore), docs/27 §5, docs/28

## Context and Problem Statement

P12 (Dashboards & Reports) is specified in docs/27 §1.1/§5 with a dedicated **Reporting read-model layer** — SQL views/materialized tables with **columnstore indexes**, versioned `/api/v1/reports/...` aggregation endpoints, **server-side PDF** export, and **Hangfire-scheduled** report delivery. docs/28 lists 21 KPIs; ADR-0012/OQ-022 assume a **client chart library** (Recharts, with an RTL spike).

For the actual footprint — a **single committee, on-prem, ≤20 users** (CON-001), with every register holding a few hundred rows at most — that layer is disproportionate. P10g already delivered the Risk & Dependency reports by composing three existing REST reads **client-side** (`features/reports/reportAgg.ts`), and P10f the impact graph the same way, both recorded as ADR-0001-consistent (the SPA composing public API results is not a module reading another's tables). This ADR settles, once, how P12 is built so the doc-vs-code gap is deliberate (guardrail #11), not silently skipped like OQ-043/RowVersion once was.

## Decision Drivers

- Guardrail #12 (do not overengineer; right-size for ≤20 users) vs. docs/27 §5's enterprise BI shape.
- P10f/P10g precedent: client-side aggregation over full-register fetches is already shipped, tested, and fast at this scale.
- The `.dc.html` visual source of truth (`ACMP Dashboards & Reports.dc.html`) renders **every** chart as CSS primitives — bars (`width%`), columns (`height%`), stacked segments, stat grids, a 3×3 matrix — with **zero** chart-library dependency. RTL is handled by logical properties, which a charting lib would fight.
- The genuine gap is not "columnstore vs nothing" but "which specific cards need data the registers don't carry." Counts and current-state groupings are cheap client-side; only **time-series / derived-history** cards (throughput-per-week, decision lead time, attendance %, audit-event volume) need server support — and those are PH-3 advanced analytics per docs/27 §4, not PH-1.

## Decision Outcome

Chosen: **client-side aggregation over thin, plain paged registers; no read-model/columnstore layer; no chart library.**

1. **No Reporting read-model tables, no columnstore, no `/api/v1/reports/*` aggregation endpoints in v1.** Dashboards and reports are composed in the SPA (`features/reports/*`, `features/dashboard/*`) from the modules' own public register reads.
2. **Where a dashboard needs data a register doesn't expose, add a plain paged register read to the owning module** — never a cross-schema aggregate. P12-PR1 adds exactly four: `GET /api/decisions` (committee-wide register), `GET /api/votes` (register, status-filterable), `GET /api/minutes` (InReview approval queue), and a `Topic.TimesDeferred` counter on the backlog projection. Each reads only its own module's tables (ADR-0001) and carries read-all authorization.
3. **Charts are CSS primitives**, composed from the shared design system (the `rpt-*` renderers P10g established) — bars/columns/stack/stat/matrix. **No Recharts/ECharts/Nivo.** This **resolves OQ-022**: the RTL question is moot because there is no chart library to validate; RTL comes from logical properties + the existing `dir` mirroring, verified by the live `.dc.html` VR.
4. **Export = client-side CSV only in v1.** Server-side **PDF** export and **Hangfire-scheduled** reports are deferred to PH-3 (they have no design surface and no ≤20-user justification yet), flagged in the progress log.
5. **Advanced / time-series dashboards** (docs/27 DB-13/14/22/23/24; the Research dashboard DB-18 = PH-2) are deferred; PH-1 cards that would need absent time-series data render **honest-empty** and are flagged, not faked.

### Consequences

- Good: no analytics DB, no columnstore, no BI machinery, no chart-lib bundle weight or RTL risk; the design's CSS-primitive charts are reproduced exactly; every number traces to an already-authorized public read. Aggregating a few hundred rows in the browser is imperceptible at this scale.
- Good: the four new registers are reusable beyond dashboards (they are the long-planned "Lists & Registers" reads) and feed the Reports tabs directly.
- Trade-off: aggregation runs on the client, so a report over the *whole* register fetches it fully. Accepted — registers are bounded (no register exceeds one page at ≤20 users; P10g verified `pageSize=500` covers the set). A server aggregate is the upgrade path **iff** a register ever outgrows a single page.
- Trade-off: docs/27 §5 and ADR-0003's "reporting via columnstore" now describe a **future** state, not v1. Recorded here rather than edited away; ADR-0003's datastore choice is unchanged (still SQL Server) — only the reporting-layer shape is right-sized.
- Trade-off: time-series cards are honest-empty until PH-3. Accepted and flagged — a visibly empty card is more truthful than a faked trend, and matches the design's own empty state.

## Validation

- P10f/P10g already run this pattern in production paths with per-file ≥95% coverage.
- P12-PR1's four register reads ship with handler tests (decisions register scope + status filter + limit; votes register status filter; minutes InReview queue; `TimesDeferred` counter across reactivation).
- ArchUnit continues to enforce module isolation — each new read lives in its owning module and touches only its own schema.
- The CSS-primitive charts are verified against `ACMP Dashboards & Reports.dc.html` by the live screenshot VR in P12-PR2/PR3.

## Links / Notes

- Extends ADR-0001 (module isolation), right-sizes ADR-0003 (columnstore reporting → future) and docs/27 §5 / docs/28.
- Resolves **OQ-022** (chart library + RTL) → no chart library; CSS primitives.
- Defers to PH-3: server PDF export, Hangfire-scheduled reports, advanced/time-series dashboards (docs/27 DB-13/14/22/23/24); PH-2: Research dashboard DB-18.
- Precedent: P10f (`ADR-0020`, read-time composition) and P10g (`features/reports/reportAgg.ts`).
- First consumed by P12-PR1 (the four registers); the dashboards/reports UI follows in P12-PR2/PR3.
