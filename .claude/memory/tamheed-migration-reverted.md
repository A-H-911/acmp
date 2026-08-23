---
name: tamheed-migration-reverted
description: Tamheed migration history — reverted twice (2.1.0 #153, 2.2.0 #155), then run a THIRD time on plugin 2.3.0 (2026-07-22, zero-repair, full cutover) on feat/tamheed-v23-migration; check PR/merge state to know the current system of record
metadata:
  type: project
---

**Tamheed migration history: two reverted cycles, then a third 2.3.0 run (operator-ordered, full cutover).**

**Cycle 1 (plugin 2.1.0):** PR #153 (`4433bd4`, 2026-07-21) migrated `docs/` → `tamheed-package/`
+ full cutover. Reverted next morning (#154). Feedback → `findings_2.md`.

**Cycle 2 (plugin 2.2.0):** PR #155 (`eb23226`, 2026-07-22): zero-repair-loop migration (one
4-row phase patch) + full cutover; all CI green. Reverted same day (#156, tree restored to
`410277e` state). Feedback → `findings_3.md`.

**Cycle 3 (plugin 2.3.0, 2026-07-22):** operator explicitly ordered a third run with full
cutover on branch `feat/tamheed-v23-migration`. **Zero repair actions** — 2.3.0's
`status_defaulted` ledger fixed the phase-Draft asymmetry (no patch needed), grouped
`status_coerced_groups`/`title_fallbacks` shipped, stale-warning self-removes on clean re-emit,
prompts report `unchanged`. Gates 7/7, fidelity clean, audit 62/11/1 (73 evidenced/1 narrated)
preserved, determinism re-proven (sha1-identical across idle open/close). Operator status_map:
Instrumented/Instrumented (P12)/Met/Met (ADR-0009)→Implemented (KPI-001..005),
Living→Approved (DOC-052); semantic defaults accepted. Feedback → `findings_4.md` (all
findings_3 items B1–B8 verified FIXED). All findings files are `.git/info/exclude`d.

**Why:** cycles 1–2 were reverted by operator choice, not tool failure. Cycle 3 was a fresh
explicit order to test 2.3.0.

**How to apply:**
- **Check the PR/merge state of `feat/tamheed-v23-migration` before assuming the system of
  record.** If merged: Tamheed package + MCP tools are the record, `docs/` is a frozen archive
  (see AGENTS.md/CLAUDE.md on that branch). If reverted again: `docs/` (Keystone v1) markdown
  protocol + keystone `1.0.0` validator stand.
- Never hand-edit `tamheed-package/` files — MCP tools are the only write path.
- Do NOT re-run the migration on your own initiative; each run requires a fresh explicit
  operator order.
- The staged flow + operator decisions are documented in `findings_4.md` and the plan file
  (`~/.claude/plans/docs-mode-migrate-package-dir-mossy-zephyr.md`).
