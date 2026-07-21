---
name: tamheed-v2-migration
description: docs/ (Keystone v1) migrated to Tamheed v2 relational package at tamheed-package/ — how, the gotchas, and what stays authoritative
metadata:
  type: project
---

# Tamheed v2 migration (2026-07-21)

`docs/` (Keystone v1, validator 7/7) was migrated to a **Tamheed v2 relational package** at
`tamheed-package/` (JSONL per table under `data/`, plus `review.html` — the only human review surface).
**`docs/` remains the authoritative planning package**; moving governance authority to tamheed-package is an
open operator decision, deliberately NOT taken.

**Contents:** 218 requirements, 74 ACs (full GWT statements), 74 audit verdicts (**73 evidenced** from the
audit's "Test ref" column, 1 narrated), 33 ADRs, 28 decisions, 58 OQs, 155 WBS items, 4 phases,
**19 slices SL-001…019 (= P1…P19, P14 Deferred/DEC-028)**, **23 deferred-work DW-001…023 (= D-01…D-23)**,
1,032 trace edges. Final `gate_run`: all gates pass, `ready: true`.

**How it was driven (⚠ transport wedge):** the tamheed plugin's MCP server hangs on Windows whenever a tool
handler calls `subprocess.run` — the child python freezes at 0 CPU (reproduced twice; kill the child pair to
unwedge). Workaround: drive the server's **plain handler functions in-process**
(scratchpad `drive_migrate.py` / `backfill_run.py`: `sys.path.insert(server dir)`, `import tamheed_server as
ts; ts.PACKAGE_ROOT = Path(repo)`) — same code/validation, and it pins the package root explicitly.

**Why:** for future `update`-mode syncs (progress_update/audit_record/work_bind after each slice), expect the
same wedge for `package_migrate`/anything spawning subprocesses; plain `entity_upsert`/`gate_run` calls may
work over MCP but the in-process driver is the proven path.

**Migration gotchas (all hit, all solved):**
- **ADRs have no YAML front-matter** (`# ADR-NNNN:` heading + `- Status:` bullet) → vanilla migrator maps
  ZERO ADRs and its fidelity report does NOT flag it; worse, `decisions.promoted_to REFERENCES adrs(id)`
  makes populate FAIL outright. Fix: parse ADRs separately and `plan.add("adrs", …)` BEFORE populate.
  Section mapping trap: "Decision **Drivers**" precedes "Decision **Outcome**" — match Outcome first.
- **DEC-028 promoted_to**: its "Promoted to" cell is `n/a (… DEC-019 …)` → parser grabs DEC-019 → FK fail.
  Null non-ADR promoted_to values (raw cell survives in custom_attributes).
- **Immutability triggers**: approved ACs and recorded verdicts refuse UPDATE ("supersede, never edit") —
  full statements + evidence must be injected **in-plan at INSERT time**, not upserted after.
- **G-COMPLETE placeholder scan** (`{{…}}`, TODO, TBD) scans ALL text columns incl. custom_attributes —
  D-22's literal JSX `style={{}}` trips it; reword everywhere in the row.
- **Failed populate leaves a poison empty `tamheed-package/data/`** → delete dir before retry.
- **G-TRACE passes vacuously**: v1 priorities are M/S/C/W so every requirement migrated `mvp=0` — the green
  gate does NOT prove MVP traceability.

**CI:** `tamheed-package/**` added to paths-ignore (ci/e2e/security) + gitleaks path allowlist;
`tamheed-package/data/.lock` git-ignored. Related: [[p19-release-readiness]].
