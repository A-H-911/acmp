# Kickoff prompt — the durable one

Paste everything between the two `=====` lines into a fresh session. This file is durably named:
when the work changes, edit it — do not create a new `prm-*.md`.

=====================================================================================

Read handoff/RESUME.md end to end before doing anything, then orient:
server_info(), package_open("tamheed-package"), gate_run().

If package_open fails on a .lock, check the PID PROPERLY before removing it — the lock holds a bare
PID and "is it alive?" LIES under PID reuse. Confirm the process does not exist, or that its identity
and StartTime do not match the lock's mtime, then delete tamheed-package/data/.lock. Never remove it
reflexively.

STATE: phases P1–P19 are COMPLETE and main is green. ⚠ DO NOT TRUST A TALLY WRITTEN HERE — a
hard-coded acceptance count goes stale on the first new verdict, and this block held one that was
already wrong. Read the live numbers instead: gate_run() gives the gate verdict and the
audit_evidence split, readiness_check("package") gives the blocking lists, and review.html#execution
renders both. PRODUCTION IS LIVE with in-app user management ENABLED — but it is UNUSED: zero topics, zero streams, one of 26 members has ever logged
in. UAT is stopped. There is NO new slice to start; §4 of the resume is the whole remaining list, in
priority order, and every decision in it has already been taken and recorded.

READ §2 AND §3 OF THE RESUME BEFORE WRITING CODE. §3 is the finding that matters most and it is why
this codebase needs a specific kind of suspicion:

★ FOUR AGGREGATE CAPABILITIES WITH NO WIRING WERE FOUND IN ONE SESSION, none by any gate —
Topic.Reopen (no endpoint), Stream.Create (no caller), StreamScopeHandler (in DI, unit-tested four
ways, IN NO POLICY — so an authorization control that FAILS OPEN), and Topic.SetScope (no caller).
EVERY ONE PRESENTS AS IMPLEMENTED: the method exists, it is correct, its comment explains its
purpose, and two are unit-tested and passing. The compiler does not care that a public method has no
caller; the unit test calls it directly, which is exactly why it passes; coverage says it IS covered.
SO: when you are told something is implemented, find the CALLER, not the definition. Recorded as
DW-026, whose cheapest first step is asserting every IAuthorizationRequirement appears in at least
one registered policy.

Four more rules that each changed an answer recently:
1. READ THE IMPLEMENTATION BEFORE CALLING SOMETHING A DEFECT — eleven instances. ⚠ IT APPLIES TO
   REGISTER ROWS TOO: I repeated DEF-045's stale "cause 3 NOT FIXED" to the operator without reading
   the two specs it described, and both were already fixed. A row feels pre-checked. It is not.
2. VERIFY THE DEPLOYED STATE, NOT THE FILE DESCRIBING IT. Prod was 56 commits behind while the
   handoff called enabling a flag "one variable". ⚠ And check your probe can tell the two states
   apart: my own /api/session probe hit the MapGroup PREFIX, which 404s on new code too — right
   conclusion, invalid evidence.
3. A MEASUREMENT THAT INDICTS KNOWN-GOOD CODE IS MEASURING ITSELF, and A GREEN EXIT CODE CAN COME
   FROM A BUILD THAT CHECKED NOTHING. `grep -c $'\r'` lost its quoting and reported CRLF for every
   .sh in the repo; `tsc -b` reported exit 0 AND zero e2e files until --force. Count what the check
   looked at.
4. THE TEST MUST FAIL WITHOUT THE CHANGE. Every guard in the last session was mutation-checked —
   reverting App.tsx failed exactly the five denied roles, and reversing two call ORDERS failed one
   test each. A case that passes with and without the code under test is measuring nothing.

BEFORE DESIGNING ANY CROSS-MODULE SEAM, READ ADR-0021: a primitive port in Acmp.Shared.Contracts,
implemented in the OWNING module's Infrastructure, unauthorized at the port because the calling
action is separately authorized, two transactions accepted; and it forbids sending another module's
MediatR command. ⚠ This is live in §4 item 1 step 6: the OrgWide fact must reach authorization as a
primitive bool on IStreamScopedResource, NOT as the TopicScope enum, because the contract lives in
Shared.Contracts and the enum in Topics.Domain.

TASK: work §4 of the resume in order.

- ITEM 1 is the big one: DEF-057 + DEF-058, an EIGHT-STEP SLICE whose design is already APPROVED
  (ADR-0042; reasoning in PE-293 and DEC-042). Nothing is built. ⚠ THE ORDER IS LOAD-BEARING and
  STEP 5 IS THE OUTAGE: all 26 existing members were seeded straight into Keycloak, so the new
  invite-time stream field does not cover them — wiring the check before backfilling locks out the
  whole committee. ⚠ Step 2 (topics pick from the taxonomy instead of free text) is cheapest RIGHT
  NOW because production has zero topics, and that stops being true the first day anyone uses it.
  Expect multiple PRs; do not try to land it as one change.
- ITEMS 2–5 are smaller and independent: DEF-056's audit handler (which turns a deliberate
  test.fail() in role-matrix.spec.ts red — delete that line and flip AC-006), the e2e flag for
  AC-011, AC-003's cheap live test, and AC-041's instrument choice.
- ITEM 6 is an operator call: deploying the idle sign-out, which is the ONLY product change published
  but not deployed.

Definition of Done (applies even though this prompt does not restate it):
- unit + integration tests; each guard proven by FORCING its refusal, and verified to FAIL without
  the change — never by asserting a handler was called
- flip AC verdicts via audit_record with evidence, and say plainly when something is ANALYSIS rather
  than a measurement
- authorization enforced server-side; AuditEvents emitted and asserted as ROWS
- no hardcoded strings (EN + AR together — check-i18n compares KEYS only); verify RTL in a browser
- no secrets in source, and never print a live credential — assert its SHAPE
- progress_update + work_bind, then gate_run() and export_html(); write the package ONLY from main
  and commit immediately (tamheed-package/data is git-tracked)
- conventional commits, small and reviewable; branch -> PR -> green CI -> squash-merge
- register every finding needing investigation or a decision as a Tamheed row AS YOU GO — including
  findings against your OWN work, and including CORRECTIONS to evidence you yourself recorded

Report the state and your plan back to me before you start writing, then proceed. ultrathink

=====================================================================================
