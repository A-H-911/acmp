# Picking up deferred work — ACMP cautions

Read alongside the stock `replan-deferred.md`, which covers the generic flow. This file holds only
what is true of THIS package and would be wrong to guess.

## The `DW-` identifiers map to the historic `D-` numbers by identity

`DW-015` is `D-15`. This has held since the parser-upgrade re-population of 2026-07-23.

⚠ Commit messages, ADRs and progress prose written BEFORE that date may still use the drifted
2.3.0-era mapping, where the numbers do not line up. If you are reading an older reference, resolve
the id against the register rather than assuming the number carries over. The repair history is
`DOC-069` (Repair & re-population record); what was damaged, what was fixed and what remains open is
the `defect` family.

## `DW-011` / `SL-014` (Tarseem diagrams) is deferred INDEFINITELY

`DEC-028` deferred it, and its activation trigger is **an explicit operator instruction and nothing
else**. No amount of "the dependency is now available" or "there is spare capacity" fires it.

⚠ It correctly has zero progress entries because it was never built — that absence is not a gap to
fill. Do not start it, and do not read its emptiness as an oversight.

## Before starting any other row

Each `deferred-work` row carries an activation trigger in prose. A human judges whether it fired —
`readiness_check` says so explicitly. Do not start a row whose trigger has not fired, and if you
believe one has, say which words in the trigger you are matching.
