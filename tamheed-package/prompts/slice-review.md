# Slice completion review

Paste this when a slice of `tamheed-package` is believed complete.
(A PHASE exit is `phase-close.md` — conflating the two is how a phase ships without its gate.)

---

Review the just-completed slice of the `tamheed-package` Tamheed package:

1. `package_open("tamheed-package")`. Identify the slice: `entity_query("slice")` and its
   bound ACs (`trace_query("<SL-x>", direction="in")`).
2. For every AC bound to the slice: verify against the actual code/tests, then
   `audit_record([{"ac_id": ..., "verdict": ..., "evidence":
   "tests/...::test_...; commit <sha>", "verified_by": "human|agent|ci",
   "verification_method": "auto-test|manual|inspection", "against_commit":
   "<sha>"}])`. Partial or Not-met is a legitimate
   verdict — record reality, not aspiration. Verdicts cascade: when all of a
   requirement's ACs are Met it auto-advances.
3. `work_bind` every commit/PR of the slice onto the entities it satisfies.
4. `progress_update` a closing entry for the slice (phase_id + slice_id set,
   event_type "transition", subject_id "<SL-x>", actor "agent:<session>").
5. Anything this slice TAUGHT (a mistake's root fix, a practice to repeat): a
   `lesson` row (`LL-`, born Proposed; kind improve|sustain) + a `learned_from`
   edge to the slice or defect — the operator confirms; only Approved binds.
   Anything discovered-but-deferred: a `deferred-work` row (severity + activation
   trigger), via `entity_upsert` — full rows.
6. Scope deviations found during review: typed `scope-change` row (Proposed, with
   delta edges to the rows it touches) before anything else moves; after operator
   approval, apply the row changes and set the `SC-` Merged.
7. `readiness_check("slice", "<SL-x>")` — items in Review count as open until
   verified here; this review is what moves Review → `lifecycle_status:
   "Implemented"`, and that transition is guarded by these same rules. Resolve
   every blocking failure (ACs not latest-Met, open work items, open critical/high
   defects — medium/low only surface as the defects-minor advisory; never downgrade
   severity to pass) or resolve it deliberately: a `DW-` row (defer), an `SC-` row
   (change scope), or a `WVR-` waiver naming the rule + entity — operator-approved
   only, reported as "waived", and the right ask for one stubborn item.
   `"force": true` overrides the whole transition and exists only on the operator's
   explicit words.
8. `gate_run()` — G-PROGRESS must hold (every AC has a verdict once auditing
   started). `export_html()` to refresh the committed review, report the verdict,
   and STOP at the phase gate for operator approval before the next slice (phase
   exits use the phase-close prompt).
