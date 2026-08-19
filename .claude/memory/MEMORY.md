# Memory Index — ACMP

> Compacted 2026-08-19 (8th). One line per memory; detail lives in topic files and the package.
> Read the linked file before acting. ⚠ Keep this file under ~17KB — past its read limit
> the tail is SILENTLY dropped on load, so an over-long index is worse than a short one.

## ★ 2026-08-19 · **`SL-030` MERGED** · **`SL-031` OPEN — the DW-029 AC programme is the active work**

> ⚠ **READ THE LIVE NUMBERS** — `gate_run()` + `readiness_check("package")`. Package readiness is
> deliberately whatever the current batch makes it; `acs-met` failing on exactly your batch's Pending
> ACs is the EXPECTED build-window state, not a regression.

- ⭐⭐⚠ **`gate_run()` CAN NEVER RETURN 7/7 AGAIN, and `readiness_check` is `ready:FALSE`** — both from
  `DEF-093`: `G-COMPLETE` scans text for placeholder markers in entity `title` AND progress `entry`, and
  `PE-469` — the note documenting that rule — quotes the token. The journal is **append-only**;
  `entity_upsert` refuses it and an appended `correction` does NOT clear the scan. **Never report
  "gates green"; reconcile the failure LIST** (`[PE-469]`) — an unexpected id is a real finding hiding
  behind a known one. Operator's call: raise with the tamheed maintainer. **Name the concept, never the
  token**, or you create another unfixable row.
- ⭐⭐ **`DW-029` is running as `SL-031`.** ⚠ **`prm-next.md` NO LONGER CARRIES THE TALLIES — batch 13
  deleted them and left the measuring command instead, after the file went stale an eighth time. Do not
  re-introduce a number here either; measure it.** Verdicts run `AC-115`…`AC-132`. ⚠ **THE METHOD
  MATTERS MORE THAN THE COUNT:** never leave a Pending/Partial AC (`acs-met` counts by `retired_in` and
  ignores `lifecycle_status`, so it holds readiness false FOREVER — trap 16c), so **a part-verified
  requirement gets a `DW-` row, not an AC**. The cheap candidate filter is **exhausted**: grepping tests
  for requirement ids returns zero, because every requirement a test names already has an AC.
- ⭐⭐ **Batch 13's classifying rule, worth reusing: surface that DOES NOT EXIST can be excluded by name
  in a Met verdict; surface that EXISTS but is unproven CANNOT.** That one line decided all five
  candidates. `NFR-043` names four span types — Webex/Tarseem are Phase 2 and genuinely absent, but EF
  Core and Hangfire run in production, so the missing instrumentation is a partial (`DW-062`), not a
  footnote. ⚠ **`src/Acmp.Worker` has NO OpenTelemetry package at all** — the host that runs the jobs.
- ⭐⭐⚠ **`DW-064` — the divergence was in a CODE COMMENT the whole time.** `deploy/Dockerfile.web` bakes
  `VITE_OIDC_AUTHORITY` into the SPA bundle at build time and **says so in its own words**, so promoting
  one web image across environments is impossible — exactly what `NFR-053` forbids. The same file
  templates the nginx CSP origins at container START via envsubst *and comments that this is so each
  environment needs no rebuild*. **The principle was understood and applied one layer down.** Nothing
  failed because there was no AC to fail — the programme's whole premise in one file.
- ⚠⚠ **A COUNT OF THE ENFORCING MECHANISM IS NOT A MEASURE OF THE PROPERTY** (`LL-006` again). The
  `NFR-021` census read as a **38-command server-side-validation hole** — the shape of a critical
  security finding. All four commands that actually carry scalar input are guarded **in the domain**:
  `SetTimebox` clamps 5..120, `MoveItem` bounds-checks the target index, `RecordActualMinutes` floors
  negatives at 0, `DeleteRecording` uses its string only as an EF equality predicate. **Filing off the
  census alone would have been wrong.** ⚠ Also: searching for validator *files* returns **1**; validators
  are co-located in feature files and there are **78**. Third instrument was the first honest one.
- ⭐⭐ **The programme's real yield is PARTIALS, not verdicts** — **nine** requirements built on one
  side only, each invisible *because* it had no AC to fail. The list lives in `prm-next.md` §2b; worst
  was **`NFR-025`** (`DEF-094`), a **Must** security requirement divergent on BOTH clauses.
- ⭐⭐⚠ [**`LL-007` — a green checker may have had NOTHING to check**](scan-must-prove-it-had-a-subject.md)
  Fired **seven+ times on my own instruments**: `tsc --noEmit -p tsconfig.json` in `src/Acmp.Web` **exits
  0 over ZERO files** (solution-style config), blessing 13 type errors that had failed `npm run build`
  for ten commits (`DEF-091`); a grep scoped to a directory that does not exist; a check swallowed by
  `|| true`; a scanner regex matching TypeScript `>=`; a cwd-relative gate path CI would have resolved to
  nothing; a validator-FILE search returning 1 where 78 exist. `vitest` transpiles, it does NOT
  typecheck — **use `npm run build` or `-p tsconfig.app.json`. Widen the instrument and prove it has a
  subject before believing ANY empty result.**
- ⭐⭐ **`SL-030` MERGED** (#295 → `1a52dba`). `AC-114` Met (`AV-192`); design record is `prm-next.md`
  §2 — do not re-litigate or rebuild it. ⚠ **`SC-021` is Merged but `WBS-22.3` STILL READS "notification
  bodies"**: the operator chose plain approval over the text-rewriting variant, so **the SC row + its
  `scope_modifies` edges ARE the correction**. Follow the edge; do not "tidy" that row. ⚠
  **`MoveTopicPriority` is deliberately UNFILTERED** (Chairman/Secretary-only gate; filtering would
  corrupt the renumbering) — reading it killed a false alarm.
- ⭐ **`LL-006` (a proxy is not the artifact) is Approved + pinned.** ⚠ `Timeline.tsx` and
  `Calendar.tsx` are deliberate honest SHELLS. Requirement ids in source comments are **positive-only**.
- ⭐ [**Store mechanics proven by experiment**](package-mechanics-proven-2026-08-18.md) — `acs-met`
  ignores `lifecycle_status`; `entity_upsert` preserves omitted nullable fields but requires NOT NULL
  ones; slice-scope `wbs-done` is vacuous for the 28 old slices (`DEF-087`); `G-TRACE` needs three legs.
- ⚠ **Trap 25:** `gh pr merge` can merge **remotely** then abort locally, looking like lost work —
  check `gh pr view --json state`, verify by CONTENT. A verification grep can count your own comment.

## ★ Earlier state — superseded by the section above; durable facts only

- ⚠ **`PH-3` stays `Approved` ON PURPOSE** — `WBS-20.4` is the email adapter vs a hard constraint
  (`DEC-055`). Do **not** "repair" it; that is the manufactured-status move `DEF-010` records.
- **Seven lessons are Approved + PINNED** (`LL-001`…`LL-007`) and bind every session via the auto-loaded
  note. Nothing is awaiting an operator interview.
- ⚠⚠ **`$?` AFTER A PIPE IS THE PIPE'S LAST COMMAND** — `dotnet format ... | tail; echo $?` printed 0
  while the real exit was 2. Redirect to a file and read `$?` on the bare command.
- **PRODUCTION IS DEPLOYED AND RECONCILED**; `/readyz` 200 on all four checks, `committee_members`
  1 → 27. The `RISK-007` adoption clock started 2026-08-17.
- `DEC-057`/`DEF-084`, `DEF-085`/`SC-017`, `DW-017`, `DW-031`, `DW-009` — all landed (#289–#292); detail
  in the package. The reusable part: **`DEF-084` reported eight methods as ONE finding; they were three.**

## Earlier 2026-08 — durable findings only (superseded state removed)

> Everything else was promoted into `prm-next.md` §5, now the single place traps live. Kept here: the
> findings with their own topic file, and the facts that re-frame how the register reads.

- ★★★ [**`DEF-078`: a green control can be blind**](a-green-control-can-be-blind.md) — the defect row's own prescribed command could not measure anything (`/readyz` returns 200 via the SPA fallback with the API dead); a healthcheck evaluated ZERO checks; gitleaks passed 153 commits because `.gitleaks.toml` allowlisted every markdown file.
- ★★ [**`DEF-056`'s "measured blocker" WAS NOT REAL**](an-absence-needs-a-proven-instrument.md) — rows were written all along; the helper read `AuditEvent.Action`, **NULL on the v1 rows every refusal writes**, and its two `NotContain` controls passed **VACUOUSLY**. **An absence is only evidence if the instrument is proven present.** `AV-159` was wrong the same way.
- ⚠⚠ [**v4 store + 4.4.x lesson mechanics**](tamheed-v4-and-liveness.md) — `status` → `lifecycle_status`; build payloads from the JSONL; `WVR-` operator-only; progress has a `correction` event. Approving a lesson **refuses without `operator_confirm: true`**.
- ★★ **`DW-029` re-frames every status in the package**: a requirement advances ONLY via the AC auto-advance trigger, so **requirement status measures whether anyone WROTE an AC, not whether it was built** — and `v_backlog` reports that faithfully. `DEF-012` is Won't-fix (`DEC-055`): the one mechanical rule that would have "fixed" it closes `WBS-20.4`, the **email adapter**, against a hard constraint.
- ⚠ **A defect row's predicted cause is a HYPOTHESIS, not evidence** (`DEF-067`) — its row forbade the fix that worked, and the real cause was test *duration*. Read the implementation, including when the row reads pre-checked.
- ⚠ **Read `ADR-0043`, NOT `ADR-0042`** — 0042 is Superseded: it wrongly claimed Guest is stream-bounded (E.3 bounds a guest by a TIME WINDOW). All 7 clauses carried over verbatim; steps 1–8 shipped, `AC-010` Met.
- ⚠⚠ **Stream scope had NEVER run on a real DB** (`DEF-066`) — `member_streams.StreamId` was an IDENTITY column and four suites were green over it. See [[inmemory-provider-hides-db-refusals]]. `DEF-068`'s landmine: `PermissionMatrixTests` evaluates cells with **no resource**, and a 2-param handler is never invoked without one — **a stream-scoped policy is RESOURCE-ONLY**.
- **Stale branches** (all pre-date `4c1b356`, so they carry `DEF-064`'s broken `ar.json` — merging one now fails `check-i18n` loudly): `chore/design-update-round2`, `chore/docs-v8-local-design`, `feat/budget-notification-observer`, `feat/p13-webex-integration`, `docs/defer-p14-tarseem`, `feat/audit-adr`.
- **Tamheed acceptance series `findings_13`–`findings_16` complete except §6:** the CLAUDE.md obligations note demonstrably TRANSFERS to fresh contexts; whether a fresh session DISCHARGES it is unproven. Only an **interactive** fresh session can close it.

## Shipped, reference only (detail in the package)

- **ADR-0039 `AC-090`** (#239): per-request revalidation. ⚠ **An unknown subject must be ALLOWED** —
  ADR-0004 provisions JIT, so failing closed refuses every first login. Seam `IPrincipalRevalidator`.
- **`DEF-052`: there is NO read-side role gate** — every named policy is a WRITE capability; no topic
  read path calls `IAuthorizationService` at all. Fixed by `GuestSurfaceMiddleware`, deny-by-default.
- Guest-expiry sweep (#240) hourly. ⚠ Predicate is `AccessExpiresAt != null && < now`, so an invited
  member (role `Guest`, null window) is **not** swept. **ACMP has no reschedule** (`DW-025`).

## Standing rules & gotchas (read before editing)

- [★ Read the implementation before calling it a defect](read-before-calling-it-a-defect.md) — **ten** instances; never caught by a gate. It has made defects smaller, made one **disappear**, and twice killed one in minutes. **Read the predicate, not the doc comment describing it** — and read the guard, not the count of guards.
- [★ The InMemory provider hides DB refusals](inmemory-provider-hides-db-refusals.md) — **`DEF-066`: stream assignment had NEVER worked on SQL Server**, and ADR-0043 shipped on top of it with four suites green. Always ask "has this write ever run against SQL Server?" ⚠ Only `Acmp.Integration.Tests` is real SQL Server; `Acmp.Api.Tests` and `Acmp.Application.Tests` are EF InMemory.
- [★ Controls must DETECT **and** TELL](controls-must-detect-and-tell.md) — **nine** instances; the "tell" half is normally the untested one. The `DEF-066` migration guards are the first actually **mutation-tested** rather than asserted in a comment.
- [★ Verify mechanically, not carefully](verify-mechanically-not-carefully.md) — `entity_upsert` replaces FULL rows; the JSONL flushes on EVERY write, so git HEAD is a live baseline. ⚠ **A measurement that indicts known-good code is measuring itself.** `/acmp/*` env params are **LF**. ⚠ PowerShell: always `--body-file` / `-F <file>`, never `-m` with backticks.
- ⚠ **PowerShell joins arrays with SPACES.** `aws ... --output text` returns an **array of lines**; `[IO.File]::WriteAllText(path,$array)` writes one space-joined line and would have **destroyed the SSM env file**. Use `($v -join "`n")` and verify the line count before publishing.
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
- Ladder plans `p6a-*`…`p16-*` in this directory — superseded by the package's slice rows.
- [Audit slice (AC-017)](audit-slice-literal-ac017.md) · [Topic Prepare UI (D-15)](topic-prepare-ui-gap-d15.md) · [Keystone package migration](keystone-package-migration.md) · [Keystone gap remediation](keystone-migration-gap-remediation.md)
