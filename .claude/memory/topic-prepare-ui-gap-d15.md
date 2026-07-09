---
name: topic-prepare-ui-gap-d15
description: "HIGH-PRIORITY defect — Topic Prepare (Accepted→Prepared) has no SPA UI, so the agenda pool is always empty and the intake→agenda core loop is broken in-product. Tracked as D-15."
metadata: 
  node_type: memory
  type: project
  originSessionId: c8e659d5-b6c4-496f-b58d-bc1e02886d1e
---

**Operator-reported bug (2026-07-07): agenda-builder "Search topics" shows nothing.** Root cause (proven, not inferred): the agenda pool is `GET /api/topics?status=Prepared`, but **the SPA never calls `POST /api/topics/{id}/prepare`** — only `useAcceptTopic`/`useReturnTopic` are wired (in the kanban). `Topic.MarkPrepared` requires status `Accepted`; with no UI trigger, **no topic ever reaches `Prepared`**, the pool is empty **by construction**, and the search filters an empty list. It's a missing-workflow (W4 "Prepare a topic"), not a search bug. Backend endpoint EXISTS (`TopicEndpoints.cs`, AC-035/BL-036) — shipped backend-only.

**Why AC-035 read "Met":** its live leg in E2E `core-loop.spec` does *"UI accept → **direct-HTTP prepare**"* — the test calls the endpoint directly, masking the absent UI. Transition + audit + eligibility are Met **at the API**; the **UI affordance is missing** → arguably Partial. I added a dated caveat to the AC-035 note in `docs/validation/acceptance-audit.md` (flagged for operator; didn't unilaterally re-flip).

**Design↔domain tension** (operator decided: KEEP the Prepared gate): the **domain package makes Prepare mandatory** (entity-lifecycles §1, W4; owner completes prep materials/affected-systems/risks before the committee's time; publish hard-guards `RequireStatus(Prepared)`). The **design files OMIT it** — kanban has 5 buckets (no `Prepared`; Accepted & Prepared both map to `accepted` in `topicMeta.ts`), timeline jumps Accepted→Scheduled, agenda pool labeled "Ready to schedule". Behavior SoT = package → keep the gate; design-update-owed logged.

**Tracked as D-15 (Tech-debt, Open, HIGH PRIORITY)** in `docs/execution/deferred-work-register.md` (v1.2.0). **Recommended fix (frontend-only, NO ADR), advisor-vetted:**
- **Tier 1 (core):** `usePrepareTopic` + a "Mark prepared" button on an `Accepted` topic detail (`TopicDetail.tsx` `.dt-actions` — already has disabled Add-to-agenda/Edit slots). **Gating = show-and-enforce** (render for any Accepted topic; backend 403s — the Owner is often a plain Member, so a role gate would hide it from the right person). **Invalidate THREE keys:** backlog + `['topics','prepared']` + `['topics','detail',key]`. Reword agenda empty-state (`meetings.poolEmpty`) to point to the action. i18n en/ar + tests.
- **Tier 2 (coherence):** a "Prepared" badge on kanban cards (Accepted & Prepared share the `accepted` bucket → otherwise invisible). NOT a 2nd action, NOT a bucket split (design = 5 fixed).
- **Tier 3 (optional):** notify the Secretary on prepare (`PrepareTopicHandler` only audits; `INotificationChannel` seam exists via ScheduleMeeting).
- **Flags (raise OQ if pursued):** no "un-prepare"/defer-from-Prepared exists (mis-prepared topic stuck until scheduled); design-update-owed.

Discovered mid-session while finishing [[p13-webex-integration-plan]] PR-A. See [[exact-design-fidelity-visual-loop]] (INV-014).
