---
name: an-instrument-must-report-on-itself
description: "A detector can be wrong in FOUR ways, three of which produce output and look healthy: never fires, always fires, silence you cannot read, and a predicate that degenerates on the deployment machine. Ask its firing RATE and prove it LIVE before trusting anything it says."
metadata:
  node_type: memory
  type: feedback
  modified: 2026-09-03
---

Built `WBS-27.1`'s stall watchdog for `DEF-109` **because that row's clause (2) could not fire**. Its own
trigger was then wrong four times in one day. Read this before shipping any detector, scanner, guard or
capture — `LL-054` is the pinned lesson, this is the working checklist.

## The four ways, in the order they get harder to see

| # | Fault | How it looks |
|---|---|---|
| 1 | **Can never fire** | `availableWorkers == 0` — `GetAvailableThreads` counts against the pool **MAX** (32767), so it needed 32,767 queued items. Dead code. Caught in review. |
| 2 | **Always fires** | `pending > 0` — routine on a loaded runner. Turned `main` red. |
| 3 | **Silence you cannot read** | Wrote NOTHING during a 24-min stall. An empty artefact = *ran and saw nothing* OR *never ran*. **Indistinguishable.** |
| 4 | **Degenerates on the deployment machine** | `threadCount >= minWorkers` — `min workers` tracks CPU count: **4 on CI, 24 locally**, so it collapsed to `pending > 0` **there only**. |

⛔ **(1) and (2) fail loudly. (3) and (4) do not fail at all** — (3) returns a clean plausible absence that
reads like a result, and (4) passes every local run.

## The three questions to answer BEFORE shipping

1. **CAN it fire?** Compute the extreme against the real API's semantics, not the name.
2. **How often on a NORMAL run?** If *every time*, it is a log line, not a detector.
3. **Can you tell its SILENCE from its ABSENCE?** If not, add a **positive control** — write a startup
   record unconditionally so the file's EXISTENCE proves it ran and its CONTENT carries the finding.

⭐⭐ **A CALIBRATION IN THE TEST ENVIRONMENT PROVES THE MECHANISM, NEVER THE DEPLOYMENT.** The watchdog was
10/10 green and mutation-checked twice, and might never have started on CI. `LL-013` does not reach this.

## Established practice here, in no rule register

Positive controls are already required in this project four times over and it is written down **nowhere
findable**: `DW-068`, `DW-084` (*"PROVEN BY FORCING IT … and not by a green run"*), `AV-213`, and `AV-216`
— **"CONFIRMED LIVE, not only by source reading."** Both `DW-` rows are `Done`, which is `LL-040`: a
practice worth following sitting where nothing indexes it.

## When you cannot choose a threshold honestly

Don't. **Record the quantity in a periodic heartbeat instead** — windowed maxima, not cumulative, so it is
a time series and can say WHEN. Measured healthy-run baselines from the first real artefact: `maxDrift`
**0.09 s** against a 15 s threshold (three orders of magnitude of headroom — a well-chosen trigger);
`maxPending` reached **167** on a good run, so no threshold below that is defensible from n=1.

⭐ **Control first, calibrate afterwards, from data.** Guessing a threshold from the same run that raised
the doubt is how this predicate got wrong three times.

Related: [[a-green-control-can-be-blind]], [[an-absence-needs-a-proven-instrument]], [[scan-must-prove-it-had-a-subject]].
