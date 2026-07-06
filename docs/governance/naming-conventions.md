---
status: Approved
version: 1.1.0
updated: 2026-07-06
owner: lead-secretary
---

# Naming Conventions — ACMP

The single source of truth for how every artifact in this Keystone package is named, identified, versioned, and cross-referenced. Consistent identifiers are what make the [traceability matrix](../validation/traceability-matrix.md) and the handoff trustworthy.

## Governed Keystone identifiers

Prefix + zero-padded number, unique within the package, never reused (retire, don't recycle). These are the identifiers the mechanical validator (`scripts/validate_package.py`, gate G-IDS) enforces.

| Entity | ID format | Lives in |
|---|---|---|
| Functional requirement | `FR-NNN` | [requirements/functional.md](../requirements/functional.md) |
| Non-functional requirement | `NFR-NNN` | [requirements/non-functional.md](../requirements/non-functional.md) |
| Constraint | `CON-NNN` | [requirements/constraint-register.md](../requirements/constraint-register.md) |
| Invariant | `INV-NNN` | [requirements/invariant-register.md](../requirements/invariant-register.md) |
| Assumption | `ASM-NNN` | [decisions/assumption-register.md](../decisions/assumption-register.md) |
| Dependency | `DEP-NNN` | [requirements/dependency-register.md](../requirements/dependency-register.md) |
| Open question | `OQ-NNN` | [decisions/open-question-register.md](../decisions/open-question-register.md) |
| Decision (lightweight) | `DEC-NNN` | [decisions/open-decision-register.md](../decisions/open-decision-register.md) |
| Architecture Decision Record | `ADR-NNNN` (4 digits) | [adrs/](../adrs/) |
| Risk | `RISK-NNN` | [risks/risk-register.md](../risks/risk-register.md) |
| Hypothesis / Experiment / POC | `HYP-NNN` / `EXP-NNN` / `POC-NNN` | research/experiments/pocs (omitted — see manifest) |
| Success metric / KPI | `KPI-NNN` | [00-charter.md](../00-charter.md) |
| Stakeholder | `STK-NNN` | [00-charter.md](../00-charter.md) / domain/stakeholders.md |
| Phase | `PH-N` | [planning/roadmap.md](../planning/roadmap.md) |
| Milestone | `MS-NNN` | planning/roadmap.md |
| Work item (WBS) | `WBS-N[.N[.N]]` | [planning/work-breakdown.md](../planning/work-breakdown.md) |
| Acceptance criterion | `AC-NNN` | [validation/acceptance-criteria.md](../validation/acceptance-criteria.md) |
| Test / validation item | `TEST-NNN` | [validation/test-strategy.md](../validation/test-strategy.md) |

`DEC` vs `ADR`: use `DEC-` for a decision in the open-decision register; **promote** it to an `ADR-NNNN` when it is architecturally significant. Record the promotion (`DEC-001 → ADR-0001`) in the register's "Promoted to" column so the link is never lost.

## ACMP extension identifiers (intentional; outside the governed set)

ACMP's domain and design docs (under [domain/](../domain/)) carry richer, ACMP-specific ID families. They are **deliberate extensions**, catalogued here so they read as intentional — the Keystone validator does not govern them. Do not "fix" them into Keystone prefixes.

| Prefix | Meaning | Home |
|---|---|---|
| `W-##` | Committee workflow | domain/workflows.md |
| `EPIC-##` | Epic | domain / planning (mapped to `WBS-N` groups) |
| `US-###` | User story | execution/backlog.md |
| `BL-###` | Backlog item | execution/backlog.md (mapped to `WBS-N.N` leaves) |
| `PAIN-##` | Pain point | domain/pain-points.md |
| `DB-##` | Dashboard | domain/reporting-dashboards.md |
| `T-##` / `AB-##` / `TB-##` / `A#` | Threat / abuse-case / trust-boundary / asset | domain/security-threat-model.md |
| `C-<AREA>-##` | Security control | domain/security-controls.md |
| `SoD-#` | Segregation-of-duties rule | domain/permission-role-matrix.md |
| `OOS-##` / `D-##` | Out-of-scope / deferred item | execution/deferred-work-register.md |
| `P##` | Build slice (execution ladder, `P1…P19`; also branch names `feat/P{n}-<slug>`) | planning/roadmap.md §Build-slice ladder |
| `R-##` | *(legacy)* resolved-decision alias — **now migrated to `DEC-NNN`** | decisions/open-decision-register.md |

## Runtime (in-app) entity keys

Distinct from planning identifiers — these are human-readable, year-scoped keys the running application mints for governance records. They are product data, **not** planning artifacts, and are out of scope for this package's governance.

`TOP-YYYY-###` topic · `MTG-YYYY-###` meeting · `AGN-…` agenda · `MIN-…` minutes · `VOTE-…` · `DECN-…` committee decision · `ACT-…` action · `RSK-…` risk · `DPN-…` dependency edge · `ADR-…` in-app ADR · `AIV-…` architecture invariant · `DOC-…` · `TPL-…` template · `DGM-…` diagram · `RMS-…` research mission · `FND-…` finding · `REC-…` recommendation.

> Note: the in-app `ADR-` / `AIV-` runtime record types are distinct from this package's planning `ADR-NNNN` files under [adrs/](../adrs/).

## Lifecycle statuses

Package artifacts follow: `Draft → Proposed → Approved → Implemented`, with branches `Rejected`, `Deferred`, and `Superseded → Obsolete`. **Decision statuses are exactly** `Proposed | Approved | Rejected | Superseded | Deferred`; ADRs additionally use `Accepted`. A *proposed* item is never rendered as *approved*.

Product runtime status models (Topic, committee-decision outcome, Action, in-app ADR, Architecture Invariant, Risk, Vote) live in [architecture/architecture.md](../architecture/architecture.md) and [domain/entity-lifecycles.md](../domain/entity-lifecycles.md).

## File & directory naming

- Files and directories: **kebab-case**, ASCII, no spaces.
- Ordered narrative docs: `NN-topic.md` (`00-charter.md`). Registers: `<thing>-register.md`. ADRs: `adr-NNNN-short-title.md`.
- One entity family per register file; one ADR per file. Links between files use **relative Markdown paths**.

## Versioning & supersession

- Each generated document carries front-matter `status` / `version` / `updated`; bump `version` on material change.
- **Immutable-after-approval** (ADRs, approved acceptance criteria): never edit meaning in place — supersede with a new item; set `supersedes`/`superseded_by` on both ends (the old item stays, status `Superseded`). Typo fixes are allowed.
- **Derived** artifacts ([traceability matrix](../validation/traceability-matrix.md), [status report](../progress/status-report.md), [execution-readiness report](../handoff/execution-readiness-report.md), [acceptance audit](../validation/acceptance-audit.md), AGENTS.md) are regenerated from sources, never hand-maintained.
