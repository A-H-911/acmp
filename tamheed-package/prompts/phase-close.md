# Phase close — the guarded exit, done deliberately

Paste this to close a phase of `tamheed-package` (semi-auto; distinct from slice-review —
this is the phase-level exit with the guarded transition at the end).

---

Close phase `<PH-x>` of the `tamheed-package` Tamheed package:

1. `package_open("tamheed-package")` if not already open.
2. `readiness_check("phase", "<PH-x>")` — resolve every blocking failure: ACs of the
   phase's slices not latest-Met, slices not closed, open work items, open defects.
   Each resolution is recorded (verdicts with evidence, statuses via full-row upserts,
   waivers via `scope-change`/`deferred-work` rows) — never narrated away.
3. Milestones of the phase (`entity_query("milestone")`): mark reached ones
   Implemented; unreached ones are either a blocking conversation with the operator or
   an explicit `scope-change`.
4. `human_required` gates for the phase: read each `GATE-` definition to the operator,
   get explicit confirmation, record it as a `progress_update` naming the `GATE-` id.
5. The transition: upsert the phase full-row with `lifecycle_status: "Implemented"`.
   If the guard refuses, the blockers are real — resolve them. `"force": true` exists
   ONLY for the operator's explicit words; if forced, the server writes the FORCED
   audit row itself.
6. `gate_run()`, `export_html()` (the phase readiness panel should now show the exit),
   a closing `progress_update` summarizing the phase, then `package_close()` and
   commit `data/`.
