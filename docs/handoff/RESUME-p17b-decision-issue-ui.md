# RESUME PROMPT — P17b final slice: decision-issuance UI + AC-014 badge

> Paste the block below into a fresh Claude Code session. It is a **feature build**, not a test spec — give it full TDD + design-fidelity + review care (it touches authorization + SoD). This file is also committed so it survives a session clear.

---

Resume ACMP P17b — the FINAL slice: build the **decision-issuance UI** (record→issue) + the **AC-014 SoD-2 badge**. New session; this is a **FEATURE build**, not a test spec. Re-orient before acting.

**FIRST (read, don't act yet):**
- Re-read `AGENTS.md` + `docs/progress/status-report.md` (§Current position).
- Read the plan: `~/.claude/plans/shimmying-splashing-newt.md` — §"**Decision-issuance UI — build spec**" holds the full scoped build (backend VERIFIED complete). Source of truth.
- The START-HERE memory (`p17a-test-hygiene.md`) is auto-loaded — trust its "18 ACs Met" + seed-helper inventory + gotchas.
- **Verify with git before touching anything:** the previous session shipped **PR #144** (`feat/p17b-live-leg` → main): 18 ACs flipped Partial→Met across 9 live spec files, ALL CI checks green (incl e2e). **Confirm PR #144 is MERGED** (it was green + `mergeable=MERGEABLE`); if merged, branch `feat/decision-issue-ui` off updated `main`; if NOT yet merged, either merge it or branch off `feat/p17b-live-leg` (which carries the seed-helper library the new live specs need).

**STATE:** The P17b live-leg slice is DONE (18 ACs Met: bin-a 11 + interpretation 5 + jobs 2). The **ONLY** remaining P17b work is this feature build. It unblocks **AC-015/016** + the **F-03 `[BLOCK]` release gate** — the real driver.

**SINGLE TASK:** build the record→issue decision UI (replace the disabled stub) + the AC-014 badge, then their live specs (AC-014/015/016).

**THE GAP (D-15 shape):** a committee **cannot issue a decision or ratify a vote through the SPA**. The "Record decision" button is a disabled "coming soon" stub (`MeetingWorkspace.tsx:298-300`, `title=t('meetings.comingSoon')`, Icon `decision` + `t('meetings.recordDecision')`), and `api/decisions.ts` has only read + supersede (no record/issue mutation). Both are API-only today.

**BACKEND — COMPLETE, no backend work:**
- `POST /decisions/` — `RecordDecisionBody(TopicId, MeetingId?, Outcome, Title, Statement, Rationale, Alternatives?, VoteId?, Conditions[])` → Draft. Policy `DecisionRecord` (Sec/Chair).
- `POST /decisions/{id}/issue` — `IssueDecisionBody(ChairOverride, OverrideJustification?)` → Issued. Policy `DecisionChairApprove` (Chairman only).
- Gates in `IssueDecision.cs`: AC-029 downstream-link (`:108` — follow-up outcomes {Approved/ConditionallyApproved/EnhancementsRequired/DesignChangesRequired/ResearchRequired} need ≥1 linked Action OR a downstream trace edge); vote-coupling integrity (`:120-127` — a set VoteId must resolve, match the topic, and be Closed); SoD-3 co-attestation (`:130`); issuing a **vote-coupled** decision **auto-ratifies the vote** (`:138` — the only path to Ratified, no separate ratify UI).

**★ SoD-3 is SIMPLER than AC-015/016's wording** — there is **NO separate "secretary co-attestation" UI field.** The co-attester = **whoever CLOSED the vote** (`vote.CounterUserId`); the gate only forbids the **issuing chair** from being that person. So:
- **AC-015** = chair closes the vote himself → tries to issue → server **403** (`Decisions.DecisionIssueDenied` audit). The UI must LET the chair attempt issue and **surface the 403 inline** (the D-15 show-and-enforce lesson).
- **AC-016** = secretary closes the vote → chair issues with override + justification → allowed, vote → **Ratified**. UI collects `ChairOverride` + `OverrideJustification` only.

**FE WORK (all that's needed):**
1. `api/decisions.ts`: add `useRecordDecision` (POST `/decisions`) + `useIssueDecision` (POST `/{id}/issue`) — mirror the existing `useSupersedeDecision` shape.
2. A **record→issue dialog** (mirror `CallVoteDialog.tsx`, the wired sibling), launched by replacing the **disabled stub at `MeetingWorkspace.tsx:298-300`**. Captures outcome/title/statement/rationale/alternatives?/conditions[], couples the agenda item's `VoteId`, then issue with override+justification. Chairman-gated (`DecisionChairApprove`), show-and-enforce (surface the 403 inline).
3. i18n EN+AR (replace the `meetings.comingSoon` tooltip); **INV-014 fidelity**; TDD (component tests) + code review.
4. Then the live specs: **AC-015** (chair-closes → issue 403), **AC-016** (secretary-closes → chair issues w/ override → decision Issued + vote Ratified). Also lands the **F-03** core-loop leg (chairman ratify → decision Issued).

**INV-014 — design references EXIST (READ DIRECTLY with file tools, NOT via MCP):** `ACMP product context/ACMP Decision, Voting & ADR.dc.html` + `ACMP product context/ACMP Create Flows & Dialogs.dc.html` (both mention record/issue/override). This is **NOT** a no-reference composition — match them exactly.

**AC-014 SoD-2 BADGE (separate, small):** render the existing `approvedBySoleAuthor` DTO field (`api/minutes.ts:41`, `MinutesDtos.cs:30` — set server-side but **never rendered**) as a warning badge on the minutes view (`MeetingMinutes.tsx`). Then a live spec flips AC-014.

**SEED HELPERS READY** (`e2e/scenario.ts`, from the live-leg slice — reuse for the AC-014/015/016 specs): `apiRecordDecision`, `apiIssueDecision`, `apiConfigureVote`/`apiOpenVote`/`apiCastBallot`/`apiCloseVote`, `apiDraftMinutes`/`apiSubmitMinutes`/`apiApproveMinutes`/`apiPublishMinutes`, the meeting chain (`apiPreparedTopic`/`apiScheduleMeeting`/`apiAddAgendaItem`/`apiPublishAgenda`/`apiStartMeeting`/`apiMarkAttendance`), + shared `roleSession(page, role, acmpRole)` in `e2e/apiHelpers.ts`.
- ⚠ For AC-015/016 the decision is **vote-coupled** (VoteId set) → the vote must be **Closed** first, and if the outcome is a follow-up type (e.g. Approved) **AC-029 needs ≥1 downstream link** before issue → either use a non-follow-up outcome OR seed a linked action / trace edge. To make the chair the closer (AC-015) close the vote with the chair's bearer; to make the secretary the closer (AC-016) close with the secretary's bearer (`apiCloseVote` is Vote.Manage = Chair/Sec).
- ⚠ Identity everywhere = **`member.keycloakUserId`** (the KC sub), NOT `publicId`.

**⚠ ENVIRONMENT LANDMINES:**
- e2e = isolated **`-p acmpe2e` ONLY**, after `stop`ping the dev stack. **NEVER `npm run e2e:up`** (wipes dev volumes). See [[e2e-local-run-nondestructive]] + [[dev-stack-rebuild-pitfall]].
- Run playwright from `src/Acmp.Web` with the **LOCAL binary** (`./node_modules/.bin/playwright test <spec>` or `npm run e2e -- <spec>`), **not `npx`** (fetches a duplicate → "two different versions of @playwright/test"). Bash cwd drifts — `cd` explicitly.
- **Tall dialogs need `page.setViewportSize({ width: 1280, height: 1400 })`** — fixed modal, footer off-screen at the default 720px.
- Live e2e run ≈ 6 min stack build + a few s/spec; CI 30-min cap has ample headroom.

**GATE DISCIPLINE:** FE-only build → `npm run test:cov` (≥95% per-file) + `check-i18n.mjs` (add every enum value to BOTH locales by hand) + `npm run build` + `oxlint`. Run the Keystone validator (`python <keystone>/scripts/validate_package.py docs`) after any docs change. **AC id cells in `acceptance-audit.md` MUST stay BARE** (bold breaks G-PROGRESS). Specs + audit-row flips ship in the **SAME PR** (R9). This touches **authorization + SoD** — give it TDD + review; verify before claiming, every step.

---
