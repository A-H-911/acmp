---
name: keystone-package-migration
description: The /docs planning package was migrated to Keystone v1.0.0 foldered format + mechanics (validator-green); paths and layout changed repo-wide.
metadata: 
  node_type: memory
  type: project
  originSessionId: b076c5a6-ff08-4880-b927-c90d80b69fb5
---

**2026-07-06 — branch `chore/keystone-package-migration`.** The hand-built ACMP planning package was reshaped into the **Keystone v1.0.0** package format and mechanics. This changed the repo layout and hundreds of path references — recall this before citing any `docs/NN` path.

**New layout (package root = `docs/`):** `requirements/` (functional/non-functional/constraint/**invariant NEW**/dependency), `decisions/` (open-question/open-decision(DEC←R-##)/assumption), `architecture/` (architecture/technology-comparison/diagrams), `adrs/` (ADRs moved from `/adr`, case-renamed `adr-NNNN-*.md`), `risks/`, `planning/` (roadmap/work-breakdown WBS←EPIC/BL), `execution/` (backlog/DoD/**deferred-work-register NEW**/checkpoints), `validation/` (acceptance-criteria/test-strategy TEST-/**traceability-matrix NEW**/acceptance-audit/ph0-validation/rebuild-findings), `progress/` (progress-log/status-report/design-parity-ledger), `handoff/` (initial/follow-up/review prompts + readiness + handoff-manifest.json), `governance/` (governance/naming-conventions/contributing/glossary), `domain/` (35 ACMP domain+design **extension** docs — the old `docs/02–34,43` etc., kebab-renamed, keep their own `W-/EPIC-/US-/PAIN-/DB-` IDs). Plus `00-charter.md`, `01-executive-summary.md`, `README.md`, `manifest.json`, `keystone-state.json`.

**Agent control:** root `CLAUDE.md` is now a **thin loader** that imports new **`AGENTS.md`** (the real standing brief — invariants `INV-001…014`, constraints, tracking protocol, current-phase pointer → `progress/status-report.md`). The 14 execution guardrails became the `INV-` register.

**Mechanics:** verified by `python <keystone marketplace>/plugins/keystone/scripts/validate_package.py docs` → **all 7 critical gates PASS**. That validator is the gate for any future package change. Keystone plugin is installed as a marketplace plugin at v1.0.0. See [[phase-prompt-standard-footer]] and [[always-stage-claude-memory-in-commits]].

**Old→new path map** (for translating stale references): `docs/07`→`requirements/functional.md`, `docs/08`→`requirements/non-functional.md`, `docs/40`→`validation/acceptance-criteria.md`, `docs/41`→split across risks/requirements/decisions registers, `docs/42`→decisions registers, `docs/36`→`planning/roadmap.md`, `docs/44`→`execution/definition-of-done.md`, `docs/10`→`domain/permission-role-matrix.md`, `docs/11`→`domain/domain-model.md`, `docs/13`→`domain/workflows.md`; `/adr/ADR-NNNN`→`docs/adrs/adr-NNNN` (bare `ADR-####` IDs unchanged). Reference: `docs/governance/naming-conventions.md`.
