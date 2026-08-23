---
name: package-mechanics-proven-2026-08-18
description: "Tamheed v4 store mechanics established by experiment on 2026-08-18 — acs-met's real predicate, entity_upsert's preserve-vs-require rule, vacuous slice wbs-done, and the relation rules that refuse plausible edges"
metadata: 
  node_type: memory
  type: reference
  originSessionId: 364f1e3b-2b4b-4521-9978-f980586f48bc
  modified: 2026-08-18T15:12:24.473Z
---

Mechanics **proven by running them**, not read from docs. All verified 2026-08-18 against tamheed 4.4.1.

## `acs-met` counts by `retired_in`, NOT by `lifecycle_status`

```sql
SELECT ac.id FROM acceptance_criteria ac
LEFT JOIN v_latest_verdicts lv ON lv.ac_id = ac.id
WHERE ac.retired_in IS NULL AND (lv.verdict IS NULL OR lv.verdict <> 'Met')
```

- **A `Deferred` AC still counts.** Only *retirement* removes one. Confirmed against live data: 96 active
  ACs including 6 still `Proposed`, all latest-`Met`, rule passes.
- ⚠ So **writing ACs for unbuilt work holds package readiness false indefinitely** — a `Pending` verdict
  never satisfies it. This killed "write an AC for all 161" as a DW-029 mechanism.
- During any build window `acs-met` fails on exactly the batch's Pending ACs. **Expected, not a
  regression** — reconcile the failing id list against your batch; an unexpected id is a real finding.
  Never resolve it by upgrading a verdict.

## `entity_upsert`: omitting PRESERVES, but NOT NULL fields are required

Established by deliberate experiment on one row before touching 24:

- `{type, id, lifecycle_status}` alone → **refused**: `NOT NULL constraint failed: requirements.kind`.
- Sending only the **NOT NULL** columns → succeeds, and every omitted nullable field
  (`statement`, `priority`, `rationale`, `verification_method`, `custom_attributes`, `disposition`)
  is **preserved byte-identically**.
- So a status-only update needs: `id, kind, title, mvp, lifecycle_status, source_kind, source_span,
  introduced_in` for a requirement. Nothing else.
- **The trap-14 verifier is cheap and caught nothing only because it was run**: hash a pre-image of
  every field except the one you're changing, re-derive after, assert byte-identity. Did this twice
  (24 requirements, 4 WBS items); both byte-identical.

## Slice-scope `wbs-done` is vacuous for every pre-existing slice

`SELECT id FROM wbs_items WHERE slice_id = ?` — and **all 155 wbs_items have `slice_id` NULL**
(135 also have `phase_id` NULL). So the rule returned zero rows for all 28 closed slices and could
never have failed. Recorded as `DEF-087`.

- Phase scope joins `(w.phase_id = ? OR s.phase_id = ?)` and therefore sees only 20 items.
- ⚠ **This also breaks the obvious AC→slice binding mechanism** — you cannot derive an AC's slice from
  `requirement --implements--> WBS --> slice_id`. That path returns nothing.
- New WBS rows created this session set `slice_id` deliberately, so `SL-029` closed against a
  non-empty set.

## `G-TRACE` wants three legs; the advisory wants one

A new mvp=1 requirement went red on `G-TRACE` while `requirements_unwired` **passed** — they are not
the same rule. `g_trace_failures` requires links to **all three** of a `decision`-or-`adr`, a
`wbs-item`-or-`slice`, and a `test`. Wire all three in the same write.

## `RELATION_RULES` refuse plausible edges

`lesson --learned_from--> deferred-work` is **rejected**; allowed targets are
`decision, defect, progress-entry, risk, slice, wbs-item`. The error names the allowed sets — it is the
only documentation. ⚠ The whole batch rolls back, so re-send the **entire** corrected batch.

## Other

- `disposition` is `superseded|accepted-with-deviation|void` — it **cannot** express "not separately
  verifiable". `verification_method` (`Test|Demonstration|Inspection|Analysis`) is the ISO-29148 field
  for that and is **NULL on all 225 requirements**, as is `rationale`.
- Requirement status advances **only** via `trg_requirement_auto_advance`, which fires on an AC's `Met`
  verdict — so a requirement with no AC can never leave `Approved` however well it shipped.
- Setting `due_by` on new `OQ-` rows made `open-questions-overdue` discriminating for the first time
  (it had been `indeterminate` at 0 of 76).
