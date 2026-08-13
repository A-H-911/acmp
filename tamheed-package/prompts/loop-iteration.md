# Loop iteration — one fully-auto pass, machine-parseable end

Use this as the repeated prompt of an unattended loop over `tamheed-package` (an in-session
/loop or an external harness re-pasting per iteration — both parse the ITERATION
block). Read loop-guard.md first: its stop conditions override everything here.

---

Execute ONE iteration against the `tamheed-package` Tamheed package, no pauses:

1. Orient: `server_info` → `package_open("tamheed-package")` → `gate_run()` →
   `entity_query("progress-entry", limit=10)` and `git log --oneline -10` cross-check
   (unbound package-relevant commits are drift — register them via the drift-register
   steps before new work).
2. Check the brakes: evaluate every loop-guard stop condition. Any of them true →
   record the reason as a final `progress_update`, `package_close()`, and emit the
   ITERATION block with `stop=<reason>` — do nothing else.
3. Pick work: the first open `wbs-item` (`entity_query("wbs-item")`) in phase/slice
   order. No open items → `stop=backlog-empty`.
4. Execute acceptance-criteria-first: failing test → implement → green. Bounded to
   this one work item.
5. Sync (the recording obligations, no exceptions): `progress_update` per unit;
   `audit_record` with evidence per verified `AC-`; `work_bind` the commit; discovered
   defects → `DEF-` rows; out-of-scope finds → `DW-` rows. A needed `scope-change` is
   NOT yours to make — that is a stop condition.
6. Close: if the slice's criteria all look Met, `readiness_check("slice", "<SL-x>")` —
   a blocking failure is a stop condition (force needs a human). Then `gate_run()` and
   `package_close()`.
7. End with EXACTLY this block (the loop driver parses it; one line, fixed order):

   `ITERATION: wbs=<WBS-x|none> slice=<SL-x|none> acs_moved=<n> gate=<pass|fail> ready=<true|false|n/a> stop=<none|reason>`
