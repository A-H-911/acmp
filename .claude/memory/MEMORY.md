# Memory Index — ACMP

> Compacted 2026-08-19 (7th). One line per memory; detail lives in topic files and the package.
> Read the linked file before acting. ⚠ Keep this file under ~17KB — past its read limit
> the tail is SILENTLY dropped on load, so an over-long index is worse than a short one.

## ★ 2026-08-19 · **PH-6 both slices BUILT** · `SL-030` green in **PR #295** · `SC-021` needs the operator

> ⚠ **READ THE LIVE NUMBERS** — `gate_run()` + `readiness_check("package")`. Package readiness is
> deliberately whatever the current batch makes it; `acs-met` failing on exactly your batch's Pending
> ACs is the EXPECTED build-window state, not a regression.

- ⭐⭐ **`SL-030` IS BUILT AND `AC-114` IS `Met` (`AV-192`).** PR **#295**, all 9 CI checks green,
  **NOT merged** — `SC-021` is Proposed and one half of the PR reverts if the operator rejects it.
  Package readiness is now **`ready:true`**. `WBS-22` + `WBS-22.3` sit at **Review**, deliberately, so
  slice readiness stays honestly false rather than manufactured green. WS-3's AC pipeline (108
  requirements, `DW-029`) is still untouched and needs its own conversation.
- ⭐⭐ **The egress sweep corrected the PLAN in BOTH directions (`LL-006` again).**
  **Notification bodies were never a leak** — `TopicNotifications` interpolates only the topic KEY,
  never a title, and every recipient is the Secretary roster or the submitter. The plan's "builders
  must take a restriction flag" was simply wrong. **Dependencies was a 4th surface nobody listed**
  (`DEF-090`) — same create-time snapshots, and the **Reports** surface `AC-114` names loads that very
  register. ⚠ **`MoveTopicPriority` is deliberately UNFILTERED** and says so: its gate is
  Chairman/Secretary-only and filtering would corrupt the renumbering. Reading it killed a false alarm.
- ⭐⭐⚠ [**A green checker may have had NOTHING to check — `LL-007`, Proposed**](scan-must-prove-it-had-a-subject.md)
  `npx tsc --noEmit -p tsconfig.json` in `src/Acmp.Web` **exits 0 while checking zero files** — that
  file is solution-style (`"files": []` + references). It blessed a tree with **13 type errors that had
  been failing `npm run build` for TEN COMMITS** (`DEF-091`, high). `vitest` never sees them: it
  transpiles, it does not typecheck, so 1241 green tests certified code that would not compile.
  **Use `npm run build` or `-p tsconfig.app.json`.** I did not catch this by being careful — vitest
  caught it and I went looking.
- ⭐ **`SL-030` design, so it is not re-litigated:** one narrow port `ITopicConfidentiality` (NOT a
  method on `ITopicReader` — that handle mints pre-signed URLs). It answers with the **whole hidden
  set** because the dependency register **pages and totals**, so the filter must compose before
  `CountAsync`/`Skip`. Hidden set is **derived from `VisibleTo` by subtraction** → one expression of the
  rule. **Agenda items masked in place** (the slot means something); **edges dropped** (an edge is a
  pointer, and a blank endpoint enters the BFS as an empty-Guid node). **Both endpoints filtered**, so a
  hidden focus is response-identical to a nonexistent id — no focus guard anywhere. **`TopicId` survives
  masking**: the SPA keys rows by it, and topics are read by KEY, which already 404s.
- ⭐ **`LL-006` (a proxy is not the artifact) is now Approved + pinned**, joined by `LL-007` (Proposed).
  ⚠ **`Timeline.tsx` and `Calendar.tsx` are deliberate honest SHELLS** — routed, commented, drawing
  nothing. Requirement ids in source comments are a **positive-only** instrument, and one was a
  *deferral* note (`InvariantStatus.cs:7`). Detail: [package-mechanics-proven-2026-08-18.md](package-mechanics-proven-2026-08-18.md)
- ⭐ **The DW-029 sweep found what no gate could:** `DW-033`–`DW-036` (unbuilt, no record anywhere),
  `DEC-062` (an operator decision living only in a code comment), `DW-037` (blocker silently cleared),
  `DEF-088`/`DEF-089` (divergences). **24 requirements are now `Deferred`** and the three status labels
  finally mean different things.
- ⭐ [**Store mechanics proven by experiment**](package-mechanics-proven-2026-08-18.md) — `acs-met`
  counts by `retired_in` and **ignores `lifecycle_status`** (a Deferred AC still counts, so ACs for
  unbuilt work block readiness forever); `entity_upsert` **preserves omitted nullable fields** but
  requires every NOT NULL one; **slice-scope `wbs-done` is vacuous for all 28 old slices**
  (`slice_id` NULL on all 155 — `DEF-087`), which also **breaks AC→slice derivation**; `G-TRACE` needs
  three legs where the advisory needs one.
- ⚠⚠ **Trap 25:** `gh pr merge` can squash-merge **remotely** then abort locally, leaving a tree that
  looks like lost work. Check `gh pr view --json state` first, verify by CONTENT, then `git reset --hard`.
  ⚠ A verification grep can count **your own comment text** — match quoted entries, not substrings.

## ★ Tamheed 4.4.1 · earlier state (ladder P1–P19 complete; superseded by PH-6 above)

- ⚠ **`PH-3` stays `Approved` ON PURPose** — `WBS-20.4` is the email adapter vs a hard constraint
  (`DEC-055`). Do **not** "repair" it to look uniform; that is the manufactured-status move `DEF-010`
  records. `PH-0`–`PH-2`, `PH-4`, `PH-5` are Implemented.
- Two advisories fail deliberately and are **not tasks**: `deferred-work-reviewed` (triggers unfired —
  closing a row to green it manufactures status) and `acs-slice-bound` (`AC-109`–`AC-112`, accepted by
  `DEC-058 d3`). `AC-113` IS slice-bound, so it did not grow that list.
- **Five lessons Approved + PINNED** (`LL-001`…`LL-005`) bind every session via the auto-loaded note.
  ⚠ `LL-006` is **Proposed and needs the operator's interview** — see above.
- `DEC-057`/`DEF-084`: eight unreachable methods were **three** problems — four wired (#289), two
  deferred (`DW-030`), two uncallable by construction. `DEF-085`: a disabled member was permanently
  locked out; `SC-017` added `EnableUserAsync`. `DW-017` (#290): owned-child audit rows carried empty
  before/after. `DW-031` (#291) + `DW-009` (#292) were **checks that found real bugs**.
- ⚠⚠ **`$?` AFTER A PIPE IS THE PIPE'S LAST COMMAND** — `dotnet format ... | tail; echo $?` printed 0
  while the real exit was 2. Fired again this session: reading it bare caught a real `IMPORTS` failure.
- **PRODUCTION IS DEPLOYED AND RECONCILED**; `/readyz` 200 on all four checks, `committee_members`
  1 → 27. The `RISK-007` adoption clock started 2026-08-17.
## Earlier 2026-08 — durable findings only (superseded state removed)

> Most of this section's old contents were either SUPERSEDED (a deploy that has since happened) or
> promoted into `prm-next.md` §5, which is now the single place traps live. Kept here: the findings
> with their own topic file, and the facts that re-frame how the register reads.

- ★★★ [**`DEF-078`: a green control can be blind**](a-green-control-can-be-blind.md) — the defect row's own prescribed command could not measure anything (`/readyz` returns 200 via the SPA fallback with the API dead); a healthcheck evaluated ZERO checks; gitleaks passed 153 commits because `.gitleaks.toml` allowlisted every markdown file.
- ★★ [**`DEF-056`'s "measured blocker" WAS NOT REAL**](an-absence-needs-a-proven-instrument.md) — rows were written all along; the helper read `AuditEvent.Action`, **NULL on the v1 rows every refusal writes**, and its two `NotContain` controls passed **VACUOUSLY**. **An absence is only evidence if the instrument is proven present.** `AV-159` was wrong the same way.
- ⚠⚠ [**v4 store + 4.4.x lesson mechanics**](tamheed-v4-and-liveness.md) — `status` → `lifecycle_status`; build payloads from the JSONL; `WVR-` operator-only; progress has a `correction` event. Approving a lesson **refuses without `operator_confirm: true`** and rejects content drift.
- ★★ **`DW-029` re-frames every status in the package**: of 222 requirements, **exactly the 60 with an AC are Implemented and exactly the 162 without one are Approved**, because a requirement advances ONLY via the AC auto-advance trigger. **So requirement status measures whether anyone WROTE an AC, not whether it was built** — and `v_backlog` reports that faithfully. `DEF-012` is Won't-fix (`DEC-055`): the one mechanical rule that would have "fixed" it closes `WBS-20.4`, the **email adapter**, against a hard constraint.
- ⚠ **A defect row's predicted cause is a HYPOTHESIS, not evidence** (`DEF-067`) — its row forbade the fix that worked, and the real cause was test *duration*. Same family: `DEF-084` reported eight methods as one finding; they were three. Read the implementation, including when the row reads pre-checked.
- ⚠ **Read `ADR-0043`, NOT `ADR-0042`** — 0042 is Superseded: it wrongly claimed Guest is stream-bounded (E.3 bounds a guest by a TIME WINDOW). All 7 clauses carried over verbatim. ADR-0043 steps 1–8 all shipped; `AC-010` Met.
- ⚠⚠ **Stream scope had NEVER run on a real DB** (`DEF-066`) — `member_streams.StreamId` was an IDENTITY column and four suites were green over it. See [[inmemory-provider-hides-db-refusals]]. `DEF-068`'s landmine: `PermissionMatrixTests` evaluates cells with **no resource**, and a 2-param handler is never invoked without one — **a stream-scoped policy is RESOURCE-ONLY**.
- **Stale branches** (all pre-date `4c1b356`, so they carry `DEF-064`'s broken `ar.json` — merging one now fails `check-i18n` loudly): `chore/design-update-round2`, `chore/docs-v8-local-design`, `feat/budget-notification-observer`, `feat/p13-webex-integration`, `docs/defer-p14-tarseem`, `feat/audit-adr`.
- **Tamheed acceptance series `findings_13`–`findings_16` complete except §6:** the CLAUDE.md obligations note demonstrably TRANSFERS to fresh contexts; whether a fresh session DISCHARGES it is unproven. Only an **interactive** fresh session can close it.

## Shipped, reference only (detail in the package)

- **ADR-0039 `AC-090`** (#239) was unsatisfiable by construction; now per-request revalidation.
  ⚠ **An unknown subject must be ALLOWED** — ADR-0004 provisions JIT, so failing closed refuses every
  first login. Seam `IPrincipalRevalidator`; window `AccessExpiresAt`.
- **`DEF-052`: there was NO read-side role gate anywhere** — every named policy is a WRITE capability.
  Fixed by `GuestSurfaceMiddleware`, deny-by-default rather than a policy per group. ⚠ **This is the
  fact `SL-030` turns on: no topic read path calls `IAuthorizationService` at all.**
- Guest-expiry sweep (#240) hourly. ⚠ Predicate is `AccessExpiresAt != null && < now`, so an invited
  member (role `Guest`, null window) is **not** swept. **ACMP has no reschedule** (`DW-025`).

## Standing rules & gotchas (read before editing)

- [★ Read the implementation before calling it a defect](read-before-calling-it-a-defect.md) — **nine** instances; never caught by a gate. It has made defects smaller, made one **disappear**, and this session **killed one in three minutes** (the sweep-vs-invitee scare). **Read the predicate, not the doc comment describing it.**
- [★ The InMemory provider hides DB refusals](inmemory-provider-hides-db-refusals.md) — **`DEF-066`: stream assignment had NEVER worked on SQL Server**, and ADR-0043 steps 3+4 shipped on top of it with four suites green. Ask "has this write ever run against SQL Server?" Also: EF **cannot** scaffold an identity removal; `ef migrations add` rewrites the snapshot as CRLF; semgrep taints a method **parameter** reaching `CommandText`.
- [★ Controls must DETECT **and** TELL](controls-must-detect-and-tell.md) — now **nine** (DEF-023/030/031/032, OQ-068, DEF-051, DEF-054, + both DEF-066/step-5 migration guards, which are the first ones actually **mutation-tested** rather than asserted in a comment). The "tell" half is normally the untested half.
- [★ Verify mechanically, not carefully](verify-mechanically-not-carefully.md) — `entity_upsert` replaces FULL rows; the JSONL flushes on EVERY write, so git HEAD is a live baseline. ⚠ **Also now: a measurement that indicts known-good code is measuring itself** (`grep -c $'\r'` degraded to `grep -c ''`), and `/acmp/*` env params are **LF**, not CRLF.
- ⚠ **PowerShell joins arrays with SPACES.** `aws ... --output text` returns an **array of lines**; `[IO.File]::WriteAllText(path,$array)` writes one space-joined line and would have **destroyed the SSM env file**. Use `($v -join "`n")` and verify the line count before publishing.
- ⚠ **`gh pr create --body` and `git commit -m` with backticks/quotes break under PowerShell** — always `--body-file` / `-F <file>`.
- ⚠ **`open_question.lifecycle_status` is a CHECK** over `Draft/Proposed/Approved/Rejected/Deferred/Implemented/Superseded/Obsolete` — "Resolved" rolls the whole batch back. `defect.fixed_by` is a **FK**; PR refs go in `custom_attributes`.
- ⚠ **The keycloak container's `docker exec` shell has no `KC_BOOTSTRAP_ADMIN_PASSWORD`** — the entrypoint exports it for its own process only. Read `/run/secrets/kc_bootstrap_admin_password`.
- ⚠ **Windows `python3` cannot see Git Bash's `/tmp`** — pass Windows-style absolute paths when building SSM `--parameters file://` payloads.
- [⚠ Baselines are numbers, not properties](baselines-as-numbers-not-properties.md) — a count-based test on a shared topic can never discriminate.
- [⚠ Immutable history → cleanup is asymmetric](immutable-history-cleanup-asymmetry.md) — deleting a Keycloak user ORPHANS its member rows forever. **Disable, never delete.**
- [A static file cannot configure a live realm](a-static-file-cannot-configure-a-live-realm.md) — `realm-export.json` reaches **fresh stacks only**; `reconcile.sh` is the only seam to prod/UAT.
- [Write the handoff LAST](write-the-handoff-last.md) — it found `DEF-053`/`DEF-054` last time. Stamp superseded files with ⛔ immediately.
- [Commit package writes before git ops](commit-package-writes-before-git-ops.md) · [Tamheed stale .lock + PID reuse](tamheed-stale-lock-pid-reuse.md) · [Tamheed data repair](tamheed-data-repair.md) · [migration history](tamheed-migration-reverted.md)
- [Localhost CI hides load races](localhost-ci-hides-load-races.md) · [Git push hang → `gh auth setup-git`](git-push-hang-fix.md) · [Run CI gates locally pre-push](ci-gates-run-locally-pre-push.md) · [Always stage .claude/memory in commits](always-stage-claude-memory-in-commits.md)
- [Coverage & E2E mandate](coverage-and-e2e-mandate.md) — ≥95% FE+BE + adversarial E2E. ⚠ **The Playwright suite is NOT UAT-only** (`e2e.yml` runs 7 services with a real Keycloak per PR) **but runs with `KEYCLOAK_ADMIN_ENABLED=false`**, so it never touches the ADR-0038 write path.
- [E2E local run (non-destructive)](e2e-local-run-nondestructive.md) — **`-p acmpe2e` ONLY**, never `npm run e2e:up`. · [Dev-stack rebuild pitfall](dev-stack-rebuild-pitfall.md) — **never `up --build`** the long-lived dev stack.
- [Exact design fidelity + visual loop](exact-design-fidelity-visual-loop.md) · [A green suite is not a look](a-green-suite-is-not-a-look.md) — ⚠ the throwaway harness must import **only** the stylesheets the real route imports.
- [Design: breadcrumb spacing](breadcrumb-spacing-rule.md) · [i18n parity ≠ completeness](i18n-parity-not-completeness.md) · [Web visual-verify cache busting](web-visual-verify-cache-busting.md)
- ⚠ **`.adm-detail-card` has no padding and clips its children** — anything opening a popover needs `.adm-card-overflow`. · **`userEvent.setup()` installs its own clipboard stub** — define a clipboard spy *after* it.
- [User prefers simple English](user-prefers-simple-english.md) · [Phase prompt Standard Footer](phase-prompt-standard-footer.md) · [Install the schedule, not just the daemon](install-the-schedule-not-just-the-daemon.md) · [Arabic rename is a grammar rule](arabic-rename-grammar-not-substitution.md) · [A clean scan must prove it had a subject](scan-must-prove-it-had-a-subject.md) · [Guard the property, not the value](guard-the-property-not-the-value.md) · [The suite assumed a fresh database](e2e-assumes-a-fresh-database.md) · [The feature is often already half-built](check-before-building.md)
- ⚠ **AC id cells in markdown tables must stay BARE** (`| AC-001 |`, never bolded) — bolding breaks the Keystone G-PROGRESS gate.
- ⚠ **A new advisory can turn `main` red with no code change** — `GHSA-q939-rpr3-3284` (SSH.NET) blocked every merge mid-session. "It's only tests" is how a blocking gate becomes advisory.
- ⚠ **A compose `secrets:` entry whose FILE IS MISSING fails the WHOLE stack** — any mounted secret must be written **unconditionally** by `gen-secrets`.

## Completed ladder P1–P19 + PH-5 (reference only — do not re-open)

- [PH-5 / SL-025 — UAT is live and LOGIN WORKS](ph5-sl025-uat-live.md) · [PH-5 AWS deployment](ph5-aws-deployment.md) · [P19 release readiness + D-23](p19-release-readiness.md) · [P18 deployment](p18-deployment.md) · [P17b decision-issuance UI](p17a-test-hygiene.md) · [P17/P18/P19 slice notes](next-p17-p18-p19.md)
- Ladder plans (reference only, superseded by the package's slice rows): `p16-*`, `p12-*`, `p11-*`,
  `p10-*`, `p9-*`, `p8-*`, `p7-*`, `p6a-*`, `p6b-*` in this directory.
- [Audit slice (AC-017)](audit-slice-literal-ac017.md) · [Topic Prepare UI (D-15)](topic-prepare-ui-gap-d15.md) · [Keystone package migration](keystone-package-migration.md) · [Keystone gap remediation](keystone-migration-gap-remediation.md)
