---
name: tamheed-v4-and-liveness
description: "The v3→v4 store migration, what it renamed/dropped, the FULL-ROW upsert trap over truncated data, and the first register-liveness sweep"
metadata: 
  node_type: memory
  type: project
  originSessionId: 7f0a7303-5ece-41a2-9f0e-cb8560e23cb5
  modified: 2026-08-14T18:49:16.522Z
---

**Migrated 2026-08-14, tamheed 4.1.0 (`7123a1b`).** 4.1.0 refuses a v3 store at open. Backup was
git `967e75d` (clean + pushed); `data-v3-backup/` is **gitignored** — the v3 data is already in
history, so a second 3.9 MB copy is permanent weight. Recover with
`git show 967e75d:tamheed-package/data/<f>.jsonl`.

**Verified mechanically, not trusted:** 3227 → 3229 rows, the delta being exactly the new `waiver`
entity type and the migration's own `PE-334`.

## What changed under you

- `defects` / `deferred_work` / `open_questions`: **`status` → `lifecycle_status`**
- `stakeholders.name` → `title`
- risks: M/H/L stashed to `custom_attributes.v3_probability` / `v3_impact`; the columns are **NULL**
  and the v4 scale was never established — left null rather than invented
- the `relation_rules` advisory is now a **real gate `G-REL`**, and the migration retyped the two
  legacy `ADR-0027 —supersedes→ FR-151/153` edges to `relates_to`, so it passes
- `AC-084` provenance repaired **honestly**: `source_kind` null → `"inferred"`, `source_span` untouched
- ⚠ **six milestone `Approved` statuses dropped and stashed NOWHERE** — only in git / the v3 backup
- new obligations: `OQ-` rows for ambiguity, **`WVR-` waivers are operator-only (never author one)**,
  `lifecycle_status` Review (done-claimed) vs Implemented (verified), and typed `progress_update`
  with a **`correction` event** — the thing `DEF-072` needed and could not have

## ⚠ The trap that matters

**`entity_upsert` requires FULL rows in v4** (a partial update is refused: *NOT NULL constraint
failed … INSERT evaluates NOT NULL before conflict resolution*) — **and the store holds truncated
data**. `RISK-001`…`012` titles are exactly 200 chars, cut mid-word, from the v2.3 damage. So
rebuilding a row from what `entity_query` just handed you **re-commits the truncation and calls it a
fix**. **Read `data/*.jsonl` when a field may be damaged.**

Those titles ARE recoverable: full text (236–395 chars) lives in `custom_attributes.v1.Risk`.
⚠ Omitting `custom_attributes` on an update still **preserves** the `v1` blob — verified by upserting
`RISK-002` and re-reading the JSONL. (Sending it still replaces the whole blob — the old G1 hazard.)

## The first liveness sweep (`ae9291f`)

`owner` + `response_strategy` populated on 11 risks, **every value recovered from
`custom_attributes.v1.Owner`**, never assigned. `risk-liveness` moved `indeterminate` → **pass**.
A rule that cannot discriminate is not a green light, it is a broken instrument.

**Deliberately not done:** `RISK-013`…`024` have no recorded owner anywhere (fabricating one is
`DEF-010`'s manufactured-status failure); `resolved_by` left null on 26 answered OQs.

⚠ **An amber lied about its own size:** `open-questions-resolved` lists 72 rows — the tally is
**48 Deferred, 26 Approved, 1 Implemented, and exactly ONE genuinely Proposed (`OQ-074`)**.
`OQ-070` carries a long evidenced resolution and is Approved yet still counts, because `resolved_by`
is null on every v2.3-imported row. **It measures bookkeeping, not open questions.**

Detail + what is left for the operator: **`findings_17.md`**. See also
[[verify-mechanically-not-carefully]], [[tamheed-data-repair]].
