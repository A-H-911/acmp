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

## Next: P17b-0 triage — BLOCKS all spec work

Read each of the **≤19** candidate ACs' literal Given/When/Then against **both** the API (`src/Acmp.Api/Endpoints/`)
**and the UI**; bin: closable-by-spec / needs-product-change / needs-interpretation / unbuilt-UI. **The "~19 flips /
36→55" forecast is RETRACTED** — it was never triaged. **AC-034/043/048/057 are unbuilt UI** and not P17-closable
despite carrying `→ P17` tags.

★ **The D-15 precedent is the hazard.** S6b wrote live E2E and flipped only **AC-035** — because writing it revealed
the SPA **had no "Mark prepared" button**; the E2E had masked it with a direct HTTP call. Voting / decision-issue /
MoM-publish / action-verify UI are **unverified**. Expect some ACs to convert to D-15-shape findings. **Stop and ask
— do not build product inside a testing slice.**

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
