# Kickoff prompt — the durable one

Paste everything between the two `=====` lines into a fresh session. This file is durably named:
when the work changes, edit it — do not create a new `prm-*.md`.

=====================================================================================

Read handoff/RESUME.md end to end before doing anything, then orient:
server_info(), package_open("tamheed-package"), gate_run().

If package_open fails on a .lock, check the PID PROPERLY before removing it — the lock holds a bare
PID and "is it alive?" LIES under PID reuse. Confirm the process does not exist, or that its identity
and StartTime do not match the lock's mtime, then delete tamheed-package/data/.lock. It went stale
twice in one session. Never remove it reflexively.

STATE: phases P1–P19 are COMPLETE. FR-159 / AC-092 shipped (guest presenters, /session), and all four
post-FR-159 items were built (DEC-041). 80 Met / 12 Partial / 1 Pending over 93 ACs; gates 7/7. There
is NO new slice to start — §4 of the resume is the whole remaining list, in priority order.

READ §2 OF THE RESUME BEFORE WRITING CODE. Seven rules this project has already paid for. These four
bite hardest, and each one changed an answer in the last session:

1. READ THE IMPLEMENTATION BEFORE CALLING SOMETHING A DEFECT. Seven instances, none caught by a gate.
   Last session it made a whole task DISAPPEAR: DW-025 was written on the premise that rescheduling a
   meeting strands a guest's access window, and ACMP HAS NO RESCHEDULE — three checks, two minutes.
   It would otherwise have been "implemented".
2. AN ADR/AC CITATION IN A TEST NAME OR InlineData IS LOAD-BEARING, AND NO GATE READS IT. SC-007
   exists because an [InlineData("Guest")] citing AC-059 caught a narrowing nothing else could see.
   If you are about to change such a test, read the row first. Supersede narrowly and record it.
3. A GREEN SUITE IS NOT A LOOK. Render new or changed screens in a real browser, in BOTH directions.
   ⚠ The throwaway harness must import ONLY the stylesheets the real route imports — last session a
   harness that imported one extra stylesheet hid a component that would have shipped unstyled.
4. VERIFY THE DEPLOYED STATE, NOT THE FILE THAT DESCRIBES IT. DEF-050 said "exposure is probably nil",
   inferred from .env.example; reading SSM showed the truth and that the defect was narrower than
   recorded. "Detects but does not tell" is this project's most repeated bug class.

BEFORE DESIGNING ANY CROSS-MODULE SEAM, READ ADR-0021. It already fixes the pattern — a primitive
port in Acmp.Shared.Contracts, implemented in the OWNING module's Infrastructure, unauthorized at the
port because the calling action is separately authorized, two transactions accepted. It also forbids
sending another module's MediatR command. Last session this turned an open architecture question into
a lookup. ADR-0040 and its ports (IGuestProvisioner, IGuestWindowWriter) are the worked examples.

TASK: work §4 of the resume in order. Item 1 is an OPERATOR action and is the highest-value thing in
the list — if you cannot do it, say so and move to item 2. Items 2 and 3 (DEF-053, DEF-054) are small,
have a known shape, and are the honest place to start writing code. Items 4–6 are an evidence
campaign: AC-093, AC-004 and eleven Partials that are nearly all Partial for the SAME reason — proven
by unit/handler tests with no live leg. Treat that as one campaign, not eleven tasks, and agree the
approach with me before writing eleven tests.

Definition of Done (applies even though this prompt does not restate it):
- unit + integration tests; each guard proven by FORCING its refusal, never by asserting a handler
  was called
- flip AC verdicts via audit_record with evidence — an evidenced verdict beats a narrated one
- authorization enforced server-side; AuditEvents emitted and asserted as ROWS
- no hardcoded strings (EN + AR together — check-i18n compares KEYS only, so a missing value renders
  raw English and no gate catches it); verify RTL in a browser
- no secrets in source
- progress_update + work_bind, then gate_run() and export_html(); write the package ONLY from main
  and commit immediately (tamheed-package/data is git-tracked)
- conventional commits, small and reviewable; branch -> PR -> green CI -> squash-merge
- register every finding that needs investigation or a decision as a Tamheed row AS YOU GO, not at
  the end — including findings against your OWN work (DEF-053 is one)

Report the state and your plan back to me before you start writing, then proceed. ultrathink

=====================================================================================
