---
name: p6a-meeting-ia-plan
description: Resumable plan for P6a (meeting-page IA reconcile) — design anatomy + route/file map already analysed; build not started.
metadata: 
  node_type: memory
  type: project
  originSessionId: 0403b200-aedb-4abd-a29b-7ab102d95133
---

Round-2 reconcile **P6a (Meetings IA)** — **BUILT & shipped** 2026-06-30 on branch `feat/p6a-meeting-ia` (PR opened, not self-merged). Shell + nested routes (Overview/Agenda/Attendance/Notes/Minutes/Recording), MeetingOverview/Minutes/Recording/Gate new, AgendaBuilder readOnly self-derived, DV-16 control re-added, DV-21 label→"Prepared", DV-03 confirmed. 384 FE tests green, per-file lines ≥95%, dev-stub VR (EN-light + AR-RTL-dark) done. **Next = P6b** (notifications IA: bell popover + full inbox, no preferences page v1 per RD-09 + DV confirmations). See [[coverage-and-e2e-mandate]], [[exact-design-fidelity-visual-loop]].

**Authority = Usage Map decisions** (`ACMP Usage Map.dc.html`): RD-08 → Meetings owns the detail SHELL (list·schedule·overview/lifecycle·recording·route-denied); Agenda&Meeting owns CONTENT (agenda·conduct·minutes); remove the duplicate `denied`. NV-08 → addressable sub-tabs Agenda·Attendance·Notes·Minutes·Recording; conduct = runtime composition of Attendance+Notes while inprogress, each deep-linkable. RD-09 → notifications = bell popover + full inbox only, **no preferences page in v1** (P6b). 

**Target routes** (App.tsx, nest under `meetings/:key`): index→Overview · `/agenda`→AgendaBuilder · `/attendance` & `/notes`→MeetingWorkspace (the conduct composition; both render it during inprogress, gate otherwise) · `/minutes`→P7 placeholder · `/recording`→Recording. **Tab strip = 6 deep-linkable NavLinks: Overview·Agenda·Attendance·Notes·Minutes·Recording** (design's strip is the first 5; Recording promoted from the overview quick-link per NV-08 + route map — **record as blessed deviation**).

**Design anatomy (`ACMP Meetings.dc.html`, already read — don't re-read):** screen enum list/create/overview/recording; lifecycle notready/scheduled/inprogress/concluded/cancelled; recState ready/pending/notranscript. Overview = header card (key chip + status chip + title + when·type·mode meta + primary action + tab strip) → conditional lifecycle banner → grid[ agenda-preview card | readiness rows + quick-links sidebar ]. `lcMap` primary/banner per state: notready→"Build agenda"(primary)+warn banner "Agenda not published"; scheduled→"Start meeting"(primary), no banner; inprogress→"Open live notes"(primary)+info banner; concluded→"Review minutes"(ghost)+success banner; cancelled→"Reschedule"(ghost)+neutral banner. Readiness rows: Agenda published / Quorum expected 6/8 / Topics scheduled. Recording: recReady(player+transcript)→**Webex P2 honest-defer**; recPending(warn pulse "being retrieved"); recNoTranscript("transcript unavailable").

**Reuse / refactor:** `MeetingPage.tsx` → meeting **shell** (header card + tab NavLinks + `<Outlet/>`); keep `AgendaBuilder` + `MeetingWorkspace` as-is, re-homed. New: `MeetingOverview.tsx`, `MeetingRecording.tsx`, `MeetingMinutes.tsx` (extract the P7 gate).

**DV-16 (re-add):** actual-time + outcome control in `MeetingWorkspace` `ActiveItem` (~line 264), wired to `useRecordActualTime` (exists in api/meetings.ts — takes `actualMinutes` + `outcome`). Verify the `outcome` enum values from the backend agenda-item outcome (Pending/… ) during impl. **DV-21:** AgendaBuilder pool label "Scheduled topics" → "Prepared" (source is already Prepared topics). **DV-03:** timer mm:ss/h:mm:ss already correct in `formatElapsed` — confirm only.

**Tests/i18n:** update MeetingPage.test (shell+routes), MeetingWorkspace.test (DV-16), App.test (nested meeting routes); add MeetingOverview/Recording tests; add i18n `meetings.overview.*` / `meetings.recording.*` / `meetings.tab.{overview,attendance,notes}` / DV-16 actual-time keys / reconcile `meetings.banner.*` to the 5 lifecycle states (EN+AR parity). Full suite green, ≥95% per-file. Live meeting-workspace VR (dev-stub fallback; `acmp-admin` password caveat). Output: before/after + findings-closure table + GO/NO-GO.
