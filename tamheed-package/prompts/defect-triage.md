# Defect triage — register FIRST, then fix

Paste this when a bug is reported or discovered during execution of `tamheed-package`.

---

Triage a defect against the `tamheed-package` Tamheed package — the registration comes
BEFORE the fix, so the record survives even if the session dies mid-repair:

1. `package_open("tamheed-package")` if not already open.
2. Reproduce the symptom as a minimal failing test — no fix yet.
3. Register it: `entity_upsert([{"type": "defect", "id": "DEF-<next>", "title":
   "<symptom>", "severity": "critical|high|medium|low", "lifecycle_status": "Open",
   "found_in": "<PH-x or SL-x>"}])` — full row; check the next free id with
   `entity_query("defect")`. Severity is honest impact: critical/high BLOCK
   readiness, medium/low only surface as the defects-minor advisory — never pick a
   severity to pass a gate.
4. Identify what it touches: the failing `FR-`/`INV-`/`AC-` via `trace_query` — if an
   invariant is at stake, say so loudly.
5. Fix to green. Scope stays minimal; if the real fix needs out-of-scope work, that
   remainder becomes a `deferred-work` row with an activation trigger, not silent
   extra scope.
6. Close the loop: `audit_record` any affected `AC-` with the test as evidence plus
   `verified_by`, `verification_method`, and `against_commit` (the fix commit);
   `work_bind` the fix commit to the `DEF-` and the `AC-`; flip the `DEF-` status
   (full-row upsert); `progress_update` the whole event (event_type "work-done",
   subject_id the `DEF-` id, actor "agent:<session>").
7. `gate_run()` — report the verdict delta.
