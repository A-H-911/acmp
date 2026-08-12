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

STATE: phases P1–P19 are COMPLETE. PRODUCTION AND UAT ARE BOTH LIVE ON main 65e45d4 WITH
KEYCLOAK_ADMIN_ENABLED=true — invite, role assignment and the guest-presenter invite are reachable in
production for the first time, and the ADR-0038 write path has now been exercised against a real
Keycloak (AV-145/146/147). 81 Met / 12 Partial / 0 Pending over 93 ACs; 136 evidenced verdicts;
gates 7/7. There is NO new slice to start — §4 of the resume is the whole remaining list, in order.

READ §2 OF THE RESUME BEFORE WRITING CODE. Eight rules this project has already paid for. These four
each changed an answer in the last session:

1. READ THE IMPLEMENTATION BEFORE CALLING SOMETHING A DEFECT. Nine instances, none caught by a gate.
   Last session it killed a suspected defect in three minutes: the live probe showed an invited
   member resolves to role Guest, which looked like the guest-expiry sweep would disable invitees —
   until ExpireGuestAccess.cs:59 showed it filters on a non-null expiry. READ THE PREDICATE, NOT THE
   DOC COMMENT THAT DESCRIBES IT.
2. VERIFY THE DEPLOYED STATE, NOT THE FILE THAT DESCRIBES IT. This is the rule that paid best. The
   handoff said enabling in-app user management was "one variable"; production turned out to be 56
   commits behind AND its deployed reconcile.sh had no ensure_admin_client at all, so the flag would
   have booted a healthy host authenticating as a client that did not exist. ⚠ And check your probe:
   my own first probe hit /api/session, which is the MapGroup PREFIX and 404s on new code too. It was
   right for the wrong reason. The valid form was /api/session/me.
3. A MEASUREMENT THAT INDICTS KNOWN-GOOD CODE IS MEASURING ITSELF. `grep -c $'\r'` lost its quoting,
   degraded to `grep -c ''`, and reported CRLF for every .sh in the repo — including one CI runs
   green daily. Use `tr -cd '\r' | wc -c`. Also: do not accept ONE tool's negative as proof of
   absence — the Grep tool returned "No files found" for a string that is in the tree.
4. AN ADR/AC CITATION IN A TEST NAME OR InlineData IS LOAD-BEARING, AND NO GATE READS IT. If you are
   about to change such a test, read the row first. Supersede narrowly and record it.

BEFORE DESIGNING ANY CROSS-MODULE SEAM, READ ADR-0021. It already fixes the pattern — a primitive
port in Acmp.Shared.Contracts, implemented in the OWNING module's Infrastructure, unauthorized at the
port because the calling action is separately authorized, two transactions accepted. It also forbids
sending another module's MediatR command. IGuestProvisioner, IGuestWindowWriter and
IPrincipalRevalidator are the worked examples.

TASK: work §4 of the resume in order.
- Item 1 is OQ-076 and it is an OPERATOR DECISION, not code: AC-004 asks for an idle timeout, but
  automaticSilentRenew means an open tab is never idle. Put the three options to me and wait.
- Item 2 is the real work: eleven Partials that are nearly all Partial for the SAME reason — proven
  by unit/handler tests with no live leg, because Acmp.Api.Tests authenticates with a synthetic
  TestAuthHandler. I have ALREADY AGREED THE APPROACH: a CI E2E leg now (e2e.yml runs the full
  seven-service stack with a real Keycloak on every PR), re-evidenced on UAT later. Treat it as ONE
  campaign in tranches, not eleven tasks, and tell me the tranche boundaries before you start.

Definition of Done (applies even though this prompt does not restate it):
- unit + integration tests; each guard proven by FORCING its refusal, never by asserting a handler
  was called — and check the test FAILS without the change, or it is measuring nothing
- flip AC verdicts via audit_record with evidence — an evidenced verdict beats a narrated one, and
  say plainly when something is ANALYSIS rather than a measurement
- authorization enforced server-side; AuditEvents emitted and asserted as ROWS
- no hardcoded strings (EN + AR together — check-i18n compares KEYS only); verify RTL in a browser
- no secrets in source, and never print a live credential into a session log — assert its SHAPE
- progress_update + work_bind, then gate_run() and export_html(); write the package ONLY from main
  and commit immediately (tamheed-package/data is git-tracked)
- conventional commits, small and reviewable; branch -> PR -> green CI -> squash-merge
- register every finding that needs investigation or a decision as a Tamheed row AS YOU GO, not at
  the end — including findings against your OWN work, and including CORRECTIONS to evidence you
  yourself recorded (OQ-075 carries one)

Report the state and your plan back to me before you start writing, then proceed. ultrathink

=====================================================================================
