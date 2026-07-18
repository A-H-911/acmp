---
name: p17a-test-hygiene
description: "★START HERE★ P17 in flight. P17a hygiene DONE on feat/p17a-test-hygiene (incl. un-breaking Keystone G-PROGRESS, red on main since e15cfff). Next = P17b-0 AC triage, which BLOCKS all spec work."
metadata: 
  node_type: memory
  type: project
  originSessionId: 6930f94b-a23e-40c1-9edb-0f39c09dd244
---

# P17 — Testing hardening (in flight, 2026-07-17)

Plan: `~/.claude/plans/shimmying-splashing-newt.md` (revision 2 — survived a devil's-advocate pass that broke 3 of
revision 1's claims). Slice order: **P17a hygiene → P17b-0 triage → P17b live specs → P17c VR.**

**The real driver is F-03, not AC-flipping.** `checkpoints.md:22` is a `[BLOCK]` release gate requiring the full loop
through *vote → ratify → decision → action → MoM publish* in staging. **No spec goes past the minutes gate today.**
P17b builds the evidence P19 blocks on; AC flips are a by-product.

## ★★ The find: `main` was NOT READY on a critical Keystone gate, and the record said green

**AC id cells in `acceptance-audit.md` MUST stay BARE (`| AC-001 |`). NEVER bold them.** The old memory rule
"never un-bold the AC ids" was **exactly backwards** and is deleted.

| commit | id cells | validator |
|---|---|---|
| `11c6372` P16b | bare | **OK** |
| `e15cfff` P16 #141 "G-IDS fix" | **bold** | **NOT READY — G-PROGRESS**, 74 gaps |
| P17a un-bold | bare | **OK, 7/7** |

- **G-IDS already skips this file BY NAME** — `validate_package.py:428-436`: `audit_view = "acceptance-audit" in
  pf.rel.lower()` then `for table in pf.tables: if audit_view: continue`. Its cells can never be duplicate
  definitions, bold or bare. **`_guess_id_column` is never reached for this file.**
- **G-PROGRESS has no such skip** (`:968-988`): `cell.strip().strip("`")` + `ID_TOKEN_RE.fullmatch` — **strips
  backticks ONLY, never asterisks** ⇒ `**AC-001**` matches nothing ⇒ all 74 read "not represented (coverage gap)".
- Validator unchanged since **2026-06-22** ⇒ no version excuse. P16's "pre-existing NOT READY on main" was false
  (main was OK) and its "validator green 6/6" was recorded **without re-running after the change**.
- **Why it went wrong:** P16 cited `_guess_id_column`'s ≥60% rule *correctly* but never checked the **caller**, and
  never ran the validator both ways. Reading a function ≠ knowing it runs. (I made the identical error shape the
  same day — see D-19 below.)
- Do NOT "fix" it by linking (`[AC-001](acceptance-criteria.md#ac-001)`) — no per-AC headings ⇒ 74 broken anchors.
  Backticks are stripped and change nothing. A `<!-- KEYSTONE -->` comment in the file now records this.

## D-19 was misdiagnosed in the register too — and I nearly repeated the mistake

Register blamed "order-/hash-dependent pick over the mocked decisions list". **There is no list.** Real cause:
`loc` (`LocationDisplay`) is **always mounted**, so `await findByTestId('loc')` resolved on its first poll and
`toHaveTextContent` evaluated **once** — before `SupersedeDialog.onConfirm`'s **post-await** `navigate()` flushed
(`SupersedeDialog.tsx:70-81`), leaving the initial route `DECN-2026-008`. Fix = retry the **assertion**:
`await waitFor(() => expect(screen.getByTestId('loc')).toHaveTextContent(...))`. 8/8 green.

★ **`:168`/`:188`/`:209`/`:217-220` were NOT touched — they are provably safe.** `mutateAsync` is *invoked*
synchronously inside `onConfirm`, so an awaited `user.click` guarantees the call happened; only code after the
handler's own `await` races. My plan said to "fix" them — that was a sub-agent pattern-match I'd repeated; reading
the handler settled it. **An awaited `user.click` guarantees the handler's SYNC body, nothing after its `await`.**

## P17a done (branch `feat/p17a-test-hygiene`, no AC flips)

D-19 fix + register row rewritten · `AGENTS.md:17` P12→P17/P18/P19 + P14 deferral (v1.2.0) · `security.yml` header
(claimed all scanners report-only; gitleaks/semgrep/trivy-fs are **gating**) · `→P14` blockquote judged =
**annotated, not rewritten** (audit-timeline actually shipped in the Audit slice #105; repo pattern is dated nested
annotations) · **live-leg rule written into `definition-of-done.md` §Acceptance Criteria (v1.2.0)**.

**Operator decisions this session:** (1) live-leg rule = **keep the bar, fix the label** — it was cited as
"per G-TRACE", but G-TRACE is Keystone *link-completeness* (`work-breakdown.md:331`), already passing, and says
nothing about verdicts. **NOT licence to flip ACs cheaply.** (2) AC-025 = test the ballot leg live
(`POST /votes/{id}/change` on a closed vote), record tally/chair-action as immutable-by-absence + raise an OQ.
(3) AC-054/055 = **force a real fire in e2e** (overrode my recommendation).

## ★ P17b-0 triage DONE (2026-07-17) — results in `~/.claude/plans/shimmying-splashing-newt.md` §top

21 candidate ACs binned (each read against literal G/W/T + verified API+UI surface). **11 clean UI flips / 5
interpretation / 3 product-blocked / 2 jobs.** Full table + per-AC evidence in the plan file.

★★ **HEADLINE D-15 REPEAT (verified) — decision issuance/ratification has NO working UI.** The "Record decision"
button is a **`disabled` "coming soon" stub** (`MeetingWorkspace.tsx:298-300`), and `api/decisions.ts` has only
read + supersede (no record/issue mutation). So a committee **cannot issue a decision or ratify a vote through the
SPA** — both API-only. This blocks **AC-015/016** AND the **F-03** `[BLOCK]` release gate (needs "chairman ratify →
decision record Issued" in staging). ("Create action" next to it is also disabled, but actions CAN be created from a
DecisionPage's CreateActionDialog.)

**Operator decisions (all recorded in the plan):**
1. **BUILD the decision-issuance UI inside P17** (record→issue with chairman override + secretary co-attestation;
   backend + SoD-3 gate already exist). Turns P17 into testing+feature ⇒ needs design (INV-014: look for a
   `.dc.html`) + TDD + review. **Fresh session.**
2. **BUILD the AC-014 SoD-2 warning badge** — render the existing `approvedBySoleAuthor` field (never rendered today)
   on the minutes view. Small.
3. **Flip all 5 (c) interpretations to Met-with-note + OQ each.** ⚠ **AC-022 the operator OVERRODE my
   recommendation** — its audit note MUST foreground the product↔criterion divergence (Fork 1: re-vote routes to
   `/change`, so the AC's "You have already voted" rejection never occurs in-UI; only the API 409 + DB unique index
   enforce one-ballot-per-voter). **Never write "the UI rejects a second ballot" — it's false.**

**P17b execution order (fresh sessions):** bin (a) 11 specs (no deps) → 5 (c) audit-row flips + OQs → AC-054/055
real-fire (R10 gate) → **[feature]** build decision-issue UI + AC-014 badge → their specs (AC-014/015/016).
**AC-034/043/048/057** carry `→ P17` tags but are unbuilt UI — not P17-closable; rewrite their residuals.

★ **e2e wall-clock measured (P17a PR CI): 5m57s against the 30-min cap** — R2 (budget) largely de-risked;
API-seeding via `scenario.ts` should suffice, storageState almost certainly unneeded.

## P17b spec #1 — ✅ RUN + GREEN (2026-07-17). AC-062/063 → Met.

**`src/Acmp.Web/e2e/p17b-traceability.spec.ts`** covers **AC-062 + AC-063** (the create round-trip proves both).
**Ran first-try green against the isolated `-p acmpe2e` stack: 2.5s test, 4.0s total, exit 0** — every `// VERIFY:`
selector resolved as predicted by the pre-run static check (read `Select.tsx`+`en.json`+`traceMeta.ts` first). The
warning header + `// VERIFY:` markers are now removed. **AC-062 + AC-063 flipped Partial→Met** in
`acceptance-audit.md` (evidence cites the live spec + the live-leg rule in `definition-of-done.md`, NOT "per
G-TRACE"; ids kept BARE; validator 7/7 incl G-PROGRESS). Spec + audit flips committed together (R9). No new seed
helper was needed (two topics via `apiCreateTopic`).

Confirmed harness facts from the live run: custom `Select` trigger's accessible name = its `ariaLabel` (wins over
placeholder) → `getByRole('button',{name})`; option = `role=option`, name = option label; POST hits `/api/traceability`
→ 201; `useCreateRelationship.onSuccess` invalidates `['traceability']` so the source panel refetches in place (no
reload). **Stack management this session: Docker Desktop was OFF; started it, `stop`ped the auto-restarted `acmp` dev
project, brought up `-p acmpe2e` (~6min build), ran, left e2e stack UP for the clusters.** ★ **CI e2e (workflow_dispatch
on the branch) = `success` in 6m13s** vs the 30-min cap — R2 fully de-risked, spec #1 Met flip is CI-backed on Linux.

★ **Run playwright from `src/Acmp.Web` with the LOCAL binary** (`./node_modules/.bin/playwright test <spec>` or `npm
run e2e -- <spec>`). `npx playwright` from the repo root fetches a SEPARATE `playwright@1.61.1` → "two different
versions of @playwright/test" / "did not expect test.describe() here". Bash cwd drifts between calls — `cd` explicitly.

## ✅ P17b spec #2 — voting cluster RUN + GREEN (2026-07-17). AC-023/024 → Met.

**`src/Acmp.Web/e2e/p17b-voting.spec.ts`** (2 tests, both green, 1.3s+2.0s): **AC-024** (secretary opens a vote 0-cast
below MinCast → clicks Close in UI → server rejects → vote stays Open + announced error, Fork 2) and **AC-023** (a
chairman genuinely casts via a 2nd `browser.newContext()`, secretary closes, closed roster renders the ballot
attributed by name+choice). Added 4 vote seed helpers to `scenario.ts` (`apiConfigureVote`/`apiOpenVote`/
`apiCastBallot`/`apiCloseVote`). AC-023/024 flipped Partial→Met (evidence cites the spec + live-leg rule; ids BARE;
validator 7/7). AC-021 (the 3rd voting AC) NOT done — its under-test path is the CallVoteDialog in MeetingWorkspace
(heavier: meeting in-session + agenda), separate from VotePage.

★★ **VOTE/BALLOT SEED GOTCHA (cost me one iteration):** eligible-voter `UserId` MUST be the **Keycloak sub**, NOT the
ACMP `member.publicId`. `Ballot.VoterUserId` = the KC sub; `CastBallot`/`Vote.Cast` match the ballot by
`CurrentActor.Of(user).Sub` = `ICurrentUser.UserId` (the sub) → a `publicId` eligible-voter row makes every cast 409
Conflict. GET `/api/members` exposes both — use **`member.keycloakUserId`** (added to the `ApiMember` interface) for
vote eligibility. Also: **Secretary CANNOT cast** (Vote.Cast = Chairman/Member) — AC-023 needs a real chairman bearer.
★ **SoD-1 / identity everywhere is the KC sub** — same rule for actions (owner/completer/verifier) and votes.

## ✅ P17b spec #3 — actions AC-013 RUN + GREEN (2026-07-17). AC-013 → Met.

**`src/Acmp.Web/e2e/p17b-actions.spec.ts`** (green first-try, 2.3s): a chairman owns+completes an action (2nd
context), then the **secretary (a different sub) drives Verify in the UI → 204 → Verified** (SoD-1 positive). Seed
helpers `apiCreateAction`/`apiStartAction`/`apiCompleteAction` added to `scenario.ts` (owner = `keycloakUserId`;
`SourceType='Topic'`, `SourceId`=a seeded topic id — SourceId is only NotEmpty-validated, no cross-module FK).
Enums travel as string names (`priority:'Normal'`, `sourceType:'Topic'`). AC-013 flipped Partial→Met (validator 7/7,
oxlint clean, id BARE). **Refactor:** extracted the shared `roleSession(page, role, acmpRole)` into `apiHelpers.ts`
(was duplicated in the voting spec) — voting re-run green after the change. traceability keeps its own
`secretarySession` (not churned).

## ✅ P17b spec #4 — notifications AC-052 RUN + GREEN (2026-07-17). AC-052 → Met.

**`src/Acmp.Web/e2e/p17b-notifications.spec.ts`** (green first-try, 6.2s): secretary opens a vote with the member as
eligible voter → member's bell shows the unread VoteOpened → clicking the notification navigates straight to
`/votes/{key}`. **No new seed helper** — reused `apiConfigureVote`/`apiOpenVote`. Harness facts: bell button
accessible name = `notif.title`/`notif.titleUnread` → `getByRole('button',{name:/Notifications/})`; the popup =
`getByRole('dialog',{name:'Notifications'})`; each row's `.notif-row-msg` / `.notif-key` button → `navigate(deepLink)`.
AC-052 flipped Partial→Met (validator 7/7, oxlint clean, id BARE).

## ✅ P17b spec #5 — decisions AC-028 RUN + GREEN (2026-07-17). AC-028 → Met.

**`src/Acmp.Web/e2e/p17b-decisions.spec.ts`** (green, 1.1s): a chairman supersedes an Issued decision via the
SupersedeDialog → 201 → navigates to the readable successor; the prior flips to Superseded (banner) with its
statement intact. Seed helpers `apiRecordDecision`/`apiIssueDecision` added. **Cheap Issued-decision seed:** record
`outcome='Deferred'` (a NON-follow-up outcome → skips the AC-029 downstream-link gate) with `voteId=null` (skips
SoD-3/vote-coupling), then issue `chairOverride=false`. Two honest audit notes: (1) actor is **Chairman** — supersede
is `DecisionChairApprove` (Chairman-only); the AC's "Secretary" is a product↔criterion nuance (OQ candidate); (2) the
`SupersededByDecisionId` back-link is **not rendered** (prior DTO carries only the successor Guid — flagged in
DecisionPage), so it stays on API/unit proof. AC-028 flipped Partial→Met (validator 7/7, oxlint clean, id BARE).

★★ **TALL-DIALOG VIEWPORT GOTCHA (cost 3 iterations):** the supersede dialog is taller than the default 720px
viewport, and the modal is `position:fixed` (no page-scroll), so its footer confirm button sat at y≈885 — visible +
enabled but UN-clickable (`.click()` timed out). Fix: `await page.setViewportSize({ width: 1280, height: 1400 })`
before opening a tall dialog. The small dialogs (trace/action/vote) didn't hit this. Diagnosis path: `toHaveValue`
proved the fields filled (ruled out validate), then logging the button `boundingBox` showed y=885 > 720.

## ✅ P17b spec #6 — meeting-workspace vote AC-021 RUN + GREEN (2026-07-17). AC-021 → Met.

**`src/Acmp.Web/e2e/p17b-meeting-vote.spec.ts`** (green FIRST-TRY, 4.3s — the heaviest cluster: 3 contexts, 8 seed
steps, 2 UI surfaces). Secretary calls a vote from the in-session MeetingWorkspace CallVoteDialog → configures →
opens on the vote page → config locks + roster = exactly the 2 voting-eligible members. Seed helpers added:
`apiPublishAgenda`/`apiStartMeeting`/`apiMarkAttendance`. **Key facts:** CallVoteDialog only CONFIGURES (Configured
state), the OPEN happens on VotePage's "Open voting"; the workspace `/meetings/{key}/notes` auto-activates the first
Pending item (no manual activate); voting-eligible roles = **Chairman + Member** (`DefaultVotingEligibility`, Secretary
NOT eligible); a meeting-LINKED vote's open enforces MinPresent against live attendance (count of Present +
IsVotingEligible rows, self-contained in Meetings) → must mark voters Present first; agenda must be **published before
start**. Used the tall-viewport fix again. AC-021 flipped Partial→Met (validator 7/7, oxlint clean, id BARE).

## ✅ P17b spec #7 — MoM lifecycle AC-036/037/038 RUN + GREEN (2026-07-17). ALL THREE → Met.

**`src/Acmp.Web/e2e/p17b-minutes.spec.ts`** (3 tests green FIRST-TRY: 1.5s/0.97s/1.2s). AC-038 approve&publish,
AC-037 request-changes, AC-036 supersede→v2. Seed helpers: `apiDraftMinutes`/`apiSubmitMinutes`/`apiApproveMinutes`/
`apiPublishMinutes`. **Key facts:** minutes page renders for an **InProgress** meeting (NO end step —
`meeting.status InProgress|Held|Closed`); one MoM per meeting; UI combines approve→publish into ONE "Approve &
publish" button; supersede via SupersedeMinutesDialog ("Minutes body"+"Reason for superseding"→"Publish correction")
→ 201 version=2; approve soft-SoD-2 non-blocking. Banners: Published="Published & locked…", Draft="Editable…".

## ★★★ bin (a) COMPLETE — 11 ACs Met this session ★★★

**062/063 (trace), 023/024 (voting), 013 (actions), 052 (notifications), 028 (decisions), 021 (meeting-vote),
036/037/038 (MoM).** 7 spec files, all live-verified on `-p acmpe2e` + CI-green. `scenario.ts` seed-helper library
spans topic + meeting(schedule/agenda/publish/start/attendance) + minutes(draft/submit/approve/publish) +
vote(configure/open/cast/close) + action(create/start/complete) + decision(record/issue); shared `roleSession` in
apiHelpers.ts. **REMAINING P17b (NOT bin (a)):** (c) flips 012/022/025/026/027 (+OQs — AC-022 note is load-bearing:
NEVER "UI rejects a 2nd ballot"); jobs 054/055 (real-fire, R10 gate); **[fresh-session] decision-issue UI build**
(unblocks AC-015/016 + F-03 — the real driver). See plan §top.

**Harness facts confirmed for the remaining specs:** seed via API with `captureBearer(page)` + a real bearer, reserve
the UI for the behavior under test (`scenario.ts` convention). `scenario.ts` has topic/meeting/agenda helpers but
**NONE for votes/decisions/actions/MoM/traceability** — the voting (021/023/024), MoM (036/037/038), actions (013),
decisions (028), notifications (052) clusters each need a seed helper added. Custom `Select` = button (accessible
name = ariaLabel/placeholder) → `role=option`, per core-loop.spec.

**Why I stopped at spec #1 (not all 11):** an e2e spec is worthless until RUN against the isolated `-p acmpe2e` stack
— authoring 11 unrun specs = confident-but-unverified output, the exact thing this session existed to remove. The
run is expensive (~6 min/run × selector-fix iterations) and environment-sensitive (**NEVER `npm run e2e:up`** — wipes
dev volumes; use `-p acmpe2e` after stopping dev). That + the session cost (~$210) ⇒ the live-run + remaining
clusters belong to a fresh session with clean context and budget. **✅ spec #1 now DONE (see section above). NEXT:
replicate the pattern for the remaining bin-(a) clusters — voting (021/023/024), MoM (036/037/038), actions (013),
decisions (028), notifications (052) — each needs a new seed helper added to `e2e/scenario.ts` FIRST (it has
topic/meeting/agenda helpers but NONE for votes/decisions/actions/MoM). Run-verify-then-replicate, one cluster at a
time; do NOT bulk-author unrun specs.**

## Gotchas for P17b/P17c

- **Playwright = 1.61.1** (lockfile), NOT the `^1.49.1` in package.json. VR baselines must come from
  `mcr.microsoft.com/playwright:v1.61.1-*` on the compose network — host-generated PNGs are `-win32` and CI
  (`-linux`) fails them as *missing*.
- **AC-041 needs Chrome AND Edge** (its literal text); config has one `chromium` project. And self-baselined VR
  proves *unchanged*, never "no LTR artifacts" ⇒ close on human sign-off + regression-guarding, or state the limit.
  **Never widen `maxDiffPixelRatio` to pass.**
- **e2e: 30-min hard cap**, PR/`workflow_dispatch` only (never branch push); `core-loop.spec` = one test at
  `setTimeout(180_000)` + 3 PKCE logins. **API-seed via `scenario.ts`**; drive only the AC-under-test through the UI.
  Measure after **spec #1**.
- **`deploy/.env.example` is the PRODUCTION template** ("Copy to deploy/.env") — put the minutely
  `ACTION_REMINDERS_SWEEP_CRON` in the **e2e workflow job `env:`** (shell env beats `--env-file`), and give the
  compose var a safe `:-0 6 * * *` default. Compose passes **no** `ActionReminders*` to the worker today.
- **R10 (blocking for AC-054/055):** verify `SweepActionRemindersHandler` is a **no-op emitting no AuditEvent when
  nothing is due** — every audited write takes ONE global exclusive `sp_getapplock('acmp-audit-chain')`
  (`AmbientTransaction.cs:57`, ADR-0028), so a minutely sweep could contend for 30 min. If it emits
  unconditionally, **STOP and ask**.
- **Piping swallows `$?`** — `npm run test:cov | grep` reported "pass" while hiding the exit code. Capture exit
  codes explicitly. Same trap as `dotnet format | tail`. See [[ci-gates-run-locally-pre-push]].
