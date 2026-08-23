# Phase close — the guarded exit, done deliberately

Paste this to close a phase of `tamheed-package` (semi-auto; distinct from slice-review —
this is the phase-level exit with the guarded transition at the end).

---

Close phase `<PH-x>` of the `tamheed-package` Tamheed package:

1. `package_open("tamheed-package")` if not already open.
2. `readiness_check("phase", "<PH-x>")` — resolve every blocking failure: ACs of the
   phase's slices not latest-Met, slices/work items still open (Review counts as
   open — done-claimed is not verified), open critical/high defects (medium/low only
   surface as the defects-minor advisory; never downgrade severity to pass). Each
   resolution is recorded (verdicts with evidence, statuses via full-row upserts,
   deferrals/scope via `DW-`/`SC-` rows, a named-rule exception via a `WVR-`
   waiver — operator-approved only, reported as "waived") — never narrated away.
3. **Expired waivers** touching this phase (`expired_waivers` in the readiness
   report): resolve the underlying item, or get fresh operator words for a new
   `WVR-` (or a full-row upsert with a new `expires`) — never carry one silently
   across a phase boundary.
4. Milestones of the phase (`entity_query("milestone")`): roadmap labels only — no
   lifecycle, never a gate. Report which read as reached; an unreached one is either
   a conversation with the operator or an explicit `scope-change` — never a status
   flip.
5. `human_required` gates for the phase: read each `GATE-` definition to the operator,
   get the explicit decision, upsert the gate row's `outcome`
   (Go/Hold/Redirect/Kill), and record it as a `progress_update` (event_type
   "gate-decision", subject_id the `GATE-` id).
6. The transition: upsert the phase full-row with `lifecycle_status: "Implemented"`.
   If the guard refuses, the blockers are real — resolve them, or for one stubborn
   item ask the operator for a `WVR-` waiver. `"force": true` overrides the whole
   transition and exists ONLY for the operator's explicit words; if forced, the
   server writes the FORCED audit row itself.
7. `gate_run()`, `export_html()` (the phase readiness panel should now show the exit),
   a closing `progress_update` summarizing the phase (event_type "transition",
   subject_id "<PH-x>", actor "agent:<session>"), then `package_close()` and
   commit `data/`.
