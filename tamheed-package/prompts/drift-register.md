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
     `scope-change` row (`SC-`) FIRST, `decision_ref` naming the deciding
     `DEC-`/`ADR-` — upsert the decision row (status Proposed) if none exists, and
     STOP for approval before treating it as settled;
   - work simply unrecorded → `progress_update` per unit + `work_bind` per orphan
     commit + `audit_record` for any criterion actually verified (with evidence).
4. `gate_run()` and `readiness_check("package")` — report what was registered and
   what the package now says is blocking.
5. `package_close()` and remind the operator to commit `data/`.
