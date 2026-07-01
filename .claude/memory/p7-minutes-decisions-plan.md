---
name: p7-minutes-decisions-plan
description: "P7 (Minutes & Decisions) slicing, scope decisions, and resume point — P7a merged, P7b next."
metadata: 
  node_type: memory
  type: project
  originSessionId: cfdd22d5-c268-445a-a119-069044e9f08e
---

P7 = Minutes & Decisions, built in 4 slices (Decisions before MoM, since the minutes doc aggregates a real decision).

**P7a — Decisions module backend: DONE & MERGED to main** (PR #58, squash `0fbcaf7`, 2026-07-01).
New `Decisions` module (Decision + owned DecisionCondition): W12 record/issue (chair approve+override), W21 supersede, immutable-after-Issued (AC-027/028), `DECN-YYYY-###`, `ITopicDecisionRecorder` seam (Topic→Decided), DecisionIssued notify fan-out. 620 tests green, coverage 99.61%.

**P7b — Decision detail UI + Decision.Title: DONE & MERGED to main** (PR #59, squash `2af08eb`, 2026-07-01).
Additive bilingual `Decision.Title` (migration `Decisions_AddTitle`); `isDecision` screen at `/decisions/:key` (DecisionIssued deep-link) + supersede dialog (full successor body — blessed deviation). **Operator FINAL decision (PR #60, `5a2d139`): content typed in one language is MIRRORED to both `LocalizedString` columns (`en === ar`)** — keeps both populated for SQL Full-Text Search; validators require BOTH EN+AR. (An earlier "entered language only" variant was shipped then reversed.) `MaximumLength(512)` on Title. Honest defers: Convert-to-ADR stub, from-topic/successor-key links omitted (ADR-0001 Guid-only DTO), Alternatives-as-text, vote/audit-timeline → P9/P14. Record/Issue UI out of scope. BE 622 green + gate 135 files @99.62%; FE 422 green (decisions 100% lines); dev-stub VR vs isDecision done (EN-light+AR-RTL-dark, no drift). AC-027/028 stay Partial (Met→P17).

**Locked scope decisions (operator):**
- MoM lifecycle = **5-state** `Draft→InReview→Approved→Published(+Superseded)` (doc 11 literal), NOT the design's 3-toggle — reconcile the design as a blessed deviation in P7c.
- Honest defers (do NOT build): Convert-to-ADR (W17) → P9/P11 (P7b = disabled stub); **AC-029** downstream-link-gate → P8 + **OQ-045** retrofit; SoD-3 co-attestation → P9; crypto hash-chain → P14.
- **P7b: ADD a `Title` LocalizedString to the Decision aggregate** (new migration + capture in record/supersede) for a design-faithful header — operator chose this over outcome-as-headline. This reopens the merged P7a model slightly.
- P7b honest-empty/deferred on the decision detail: vote tally → P9; immutable-history timeline → needs audit-query API (BL-066/P14); affected-systems → not modeled.

**P7c — MinutesOfMeeting backend: DONE & MERGED to main** (PR #61, squash `66abd35`, 2026-07-01).
MoM homed IN the existing **Meetings module** (docs/11 §B/§C — NOT a new module; differs from P7a). New `MinutesOfMeeting` aggregate + 5-state `MinutesStatus`; 7 command slices + 2 reads; `MIN-YYYY-###` via the existing MeetingKeyGenerator; owned bilingual `Summary` (mirrored) with unique `(Key,Version)`; migration `Meetings_Minutes`; `MinutesEndpoints`. AC-014/036/037/038 → **Partial**. BE 661 green + gate 150 files @99.63%.
LOCKED for P7c (do NOT re-litigate): **version-preserving supersede** (same MIN key, Version++, `PublishedCorrection` one-shot successor, prior→Superseded+backlink) — UNLIKE Decisions which mint a new key; **Approve & Publish are TWO distinct transitions** (notify-all + immutable on Publish; SoD-2 soft sole-author flag on Approve = `CreatedBy`); **5-state vs design 3-toggle = BLESSED DEVIATION** (design updated at P7d); **Content:json → single markdown `Summary` LocalizedString** (flagged data-model deviation); AC-037 InReview→Draft implemented per AC (doc 12 §6 table gap noted).

**P7d — Minutes UI: DONE & MERGED to main** (PR #62, squash `9de17e4`, merged 2026-07-01 via explicit operator override of the never-self-merge guardrail; local branch deleted).
Replaced the `MeetingMinutes.tsx` placeholder with the real tab (governs off `ACMP Agenda & Meeting.dc.html` isMinutes + denied). `api/minutes.ts` (TanStack Query hooks) + `MeetingMinutes.tsx` + `minutes.css`; renders by MoM status × role (create-draft / Draft editor / InReview review card / Approved / Published locked+version-history / Superseded / read-only-denied). i18n namespace `meetings.mom.*` (EN+AR). AC-014/036/037/038 stay Partial (Met→P17 live real-stack). FE gates green (tsc/vite/oxlint/vitest 440 + per-file cov ≥95%).
LOCKED for P7d: design's single "Approve & lock" → ONE **"Approve & publish"** action driving both transitions (notify on publish); numbered Decision/Actions section cards → a single **markdown Summary** body (md rendered as text, DV-04); SHA-256 footer→P14, Export-PDF=disabled stub; denied=read-only/no-access path (meetings routes have no extra role gate); i18n namespace MUST be `meetings.mom` (NOT `meetings.minutes`).
**Known flakes (pre-existing, not P7d):** NotificationCenter axe/canvas `getContext` (jsdom) intermittently fails vitest; `dnd-and-failures.spec.ts` agenda drag can time out in headless e2e. If CI e2e is red, check it's ONE of these before assuming a real break.

**Resume point: P7 DONE → P8.** #62 merged (squash `9de17e4`), main synced, feat branch deleted. Next: **P8 — Actions module** (backend then UI): unblocks MoM→Action linkage + the AC-029 downstream-link-gate retrofit onto the shipped IssueDecision path (OQ-045); `Action` aggregate `Open→InProgress→Blocked→Completed→Verified` + SoD-1 (verifier≠owner, AC-012/013). See [[exact-design-fidelity-visual-loop]] and guardrail #14.
