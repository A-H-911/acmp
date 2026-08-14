# Loop guard — the brake for fully-auto execution

Keep this beside loop-iteration.md when running `tamheed-package` unattended. The loop stops
the moment ANY condition below is true; scope decisions and forced transitions always
need a human.

---

Stop conditions (evaluate at the start of every iteration and before closing a slice):

1. `gate_run()` verdict degraded vs the previous iteration (a gate that passed now
   fails).
2. The same `AC-` records Not-met twice in a row — the loop is not converging on it.
3. A `scope-change` row would be required (something must be deferred, cancelled, or
   expanded) — scope is a human decision; register NOTHING and stop.
4. `readiness_check` reports a blocking failure at a slice/phase close — both outs
   are the operator's alone (`"force": true` for the whole transition, a `WVR-`
   waiver for a single named rule), so the loop halts and surfaces the failing rules
   verbatim.
5. Open `DEF-` count grew by more than 2 in one iteration — the code is fighting back.
6. Two consecutive iterations produced nothing worth a `progress_update`.
7. Any store error (locked, stale tree, refused batch) — never retry around the
   single-writer or stale-tree guards.

On stop: record the reason as a final `progress_update` (event_type "escalation" —
this is the one write that is always allowed), `package_close()`, and emit the
ITERATION block with `stop=<reason>`
plus a short verbatim report of what triggered it. The operator restarts the loop after
resolving — never restart yourself.
