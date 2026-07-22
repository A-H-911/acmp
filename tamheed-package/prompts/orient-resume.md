# Orient / resume — after a session clear, compaction, or handover

Paste this to re-orient an agent on the `tamheed-package` Tamheed package before any new work.

---

Orient yourself on this project's Tamheed package before doing anything else:

1. `server_info` — confirm the server version and the resolved package root.
2. `package_open("tamheed-package")` — take the single-writer lock.
3. `gate_run()` — note the verdict, any failing gate, and any G-TRACE warning.
4. Recent state: `entity_query("progress-entry", limit=10)` and
   `entity_query("audit-verdict", limit=10)` — what was the last recorded activity?
5. **Cross-check git against the package** (the package is the state; git is the
   evidence): run `git log --oneline -15` and compare against `work_bind` records
   (`entity_query("progress-entry")` notes and `last_referenced` stamps). List any
   commits that look like package-relevant work but have no recorded binding —
   flag them to the operator; do NOT invent verdicts for them.
6. Identify the active slice/phase: `entity_query("slice")`, `entity_query("phase")`,
   and the roadmap order — state which slice you believe is in progress and why.
7. Report back in five lines: package state, gate verdict, last recorded activity,
   unrecorded-work findings, and the slice you propose to resume. STOP for
   confirmation before writing anything.
