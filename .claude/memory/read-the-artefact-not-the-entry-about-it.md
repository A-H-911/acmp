---
name: read-the-artefact-not-the-entry-about-it
description: "DEF-121's crash artefact was called insufficient twice while the answer sat in two files it KEPT — plus the strict:true branch-protection trap: any push to main leaves every open PR unmergeable."
metadata: 
  node_type: memory
  type: project
  originSessionId: ce1b735d-ccfc-423e-af9e-6b424237ec24
  modified: 2026-09-02T14:41:02.293Z
---

# Read the artefact, not the entry about it (2026-09-02, `DEC-116`)

## The finding — the memory-pressure hypothesis for `DEF-121` is REFUTED

Two sessions concluded the evidence was locked inside the **1253 MB `core.sqlservr.21.gdmp`** the 64 MB
ceiling dropped. **It was in two files the capture KEPT and uploaded**, and it settles the question:

| source (both `kept`) | measured at the crash |
|---|---|
| `thread_information.log` (139,922 B) | `VmRSS: 219588 kB` with **`VmHWM` EQUAL to it** (never used more, ever), `VmSwap: 0`, `State: T (stopped)`, across **31** `/proc/<tid>/status` blocks |
| `info.log` (986,112 B) | `/proc/meminfo`: `MemTotal 16373452`, **`MemAvailable 11231204`**, `SwapTotal 3145724` / **`SwapFree 3145680`** (44 kB used); cgroup `memory.peak 1537601536`, `memory.max` **unset** |

**Nothing was starved at either level.** `OOM`/`Killed` scan zero, control `sqlservr` at 162 hits.

⛔ **This satisfies NO clause of `DEF-121`'s end condition.** Clause (2) wants a captured log that
**IDENTIFIES A CAUSE**; an elimination is not an identification, and `AppLoader: Failed to load LSA
0xc0070102` / errno 2 ENOENT is as unexplained as ever (`DEC-116` d1). ⚠ Covers **occurrence 2 only**;
`dmesg.tail.txt` and both `journalctl` files are **0 bytes**, so no kernel-side channel exists.
⭐ It does buy one thing: `DEF-121` and `DEF-109` are now separate families **on evidence**, not signature.

## Why both sessions got it wrong — `LL-052` (Approved, **pinned**)

- `PE-784` described `info.log` from its **`file(1)` type** — "a binary-framed blob".
- `PE-787` read the **first block** of a 3,162-line file (`io`, then `sched`), found scheduler stats, and
  generalised. The file is a **repeating `io`/`sched`/`status` triple per thread**; every third block is
  the one that mattered. ⚠ `PE-787` was itself a correction of `PE-784`, and cited `LL-041`/`LL-043`
  while committing the same class of error.

⭐⭐ **A file's NAME and its FIRST SCREEN both describe its FORMAT, never its CONTENT. A manifest saying
`kept` is a statement about the copy succeeding — quote it only for what was `DROPPED`.**

⚠ Also corrected: `DEF-121`'s "no `Last errno` line appears at all" is **false** — the pair is in
`crash.txt` AND at lines 396–397 of the backend job log. One of its two "unreconciled differences"
never existed.

## Two instrument failures worth more than the results

1. ⛔⛔ **A CALIBRATED VERIFIER POINTED AT A MOVED FIXTURE PASSES VACUOUSLY** (`LL-032`'s shape). My
   pre-image/prefix check for three 5–20 KB row appends was re-run mid-batch, overwrote two pre-image
   files with **post-write** text, and compared those rows to themselves: `byte-identical-prefix=True`,
   **`grew=+0`** on rows just grown by 2,400 characters. **The delta was the only tell** — a PASS/FAIL
   verifier would have said PASS. ⭐ Fix: re-verify against `git show <sha>:…jsonl`, a baseline the write
   cannot move. **A calibration proves the check discriminates; it says nothing about whether the
   baseline is still the baseline.**
2. ⚠ `tsc -b … | tail` reported **exit 0 while printing two errors** — `$?` is the last command in a
   pipe. Read the OUTPUT, or capture the code before piping.

## ⛔⛔ `strict: true` — the branch-protection trap I walked into

`required_status_checks.strict` is **TRUE** on `main`. So **every push to `main` makes every open PR
`BEHIND` and unmergeable, whatever the push touched.** A package commit is path-ignored by both
workflows — *no CI run fires* — and that reads far too easily as *no effect on open PRs*. **They are
unrelated mechanisms.** I greened `#340`, pushed a package commit recording the green, and made it
unmergeable; the fix costs a full CI cycle and re-exposes the merge to `DEF-121`.

⭐ **Order: package writes to `main` FIRST, then rebase the branch onto that, then push nothing to `main`
until the PR lands.** `enforce_admins` is `false` so an admin merge would work — `DEC-115` d1 declined
one over a *red* check, which is not squarely a stale-but-green branch, and that is exactly why the
difference is not the agent's to split.

## The pixel assertion (`DEC-116` d2 — an OVERRIDE that reverses `DEC-114` d3)

`e2e/def128-calendar-columns.spec.ts`. ⭐⭐ **Its CONTROL is worth more than its result:** with the
pre-fix `repeat(7, 1fr)` the spread is **335.61 px** / **276.17 px** and the assertion fails; with
`minmax(0, 1fr)` it is 0.02 px and passes — **but `.cal-weekday` reads 0.02 px in BOTH**, so the header
grid is clean whether the defect is present or not. Measuring the header alone (the instrument that
first reported `DEF-128` clean) can never discriminate (`LL-043`). ⚠ The fault is **conditional on a
long title**, so each test seeds one and asserts the chip is visible before measuring — otherwise it
passes vacuously and would keep passing if the CSS were reverted (`DEF-126`'s lesson, applied ahead of
time). ⚠ **Check the e2e COUNT, not the colour:** 99 → 101, exactly +2, because `msedge` carries
`testMatch: /rtl-a11y\.spec\.ts/` and the new spec runs in `chromium` alone.

Related: [[def109-the-host-is-the-unit-that-leaks]], [[verify-mechanically-not-carefully]],
[[read-before-calling-it-a-defect]], [[an-absence-needs-a-proven-instrument]].
