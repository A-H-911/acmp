---
name: tamheed-v4-and-liveness
description: "The v3→v4 store migration, what it renamed/dropped, the FULL-ROW upsert trap over truncated data, and the first register-liveness sweep"
metadata: 
  node_type: memory
  type: project
  originSessionId: 7f0a7303-5ece-41a2-9f0e-cb8560e23cb5
  modified: 2026-08-15T19:07:25.384Z
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

---

## 4.2.0 (2026-08-15) — three findings_17 items closed, two traps learned

`open-questions-resolved` **72 → 1** (`OQ-074`) on the 4.2.0 rule fix — B1 was a correct diagnosis.
Closed: **C3** (risk scale, `low|medium|high`, recovered from `custom_attributes.v3_*`), **A4**
(11 truncated titles restored from `custom_attributes.v1.Risk`), **A5** (six milestone statuses
stashed as `custom_attributes.v3_status`, blob **merged** not replaced). C2 re-verified: omitting
`custom_attributes` preserves the blob across 34 full-row upserts.

⚠⚠ **A GENERATED PAYLOAD MUST BE PASTED, NOT RE-TYPED.** C1 says build from the JSONL — done — but
I then hand-transcribed the generator's output into the tool call and flipped `RISK-012`'s
probability from `high` to `medium`. **The hand is the untrusted transport.** Care did not catch it;
a verifier that re-read the JSONL and re-derived every value from the stash did, in one line.
**End any N-row repair with that independent re-read.**

⚠⚠ **A HOLLOW `pass` IS WORSE THAN AN `indeterminate`.** `risk-liveness` flipped to `pass` when I
populated owners — but `probability`/`impact` were null, so **no row could satisfy its
high-probability/high-impact predicate**; it passed because it could not fail. With the scale
recovered it correctly **fails**, naming `RISK-013/016/017/018/019/020`. An `indeterminate`
announces itself; a rule that cannot discriminate reports green. Recorded as a typed `correction`
event against `PE-335` — the v4 mechanism `DEF-072` needed and could not have.

⚠ **Customising a stock prompt opts it out of every future refresh, silently and permanently.**
4.2.0 changed `slice-review.md`; `refresh_stock` correctly skipped ours because it is customised, so
it now lags. Three files are in that state (`orient-resume`, `integrity-check`, `slice-review`).

---

## 4.3.0 (2026-08-15) — the `lesson` family, and one dangerous tool warning

**Order matters: `package_migrate` FIRST.** A package created before 4.3.0 has no `lesson` row in
its type registry, so a lesson write fails on the registry FK. `package_migrate("tamheed-package")`
previews **`mode: "registry-sync"`, `entity_types_added: ["lesson"]`** — a pure append, no backup
taken, no data transform. Applied diff was literally two lines.

⚠⚠ **`handoff_emit` MUST AIM AT `tamheed-package`, NOT THE REPO ROOT — AND THE ROOT-AIMED WARNING IS
ACTIVELY DANGEROUS.** The marker-managed span lives in `tamheed-package/CLAUDE.md`
(`tamheed:note vN` … `/tamheed:note`). Aimed at the repo root, `handoff_emit` matched the project's
**own unmarked `## Tamheed progress tracking` heading** — the *import* prose — reported it as a "v1
note", and advised **deleting that section and re-running**. Following that would delete the passage
explaining why the note is imported rather than restated, which exists *because* a duplicated copy
went two generations stale. **The fix is the target directory, not the file.** Recorded in `PE-364`;
probably a tamheed bug (heading-match without requiring the markers).

**The `lesson` schema** (`db/migrations/002_lessons.sql`): `title`+`statement` NOT NULL, `kind` is
**`improve|sustain`**, plus `context`/`recommendation`/`rationale`/`category`/`impact_if_followed`/
`impact_if_ignored`; `lifecycle_status` **`Proposed|Approved|Rejected|Superseded|Obsolete`** — **no
Draft, no Deferred** (an undecided lesson should keep nagging). Edges: **`learned_from`** is locked
to `lesson →` {`defect`,`decision`,`risk`,`slice`,`wbs-item`,`progress-entry`}; write one with
`entity_upsert({type:"trace-edge", from_id, to_id, relation})` — `trace-edge` is upsertable even
though it is not in `entity_types.jsonl`. Same-batch endpoints are visible to the FK.

⚠ **`trg_lessons_immutable` freezes `confirmed_by`/`confirmed_at` too** — so **approval + attribution
+ pin must go in ONE upsert**. Approve first and the attribution can never be added.
⚠ **And the approving write itself is unguarded**: the trigger fires only when OLD is *already*
Approved, so content drift on the write that approves is accepted **silently**. Save a pre-image and
verify byte-identity after — this is [[an-absence-needs-a-proven-instrument]]'s sibling and is
exactly what **`LL-001`** (Approved + **pinned**, `learned_from → PE-336`) tells you to do. Pinned
lessons render in the tool-owned note under *"Lessons (operator-confirmed — these bind every
session)"*.
