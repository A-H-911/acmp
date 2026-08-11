# Kickoff prompt — ADR-0038 role-assignment UI

Paste everything between the two `=====` lines into a fresh session.

=====================================================================================

Read handoff/RESUME-adr0038-frontend.md and orient before doing anything:
server_info, package_open("tamheed-package"), gate_run().

If package_open fails on a .lock, check the PID PROPERLY before removing it — the lock holds
a bare PID and "is it alive?" lies under PID reuse. Confirm the process does not exist (or
that its identity/StartTime don't match the lock mtime), then delete
tamheed-package/data/.lock. Do not remove it reflexively.

TASK: build the role-assignment UI — FR-157, AC-089, AC-090. The backend is merged (PR #234)
and the four guards are implemented and tested server-side; only the SPA is missing.

Read §2 and §3 of the resume before designing. Two rules the last session paid for:

1. CHECK WHETHER IT IS ALREADY BUILT. Three times a "new" thing already existed. Grep the
   domain enums, src/Acmp.Web/src/i18n/locales/en.json, and "ACMP product context"/*.dc.html
   before you design anything.
2. AN ADR CITATION IN A TEST NAME IS LOAD-BEARING AND NO GATE READS IT (see SC-004). If you
   are about to override a test whose name cites an ADR or AC, read that row first. If the
   code is right and the document is stale, record a scope-change — do not diverge silently,
   and do not build the worse thing out of deference to the document.

Constraints specific to this slice:
- It must land WHOLE — mutations without UI are unused exports and the frontend gate is
  per-file >=95%, so dead code fails CI.
- Granting Administrator or Chairman must send confirmedPrivileged. The server REFUSES
  without it, so build the confirmation as a real gate, not a cosmetic dialog.
- The server also refuses self-role-change and removing the last Administrator. Surface those
  refusals as messages; do NOT pre-hide the control and call that the rule.
- admin.kc.note still claims roles are managed in Keycloak. That is now partly false — reword.
- i18n EN + AR together: check-i18n compares KEYS only, so a missing value renders raw English
  and no gate catches it.

Definition of Done (applies even though this prompt does not restate it):
- unit + integration tests; each guard proven by FORCING its refusal, never by asserting a
  handler was called
- flip the relevant AC verdicts via audit_record with evidence (AC-089/AC-090 are currently
  Partial — AV-133/AV-134 — because the UI was missing)
- authorization enforced server-side; AuditEvents emitted and asserted as ROWS
- no hardcoded strings (EN + AR), verify RTL
- no secrets in source
- progress_update + work_bind, then gate_run() and export_html(); write the package ONLY from
  main and commit immediately (data/ is git-tracked)
- conventional commits, small and reviewable; branch -> PR -> green CI -> squash-merge
- raise an ADR row (status Proposed) for any new architecture decision and STOP for approval

Report the state and your plan back to me before you start writing, then proceed. ultrathink

=====================================================================================
