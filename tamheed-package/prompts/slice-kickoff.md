# Slice kickoff — start the next slice, plan-first

Paste this to start execution of the next open slice of `tamheed-package` (semi-auto: plans
stop for your approval).

---

Kick off the next slice of the `tamheed-package` Tamheed package:

1. `package_open("tamheed-package")` if not already open, then `gate_run()` — a failing gate
   is fixed or explained before new work starts.
2. Find the next open slice: `entity_query("slice")` + `entity_query("phase")` in
   roadmap order — the first slice not in a terminal status whose phase is active.
3. Read its contract: `entity_query("execution-plan")` for the slice's `EP-` row, the
   bound criteria via `trace_query("<SL-x>", direction="both")` and
   `entity_query("acceptance-criterion")`, and the invariants in force
   (`entity_query("invariant")`).
4. Propose a bounded, acceptance-criteria-first plan: per `AC-`, the failing test you
   will write, the implementation step, and the pass/fail observable. **STOP for
   operator approval before writing any code.**
5. After approval, per unit of work: failing test → implement → `progress_update`
   (event_type "work-done", subject_id the `WBS-`/`AC-`, actor "agent:<session>") →
   `audit_record` with evidence plus its chain (`verified_by`, `verification_method`,
   `against_commit`) → `work_bind` the commit.
6. Anything discovered along the way follows the recording obligations: defect →
   `defect` row FIRST; out-of-scope → `deferred-work` row with a trigger; deviation →
   `scope-change` row FIRST; genuine ambiguity → an `OQ-` row plus a
   `[NEEDS-CLARIFICATION: OQ-NNN]` marker at the spot — never assume.
7. When the slice looks done: set the finished `wbs-item` rows to
   `lifecycle_status: "Review"` (done-claimed; `Implemented` means verified, and
   readiness counts Review as open), then run the slice-review prompt (it ends with
   `readiness_check("slice", "<SL-x>")`).
