---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# ACMP — execution-ready package

This directory is the **Keystone v1.0.0 planning & governance package** for the Architecture Committee Management Platform (ACMP). It is the single source of truth for *what* to build, *why*, *which decisions are settled*, *what remains open*, and *how* to execute — traceable end-to-end. The running application lives in `../src`; the agent control surface is [`../AGENTS.md`](../AGENTS.md) (imported by [`../CLAUDE.md`](../CLAUDE.md)).

## How an agent should consume this package

1. Read [`../AGENTS.md`](../AGENTS.md) — invariants, hard constraints, conventions, and where the build currently stands.
2. Read [`00-charter.md`](00-charter.md) — problem, objectives, scope, success metrics.
3. Read [`architecture/architecture.md`](architecture/architecture.md) — modules, seams, data & deployment.
4. Read [`planning/roadmap.md`](planning/roadmap.md) — the phases and their gates.
5. Read [`validation/acceptance-criteria.md`](validation/acceptance-criteria.md) — the `AC-` you must satisfy.
6. Start from [`handoff/initial-prompt.md`](handoff/initial-prompt.md); check current state in [`progress/status-report.md`](progress/status-report.md).

**ADRs and approved registers are FINAL** — supersede via a new ADR, never edit in place.

## Reading order by question

| If you want… | Open |
|---|---|
| The problem & scope | [00-charter.md](00-charter.md) · [01-executive-summary.md](01-executive-summary.md) |
| What must be built | [requirements/functional.md](requirements/functional.md) · [requirements/non-functional.md](requirements/non-functional.md) |
| What must never change | [requirements/invariant-register.md](requirements/invariant-register.md) · [requirements/constraint-register.md](requirements/constraint-register.md) |
| Why it's built this way | [adrs/](adrs/) · [decisions/open-decision-register.md](decisions/open-decision-register.md) |
| What's unresolved | [decisions/open-question-register.md](decisions/open-question-register.md) · [decisions/assumption-register.md](decisions/assumption-register.md) |
| How it's structured | [architecture/architecture.md](architecture/architecture.md) · [architecture/technology-comparison.md](architecture/technology-comparison.md) |
| The plan & work | [planning/roadmap.md](planning/roadmap.md) · [planning/work-breakdown.md](planning/work-breakdown.md) · [execution/backlog.md](execution/backlog.md) |
| How it's verified | [validation/acceptance-criteria.md](validation/acceptance-criteria.md) · [validation/test-strategy.md](validation/test-strategy.md) · [validation/traceability-matrix.md](validation/traceability-matrix.md) |
| What could go wrong | [risks/risk-register.md](risks/risk-register.md) |
| Where the build is | [progress/status-report.md](progress/status-report.md) · [validation/acceptance-audit.md](validation/acceptance-audit.md) |
| Identifiers & conventions | [governance/naming-conventions.md](governance/naming-conventions.md) · [governance/glossary.md](governance/glossary.md) |
| The deep domain/design detail | [domain/](domain/) (extension docs — domain model, workflows, threat model, dashboards, IA, …) |

## The non-negotiables (read these first)

The 14 invariants (`INV-001…014`) in [requirements/invariant-register.md](requirements/invariant-register.md) are hard rules a violation of which requires a new ADR: modular monolith on the mandated .NET/React/SQL-Server stack; self-contained deployment; policy-based authz + SoD; append-only hash-chained audit with immutable votes/decisions/ADRs/minutes; EN/AR + RTL first-class; no secrets in source; branch→PR→green-CI; design fidelity to the local `.dc.html` references.

## Status

Package status: **Approved**, under Keystone v1.0.0 governance. Build state: **MVP (PH-1) complete; PH-2 substantially delivered through P12**; `main` green and deployable. Mechanical gates are verified by `python <keystone>/scripts/validate_package.py docs` — see [handoff/execution-readiness-report.md](handoff/execution-readiness-report.md).

## Package layout

```
docs/
├── README.md · 00-charter.md · 01-executive-summary.md
├── requirements/   functional · non-functional · constraint · invariant · dependency registers
├── decisions/      open-question · open-decision · assumption registers
├── architecture/   architecture · technology-comparison · diagrams/
├── adrs/           adr-0001…0022 + index
├── risks/          risk-register
├── planning/       roadmap · work-breakdown
├── execution/      backlog · definition-of-done · deferred-work-register · checkpoints
├── validation/     acceptance-criteria · test-strategy · traceability-matrix · acceptance-audit · ph0-validation · rebuild-findings
├── progress/       progress-log · status-report · design-parity-ledger
├── handoff/        initial-prompt · follow-up-prompts · review-prompts · execution-readiness-report · handoff-manifest.json
├── governance/     governance · naming-conventions · contributing · glossary
├── domain/         35 ACMP domain & design extension docs (governed, outside the Keystone gated set)
├── manifest.json · keystone-state.json
```
