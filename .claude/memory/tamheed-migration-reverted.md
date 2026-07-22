---
name: tamheed-migration-reverted
description: Tamheed v2 migration attempted TWICE (2.1.0 PR #153, 2.2.0 PR #155) and fully REVERTED both times (2026-07-22) — docs/ (Keystone v1) is the system of record; do not re-migrate without an explicit operator order
metadata:
  type: project
---

**The Tamheed migration has been undone twice; `docs/` (Keystone v1) is the system of record.**

**Cycle 1 (plugin 2.1.0):** PR #153 (`4433bd4`, 2026-07-21) migrated `docs/` → `tamheed-package/`
+ full cutover. Reverted next morning (#154, tree restored byte-identical to `efabddb`).
Feedback → `findings_2.md` (repo root, `.git/info/exclude`d).

**Cycle 2 (plugin 2.2.0):** same day (2026-07-22) the operator ordered a from-scratch re-run —
PR #155 (`eb23226`): zero-repair migration (2.2.0's status_coerced/title_fallbacks ledgers +
status_map fixed the 2.1.0 findings; gates 7/7, fidelity clean, audit 62/11/1 preserved, all 9
CI checks green) + full cutover. **The operator then ordered a full undo again** — reverted on
`revert/tamheed-migration-2`, tree restored byte-identical to `410277e`. Feedback →
`findings_3.md` (repo root, `.git/info/exclude`d; regression report on the 2.1.0 findings +
new items: phase-status Draft asymmetry, handoff_emit stale-warning permanence/unconditional
prompt rewrite, ledger noise, PE rows never synthesized from the v1 progress log).

**Why:** operator choice both times, not tool failure — both runs succeeded mechanically.

**How to apply:**
- **The old tracking protocol stands:** edit acceptance-audit/progress-log/status-report
  markdown, validate with the keystone `1.0.0` `validate_package.py` (7/7). Ignore
  Tamheed-cutover instructions in the history of AGENTS.md/CLAUDE.md or PRs #153/#155.
- `tamheed-package/`, `handoff/`, root `.mcp.json` must NOT exist on main; the CI paths-ignore
  entries for `tamheed-package/**` were reverted too.
- **Do NOT re-run the migration on your own initiative** — twice ordered, twice undone;
  treat any future migrate command as requiring a fresh explicit operator instruction.
- A re-run, if ordered, is cheap and deterministic (~zero repair on 2.2.0+): staged flow +
  operator decisions are documented in `findings_3.md` and the plan file
  (`~/.claude/plans/docs-mode-migrate-package-dir-cosmic-rabbit.md`). Operator status choices
  last time: Instrumented/Met→Implemented (KPIs), Living→Approved, PH-0..3→Approved via patch.
