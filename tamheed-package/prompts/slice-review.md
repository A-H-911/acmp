# Slice / phase completion review

Paste this when a slice or phase of `tamheed-package` is believed complete.

---

Review the just-completed slice/phase of the `tamheed-package` Tamheed package:

1. `package_open("tamheed-package")`. Identify the slice: `entity_query("slice")` and its
   bound ACs (`trace_query("<SL-x>", direction="in")`).
2. For every AC bound to the slice: verify against the actual code/tests, then
   `audit_record([{"ac_id": ..., "verdict": ..., "evidence":
   "tests/...::test_...; commit <sha>"}])`. Partial or Not-met is a legitimate
   verdict — record reality, not aspiration. Verdicts cascade: when all of a
   requirement's ACs are Met it auto-advances.
3. `work_bind` every commit/PR of the slice onto the entities it satisfies.
4. `progress_update` a closing entry for the slice (phase_id + slice_id set).
5. Anything discovered-but-deferred: a `deferred-work` row (severity + activation
   trigger), via `entity_upsert` — full rows.
6. Scope deviations found during review: typed `scope-change` row before anything
   else moves.
7. `gate_run()` — G-PROGRESS must hold (every AC has a verdict once auditing
   started). `export_html()` to refresh the committed review, report the verdict,
   and STOP at the phase gate for operator approval before the next slice.
