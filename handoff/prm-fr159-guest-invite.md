# Kickoff prompt — FR-159 guest invite + /session

Paste everything between the two `=====` lines into a fresh session.

=====================================================================================

Read handoff/RESUME.md and orient before doing anything:
server_info, package_open("tamheed-package"), gate_run().

If package_open fails on a .lock, check the PID PROPERLY before removing it — the lock holds a bare
PID and "is it alive?" lies under PID reuse. Confirm the process does not exist, or that its identity
and StartTime don't match the lock's mtime, then delete tamheed-package/data/.lock. Do not remove it
reflexively.

TASK: finish FR-159 / AC-092 — the guest-invite WRITER and the /session page. The enforcement half is
already merged and tested: a member past their access window is refused per request (401
access_expired, ADR-0039) and an hourly sweep disables them in Keycloak too. AC-092 is Partial for
exactly one reason — NOTHING SETS AccessExpiresAt, and there is no page.

Read §2 and §3 of the resume before designing. Six rules this project has already paid for; these
three bite hardest here:

1. CHECK WHETHER IT IS ALREADY BUILT. AccessExpiresAt, SetAccessWindow, HasExpired, the per-request
   refusal, the sweep, IIdentityProvider.CreateUserAsync/DisableUserAsync and an invite that already
   creates at CommitteeRole.Guest ALL EXIST. Grep the domain enums, i18n/locales/en.json and
   "ACMP product context"/*.dc.html before you write anything.
2. A GREEN SUITE IS NOT A LOOK. Testing-library queries pass against completely unstyled markup —
   DEF-047 shipped a visibly broken panel with 8 tests green. Render /session in a real browser, in
   BOTH directions, before calling it done.
3. AN ADR/AC CITATION IN A TEST NAME IS LOAD-BEARING AND NO GATE READS IT (SC-004). If you are about
   to override a test whose name cites an ADR or AC, read that row first. If the code is right and
   the document is stale, record a scope change — do not diverge silently.

THE DESIGN DECISION TO SETTLE FIRST, BEFORE CODE:
The access window comes from the meeting's ScheduledEnd, which is MEETINGS-OWNED. ADR-0001 forbids
Membership reading another module's tables. Prefer a cross-module contract in Shared.Contracts (the
established pattern — ICommitteeDirectory, ITopicScheduler, IPrincipalRevalidator). That is a NEW
ARCHITECTURE DECISION: raise an ADR row (status Proposed) and STOP for my approval before writing it.
Also decide explicitly whether the window ends at ScheduledEnd or carries a grace period, and say
which — do not leave it implied.

Constraints specific to this slice:
- It must land WHOLE — the frontend gate is per-file >=95%, so a mutation with no UI is an unused
  export and fails CI.
- /session is built to "ACMP Navigation & IA.dc.html" lines 304-347 (GUEST / PRESENTER SHELL). READ
  THE .dc.html DIRECTLY WITH FILE TOOLS, not the design MCP (INV-014). DEC-037 fixes the content and
  the copy; match it.
- The banner MUST read the same AccessExpiresAt the server enforces. That is AC-092's explicit
  requirement and the reason the value is stored once. Three readers already share one boundary
  (CommitteeMember.HasExpired, exclusive) — keep it structural, not a convention.
- The route is Guest plus Chairman/Secretary for preview, ENFORCED AT THE API and not only by the
  route guard (DEC-037). navModel.ts already sets ACCESS.session = { guest: 'full' }.
- Authorization for the invite is SECRETARY (FR-159), not the Administrator-or-Secretary pair FR-156
  uses.
- i18n EN + AR together: check-i18n compares KEYS only, so a missing value renders raw English and no
  gate catches it.

Definition of Done (applies even though this prompt does not restate it):
- unit + integration tests; each guard proven by FORCING its refusal, never by asserting a handler
  was called
- flip the relevant AC verdicts via audit_record with evidence (AC-092 is Partial — AV-141)
- authorization enforced server-side; AuditEvents emitted and asserted as ROWS
- no hardcoded strings (EN + AR), verify RTL in a browser
- no secrets in source
- progress_update + work_bind, then gate_run() and export_html(); write the package ONLY from main
  and commit immediately (data/ is git-tracked)
- conventional commits, small and reviewable; branch -> PR -> green CI -> squash-merge
- register every finding that needs investigation or a decision as a Tamheed row AS YOU GO, not at
  the end

Report the state and your plan back to me before you start writing, then proceed. ultrathink

=====================================================================================
