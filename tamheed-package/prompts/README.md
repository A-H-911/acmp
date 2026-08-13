# How to use this folder — the `tamheed-package` prompt guide (tamheed v3.2.1)

This folder is the **single prompt surface** for the `tamheed-package` Tamheed package. Every
file is a paste-ready prompt for a Claude Code session. Two kinds live here:

- **Stock scenarios** (this file and the 14 named below) — shipped by tamheed, refreshed
  on upgrade; if you hand-edit one, later refreshes report it `diverged` and never
  overwrite without `force`.
- **Your project prompts** — any other filename. Operator-owned; tamheed never touches
  them. Name them by purpose, kebab-case (`kickoff.md`, `phase3-resume.md`). Files named
  `prm-NNN-<kind>.md` with a `<!-- converted … -->` header are legacy prompts converted
  from the old database — audit names, not a pattern to copy; review each (keep the
  project-specific parts, drop what the stock library now covers) and remove the header
  line when done.

## Which prompt, when

| Situation | Paste |
|---|---|
| A brand-new agent has never seen this package | `package-onboarding.md` |
| Resuming after a session clear / compaction | `orient-resume.md` |
| Starting the next slice of work | `slice-kickoff.md` |
| Work is done, package not yet updated | `progress-sync.md` |
| A bug was found or reported | `defect-triage.md` |
| Work happened without recording (any session) | `drift-register.md` |
| A slice is believed complete | `slice-review.md` |
| A phase is believed complete | `phase-close.md` |
| Closing out a release | `release-close-out.md` |
| Deferred-work triggers may have fired | `replan-deferred.md` |
| Read-only trust audit of the package | `integrity-check.md` |
| Refresh + read the human report | `generate-report.md` |
| Unattended execution — the repeated prompt | `loop-iteration.md` |
| Unattended execution — the brake (read FIRST) | `loop-guard.md` |
| Something project-specific | any other `.md` here — project prompts are operator-authored, purpose-named; read the folder |

## Semi-auto style (you drive)

The typical loop: `orient-resume` → `slice-kickoff` → the agent works → `progress-sync`
→ `slice-review` → next slice (phase exits via `phase-close`, releases via
`release-close-out`). Every **STOP for approval** in these prompts is real — the agent
waits for your words. Two things are always yours alone: **scope changes** (the
`scope-change` row needs your decision) and **`force`** (overriding a blocked
`Implemented` transition past failing readiness rules).

## Fully-auto style (unattended)

Pair `loop-iteration.md` (the prompt a loop repeats) with `loop-guard.md` (the stop
conditions — read it before starting any loop). Drive it either way:

- an **in-session loop** (e.g. Claude Code `/loop`) re-pasting loop-iteration;
- an **external harness** starting a fresh session per iteration and parsing the final
  `ITERATION: wbs=… slice=… acs_moved=… gate=… ready=… stop=…` line to decide
  continue/stop.

The loop halts itself — never restarts itself — on any guard condition: a degraded gate,
non-convergence, a needed scope change, a blocking readiness failure at a close, a
defect spike, empty iterations, or any store error. You resolve, you restart.

## One session at a time

The package has a **single-writer lock** (`data/.lock`). Two sessions pasting prompts
concurrently will collide: the second `package_open` refuses, naming the holder (pid,
host, taken_at). After a crash or a plugin reload the refusal may be a **stale lock** —
verify before clearing, with both discriminators: the named pid must belong to a live
process that plausibly IS an agent session, **and** that process must have started
*before* the lock's `taken_at` (a process younger than the lock cannot hold it). Only
when both say stale, delete `data/.lock`. Never auto-clear; when unsure, ask the other
session's operator.

## The standing rules

The **Recording obligations** table in this project's `CLAUDE.md` note binds every
session, prompted or not: defects, deferred work, and scope changes are registered
BEFORE moving on; verdicts carry evidence; `readiness_check` runs before anything is
declared done. The package is the record — when code and package disagree, fix the code
or record the change; never let them drift.
