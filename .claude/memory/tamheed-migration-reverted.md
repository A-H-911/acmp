---
name: tamheed-migration-reverted
description: The 2026-07-21 Tamheed v2 migration/cutover (PR #153) was fully REVERTED next day — docs/ (Keystone v1) is again the system of record; do not trust #153's cutover notes
metadata:
  type: project
---

**The Tamheed migration was undone (2026-07-22).** PR #153 (`4433bd4`) migrated `docs/` into a
Tamheed v2 store at `tamheed-package/` and cut over (AGENTS.md/CLAUDE.md rewired, docs/ frozen).
The operator then ordered a full undo: reverted on `revert/tamheed-migration` and merged, restoring
the tree byte-identical to `efabddb`.

**Why:** operator decision after evaluating the migrated store; the run itself succeeded (gates 7/7,
fidelity clean) — the revert is a choice, not a failure. Feedback for the Tamheed owner lives in
`findings_2.md` at the repo root (untracked, `.git/info/exclude`d).

**How to apply:**
- **`docs/` (Keystone v1) is the system of record again** — the old tracking protocol stands:
  edit acceptance-audit/progress-log/status-report markdown, validate with the keystone
  `1.0.0` `validate_package.py` (7/7). Ignore any Tamheed-cutover instructions found in the
  history of AGENTS.md/CLAUDE.md or in merged-PR #153's description.
- `tamheed-package/`, `handoff/`, root `.mcp.json` must NOT exist on main; the CI paths-ignore
  entries for them were reverted too.
- A re-migration, if ever ordered, is cheap and deterministic: the staged flow + the 15-row
  status patch are documented in `findings_2.md` and in this session's plan file
  (`~/.claude/plans/docs-mode-migrate-package-dir-lovely-dolphin.md`).
