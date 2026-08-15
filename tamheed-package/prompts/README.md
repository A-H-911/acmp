# How to use this folder — the `tamheed-package` prompt guide (tamheed v4.4.0)

This folder is the **single prompt surface** for the `tamheed-package` Tamheed package. Every
file is a paste-ready prompt for a Claude Code session. Two kinds live here:

- **Stock scenarios** (this file and the 16 named below) — shipped by tamheed, refreshed
  on upgrade; if you hand-edit one, later refreshes report it `diverged` and never
  overwrite without `force`. Since v4.1 the tool tells the two divergence kinds
  apart against its shipped stock history: a file byte-equal to an OLDER release's
  stock is `stale-stock` (you never customised it) — `handoff_emit` with
  `refresh_stock=true` updates ONLY those; a `customized` file is never touched by
  refresh (per-file acceptance stays delete + re-emit; `force` still overwrites
  ALL diverged). ⚠ Customizing a stock prompt therefore opts it out of every future
  refresh, silently and permanently — the emission warning names how far the stock
  has since moved (`stock_last_changed`); to carry a release's improvements into a
  customized copy, hand-merge: the bundled `stock-history.json` holds every
  release's body, so extract the current one and diff against your copy.
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
| Readiness advisories piling up (the amber list) | `register-liveness.md` — run it on a cadence, not only at close |
| Distilling confirmed lessons into a reusable skill | `skill-promote.md` — the operator-interview ceremony (project or user level) |
| Read-only trust audit of the package | `integrity-check.md` |
| Refresh + read the human report | `generate-report.md` |
| Unattended execution — the repeated prompt | `loop-iteration.md` |
| Unattended execution — the brake (read FIRST) | `loop-guard.md` |
| Something project-specific | any other `.md` here — project prompts are operator-authored, purpose-named; read the folder |

## Semi-auto style (you drive)

The typical loop: `orient-resume` → `slice-kickoff` → the agent works → `progress-sync`
→ `slice-review` → next slice (phase exits via `phase-close`, releases via
`release-close-out`). Every **STOP for approval** in these prompts is real — the agent
waits for your words. Three things are always yours alone: **scope changes** (the
`SC-` row needs your approval before its changes are applied and it is set Merged),
**waivers** (a `WVR-` row satisfying one named readiness rule for one named entity —
the agent may ask for one, never author one), and **`force`** (overriding a whole
blocked `Implemented` transition past failing readiness rules).

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
verify before clearing, with two discriminators: delete `data/.lock` when EITHER
proves staleness — an **identity failure** (the named pid is not plausibly an agent
session: pid reuse) or an **ordering failure** (the process started *after* the lock's
`taken_at` — a process younger than the lock cannot hold it). Keep the lock only when
BOTH checks pass. Never auto-clear; when unsure, ask the other session's operator.

## The standing rules

The **Recording obligations** table in this project's `CLAUDE.md` note binds every
session, prompted or not: defects, deferred work, and scope changes are registered
BEFORE moving on; verdicts carry evidence and its chain (`verified_by`,
`verification_method`, `against_commit`); done-claimed is `Review`, verified is
`Implemented`; `readiness_check` runs before anything is declared done. Repairing a damaged
field? Repair from `data/*.jsonl` (or the backup), never from `entity_query` output — a
full-row upsert rebuilt from a truncated query round-trip re-commits the damage. PASTE a
generated repair payload, never re-type it (the hand is the untrusted transport), and end
every multi-row repair with an independent verifier: re-read the JSONL and re-derive each
expected value from its source before calling the repair done. When execution teaches
something durable, record a `lesson` row (`LL-`, born Proposed) — only lessons the
OPERATOR approves bind future sessions (rendered into the CLAUDE.md note, pinned first);
the agent never approves its own lesson — the store REFUSES an approving or promoting
upsert without `"operator_confirm": true`, your words, in every mode; Approved lessons
with a shared theme can be distilled into a SKILL (`skill-promote.md`) that Claude Code
loads natively — promoted lessons graduate out of the note, the skill file carries them. The package is the record — when code and package disagree, fix the code
or record the change; never let them drift.
