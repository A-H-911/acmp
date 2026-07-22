---
name: tamheed-data-repair
description: The v2.3 migration passed 7/7 gates while silently damaging register data at column level; repair is in flight on fix/tamheed-data-repair with Tasks 0-1 done
metadata:
  type: project
---

**The Tamheed v2.3 migration (PR #157, `be63211`) passed every gate while damaging the
register half of the package.** Gates read 7/7, counts matched, the audit rollup survived
exactly — because **every check is row-level and the damage is column-level**. Verified
2026-07-22.

**Twelve defect classes** (full evidence + suggested `migrate.py` fixes in `findings_4.md`
§D, which is git-excluded and lives in the working tree only). The load-bearing ones:

- **`title_fallbacks` is NOT cosmetic.** `_map_register_row` falls back to `_clean_line`
  (caps 120, strips `[#*\`>-]` incl. **every ASCII hyphen**) and then writes
  `"statement": title` — so the fallback rows' full text is **destroyed, not just their
  title**. findings_3 B4 asked for this ledger to be *grouped* because it was noisy; 2.3.0
  did that and everyone called it fixed. **Grouping a data-loss signal makes it quieter,
  not safer.**
- **~415 fields truncated** at exactly 120 or 200. Recoverable where `statement` holds the
  full text (ACs, constraints, risks…); **NOT recoverable** for 78 `requirements.statement`
  and 133 WBS leaf titles.
- **`DW-` ids ≠ `D-` ids** — renumbered by *row position* over an unsorted source:
  `DW-015=D-16 · DW-016=D-17 · DW-017=D-18 · DW-018=D-19 · DW-019=D-15 · DW-020=D-21 ·
  DW-021=D-22 · DW-022=D-20`. True `#` survives only in `custom_attributes.v1`.
- **`docs/planning/roadmap.md` was consumed into 4 phase rows and discarded** — the P1–P19
  ladder and the **Legacy token map** (which warns that `P1–P4` in `backlog.md` are
  *priority codes* and the design package's `P12`/`P15` are a different scheme) existed
  nowhere in the package.
- **Permanent dead-end:** ACs were set `Approved` on import *before* slices existed, so the
  immutability trigger blocks `acceptance_criteria.slice_id` forever ⇒ **`v_phase_exit`
  returns 0/0/0 for every phase and cannot be fixed** short of superseding 74 ACs.

**Two execution guards that no gate would catch if violated:**

- **G0 — no `progress_update`/`work_bind` before the PE- backfill.** Both auto-allocate via
  `_next_id("PE-", …)`; one call claims `PE-001`, which the backfill then **silently
  overwrites**. `AGENTS.md`'s "track at each phase gate" convention actively invites this.
- **G1 — never send `custom_attributes` on an update.** It **replaces the whole JSON**,
  erasing the `{"v1": …}` blob that is the only copy of the data being recovered. Omitting
  it preserves it (`tamheed_server.py:262` — the `DO UPDATE SET` clause touches only the
  columns you send, despite the docstring saying "send FULL rows").

**Where it stands:** branch `fix/tamheed-data-repair`, plan at
`~/.claude/plans/based-on-the-generated-wobbly-badger.md` (rev.3, operator-approved).
**Task 0 + Task 1 done and committed** (`26ec872`, `bd20407`) — **zero package writes so
far**. `tools/tamheed-repair/` holds five committed extractors and 411 reviewed payload
rows in `reports/`. Tasks 2–9 do the writing and were gated on operator review of those
payloads.

**How to apply:** repairs go **only** through MCP `entity_upsert` — `package_migrate`
refuses to re-run once `data/` exists, and `packages` is absent from `ENTITY_TABLES` so the
package header (`go_no_go: "GO"` vs the real CONDITIONAL NO-GO) is **unfixable by any
tool**. Rollback is **git only** — every successful write flushes JSONL immediately. Never
defeat the AC/ADR immutability trigger with an `Approved→Proposed→edit→Approved` dance.
See [[tamheed-migration-reverted]].
