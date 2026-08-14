# Drift register — everything the package doesn't know yet

Paste this when work happened without recording (a rushed session, a hotfix, an agent
that forgot) to bring `tamheed-package` back to truth.

---

Register every piece of drift between reality and the `tamheed-package` Tamheed package:

1. `package_open("tamheed-package")` if not already open.
2. Enumerate what the package doesn't know: `git log --oneline -20` vs the recorded
   `work_bind` refs; work done, decisions taken, problems found — list them ALL
   before writing anything.
3. Classify and register each item, in this order:
   - a bug that exists → `defect` row (`DEF-`, status Open, `found_in`);
   - needed work that is out of scope → `deferred-work` row (`DW-`) with severity and
     an activation trigger;
   - any deviation from the approved plan (scope grew, shrank, or changed shape) →
     `scope-change` row (`SC-`) FIRST (lifecycle Proposed → Approved → Merged),
     `decision_ref` naming the deciding `DEC-`/`ADR-` — upsert the decision row
     (status Proposed) if none exists — plus `scope_adds`/`scope_modifies`/
     `scope_removes` delta edges to the affected rows; STOP for approval, and only
     after it apply the row changes and set the `SC-` Merged (the
     scope-changes-merged advisory flags Approved-never-Merged);
   - work simply unrecorded → `progress_update` per unit (event_type "work-done",
     actor "agent:<session>"; a WRONG earlier entry is compensated by a new event
     with `corrects: "<PE-x>"` — journals are never edited) + `work_bind` per orphan
     commit + `audit_record` for any criterion actually verified (evidence plus
     `verified_by`/`verification_method`/`against_commit`);
   - a requirement created during execution → wire its trace edges (`derives_from` /
     `implements` / `tests`) NOW — `work_bind` stamps commits, it does NOT wire
     traceability (`gate_run`'s `requirements_unwired` advisory lists the strays);
   - genuine ambiguity about what happened or what was intended → an `OQ-` row
     (owner + due_by) and a `[NEEDS-CLARIFICATION: OQ-NNN]` marker at the exact
     ambiguous spot — never a guessed record.
4. `gate_run()` and `readiness_check("package")` — report what was registered and
   what the package now says is blocking.
5. `package_close()` and remind the operator to commit `data/`.
