# Orient / resume — after a session clear, compaction, or handover

Paste this to re-orient an agent on the `tamheed-package` Tamheed package before any new work.

---

Orient yourself on this project's Tamheed package before doing anything else:

1. `server_info` — confirm the server version and the resolved package root.
2. `package_open("tamheed-package")` — take the single-writer lock.
3. `gate_run()` — note the verdict, any failing gate, and any G-TRACE warning.
4. The lessons: `entity_query("lesson", status="Approved")` — confirmed lessons
   bind this session too (a large register pages: pass the result's `next_after`
   back as `after_id`; never read `data/*.jsonl` to get around a payload cap).
   Recent state: `entity_query("progress-entry", limit=10)` and
   `entity_query("audit-verdict", limit=10)` — what was the last recorded activity?
5. **Cross-check git against the package** (the package is the state; git is the
   evidence): run `git log --oneline -15` and match each commit's short AND full sha
   against the recorded `work_bind` refs mechanically (`entity_query("progress-entry",
   search="<sha>")` and `last_referenced` stamps) — never judge from commit messages,
   which cite ids whether or not anything was bound. For every unreferenced commit
   run `git show --name-only` and bucket by path: commits touching ONLY the package
   folder are self-referentially unbindable (a package-write commit cannot cite its
   own sha — expected clean); docs/memory-only commits are out of scope; anything
   under source or tests is the only real candidate. Flag that last bucket alone,
   and report the discriminator with the result ("none — every unreferenced commit
   is a package write" is a check; a bare "no findings" is indistinguishable from
   not having looked). Do NOT invent verdicts for them.
6. Identify the active slice/phase: `entity_query("slice")`, `entity_query("phase")`,
   and the roadmap order — state which slice you believe is in progress and why.
7. Report back in five lines: package state, gate verdict, last recorded activity,
   unrecorded-work findings, and the slice you propose to resume. STOP for
   confirmation before writing anything. (Resuming work on an approved slice →
   slice-kickoff.md; a cold agent that has never seen this package →
   package-onboarding.md.)
