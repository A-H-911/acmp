# Memory Index — ACMP

> Compacted 2026-08-20 (9th). One line per memory; detail lives in topic files and the package.
> Read the linked file before acting. ⚠ Keep this file under ~17KB — past its read limit
> the tail is SILENTLY dropped on load, so an over-long index is worse than a short one.

## ★ 2026-08-20 · **batch 14 done** · **`SL-031` still OPEN — the DW-029 AC programme is the active work**

> ⚠ **READ THE LIVE NUMBERS.** `gate_run()` 7/7 and `readiness ready:TRUE` after batch 14; `defects-minor`
> is down to **`DEF-087` alone**. Never quote a tally from here or from `prm-next.md` — measure it.

- ⭐⭐⚠ **AN ID-ONLY REGISTER SWEEP IS A SCAN WITH NO SUBJECT.** `LL-005`/trap 32 say sweep `adrs`,
  `decisions`, `open_questions` before disposing of anything. Batch 14 did — **by id** — and got **ZERO hits
  for six of seven candidates**. The **keyword** sweep then found `ADR-0035` (Approved, ratified) replacing
  self-hosted MinIO with S3, which `NFR-027` still mandated by name (`DEF-098`). **Neither row names the
  other's id**, so no id sweep could ever have found it. Fixed by `SC-025`; `AC-135` Met.
- ⭐⭐ **TWO INSTRUMENTS AGREEING IS ONE INSTRUMENT when they share a mechanism.** Two scanners for
  notification leaks returned the identical 13 sites / 7 files and were both blind to the same two builders
  (C# **target-typed `new(`** — the type name never appears at the call site). Full write-up in
  [[scan-must-prove-it-had-a-subject]]. Companion: a census reporting **zero `AlterColumn` across 47
  migrations** is *implausible*, not clean — widening found a missed `DropIndex`.
- ⭐⭐ **A DERIVATION IS A COMPUTATION, NOT A FACT YOU CAN UPDATE.** I re-derived the candidate list on
  arrival (correct, 15), then produced the post-batch list by **subtracting my own four** from it — and got it
  wrong, carrying `NFR-061` that `DW-067` already removes. Re-running the rule gives **10**. Caught in-session;
  the account is written into `prm-next.md` §2b in place. **Run the three steps; never adjust the last answer.**
- ⚠ **`NFR-037` NEARLY GOT A `Met` VERDICT ON TWO THIRDS OF ITSELF.** 31 locale-aware date call sites, zero
  `toLocaleDateString`, no bare ISO string — genuinely convincing. The SPA has **exactly two**
  `Intl.NumberFormat` sites, and the requirement says *"date, time, **and number**"*. → `DW-068`.
  **Count the requirement's clauses before you count your findings.**
- ⚠ **A COMMENT-ONLY `.csproj` EDIT BROKE THE BUILD** — XML forbids `--` inside a comment; MSBuild rejected
  the project file (`MSB4025`) in 0.05 s, before compiling anything. No test suite could have seen it.
- ⚠ **`DEF-097`: TWO TEST CLASSES REGISTERING A PROCESS-GLOBAL `ActivityListener`** on one source, each
  asserting `ContainSingle()`, recorded each other's spans — **1 failure in 8 full-suite runs**, the worst
  frequency a flake can have. Fixed by serializing both into the existing `DisableParallelization` collection,
  and **proven by FORCING the overlap** (400 ms held listeners): **5 fail without, 1133 pass with**.
  A green suite over a timing flake is worth nothing.
- ⚠ **`entity_upsert` NOT NULL columns, learned by experiment:** `defects.title`, `defects.severity`,
  `scope_changes.decision_ref`, `scope_changes.description` are all required on UPDATE. Everything nullable
  (`custom_attributes`, `found_in`) is **preserved by omission** — re-confirmed on six rows this batch.
  **Hash the pre-image and re-check after** (`LL-001`); all six round-tripped byte-identical.
- **Next up: `NFR-039`** — the last code-verifiable Must NFR a reader can close. `README.md` §G (line 167) is
  the glossary. ⚠ **Read `ar.json` before concluding its stakeholder-review clause makes it a partial** —
  that inference without the read is `LL-006` exactly.

## ★ 2026-08-19/20 · batch 13 — compacted, durable findings only

- ✅⚠ **`DEF-093` FIXED upstream (tamheed 4.4.2) — `gate_run()` 7/7 and `readiness ready:TRUE` are the NORM.**
  The old "never report gates green" rule is **RETIRED** (operator, 2026-08-20): a red gate is a REAL finding.
  ⚠⚠ **The token rule changed in ONE direction only:** journal text (`progress_entries.entry`,
  `audit_verdicts.evidence`) is **exempt**; **every live ENTITY row is still screened** (`title`/`statement`/
  `description`) — there, name the concept or backtick the token. Failures now NAME the matched token.
- ⭐⭐⚠ **AN EXEMPTION THAT GREENS A GATE CAN ALSO BLIND IT** (`LL-007`). Injecting a marker into a LIVE
  entity title made `G-COMPLETE` fail and name it; the backticked form passed; the row was restored
  byte-identically. **Three tool calls, and the only thing separating "green because fixed" from
  "green because blind".**
- ⭐⭐⚠ **`LL-005` MUST SWEEP `adrs`, `decisions` AND `open_questions` — NOT JUST `requirements`.** Batch 13
  swept requirements thoroughly, then offered a build for `NFR-053`. **`ADR-0037` (Approved, ratified) decides
  that exact thing**, deferring the fix under `OQ-061`. One file-read — a bare ADR id in a docker-compose
  COMMENT — separated that from reversing an Approved ADR. Resolved by `SC-023`; nothing built.
  (Batch 14 then showed the **id-only** version of this sweep is itself blind — see the section above.)
- ⭐⭐ **Classifying rule worth reusing: surface that DOES NOT EXIST can be excluded BY NAME in a `Met`
  verdict; surface that EXISTS but is unproven CANNOT.** Decided all five batch-13 candidates, and both of
  batch 14's exclusions (`NFR-027` transcripts, `NFR-026` AI extraction).
- ⚠⚠ **A COUNT OF THE ENFORCING MECHANISM IS NOT A MEASURE OF THE PROPERTY** (`LL-006`) — the `NFR-021`
  census read as a 38-command validation hole; all four commands carrying scalar input are guarded **in the
  domain**. Searching for validator *FILES* returns **1**; there are **78**. ⚠ A defect row's predicted cause
  is a HYPOTHESIS (`DEF-067`) — its row forbade the fix that worked.
- ⚠⚠ **HANGFIRE'S `JobStorage.Current` + `GlobalJobFilters` ARE PROCESS-GLOBAL**; a second
  `BackgroundJobServer` in one process does not reliably pick up work. ⚠ Hangfire never hands a filter the
  job's own exception — it wraps it in `JobPerformanceException` with a fixed message, so record
  `InnerException`. (Batch 14 found the **listener** is global too — `DEF-097`.)
- ⚠ **`DEF-096`: `NFR-054`'s 500 MB cap is UNSATISFIABLE.** web 51 MB, worker 245 MB, api 257 MB,
  **sqlserver-fts 3.62 GB**. `SC-024` narrowed the SIZE clause; the operator **REJECTED** relaxing the
  minimal-base clause, so that half is **`DW-066`**. **Do not change a base image to close it.**
- ⭐⭐ **The programme's real yield is PARTIALS, not verdicts** — **twelve** requirements built on one side
  only, each invisible *because* it had no AC to fail. List in `prm-next.md` §2b; worst was **`NFR-025`**
  (`DEF-094`), a **Must** security requirement divergent on BOTH clauses.
- ⚠ **THE METHOD MATTERS MORE THAN THE COUNT:** never leave a Pending/Partial AC (`acs-met` counts by
  `retired_in` and ignores `lifecycle_status`, so it holds readiness false FOREVER — trap 16c), so **a
  part-verified requirement gets a `DW-` row, not an AC**.
- ⚠ **`tsc --noEmit -p tsconfig.json` in `src/Acmp.Web` EXITS 0 OVER ZERO FILES** (solution-style config) and
  blessed 13 type errors for ten commits (`DEF-091`). `vitest` transpiles, it does NOT typecheck — **use
  `npm run build` or `-p tsconfig.app.json`.**
- ⭐ **`SL-030` MERGED** (#295 → `1a52dba`); design record is `prm-next.md` §2. ⚠ `SC-021` is Merged but
  `WBS-22.3` still reads "notification bodies" — the SC row IS the correction; do not "tidy" it.
  ⚠ `MoveTopicPriority` is deliberately UNFILTERED. ⚠ `Timeline.tsx`/`Calendar.tsx` are honest SHELLS, and
  requirement ids in source comments are **positive-only** evidence.
- ⭐ [**Store mechanics proven by experiment**](package-mechanics-proven-2026-08-18.md) — `acs-met` ignores
  `lifecycle_status`; `entity_upsert` preserves omitted nullable fields but requires NOT NULL ones;
  slice-scope `wbs-done` is vacuous for the 28 old slices (`DEF-087`); `G-TRACE` needs three legs.

## ★ Earlier state — durable facts only

- ⚠ **`PH-3` stays `Approved` ON PURPOSE** — `WBS-20.4` is the email adapter vs a hard constraint
  (`DEC-055`). Do **not** "repair" it; that is the manufactured-status move `DEF-010` records.
- **Seven lessons are Approved + PINNED** (`LL-001`…`LL-007`) and bind every session via the auto-loaded
  note. Nothing is awaiting an operator interview.
- ⚠⚠ **`$?` AFTER A PIPE IS THE PIPE'S LAST COMMAND** — redirect to a file and read `$?` on the bare command.
- **PRODUCTION IS DEPLOYED AND RECONCILED**; `/readyz` 200 on all four checks. `RISK-007` adoption clock
  started 2026-08-17.

## Earlier 2026-08 — durable findings only

> Traps live in `prm-next.md` §5. Kept here: findings with their own topic file, and facts that
> re-frame how the register reads.

- ★★★ [**`DEF-078`: a green control can be blind**](a-green-control-can-be-blind.md) — a healthcheck that
  evaluated ZERO checks; gitleaks passing 153 commits over an allowlist covering every markdown file.
  ⚠ Read `ADR-0043`, **not** `ADR-0042` (Superseded — it wrongly claimed Guest is stream-bounded).
- ★★ [**`DEF-056`'s "measured blocker" WAS NOT REAL**](an-absence-needs-a-proven-instrument.md) — the helper
  read `AuditEvent.Action`, **NULL on the v1 rows**, and its two `NotContain` controls passed **VACUOUSLY**.
  **An absence is only evidence if the instrument is proven present.**
- ⚠⚠ [**v4 store + 4.4.x lesson mechanics**](tamheed-v4-and-liveness.md) — `status` → `lifecycle_status`;
  build payloads from the JSONL; `WVR-` operator-only; progress has a `correction` event. Approving a lesson
  **refuses without `operator_confirm: true`**.
- ★★ **`DW-029` re-frames every status in the package**: a requirement advances ONLY via the AC auto-advance
  trigger, so **requirement status measures whether anyone WROTE an AC, not whether it was built**. `DEF-012`
  is Won't-fix (`DEC-055`): the one mechanical rule that would "fix" `v_backlog` closes `WBS-20.4`, the
  **email adapter**, against a hard constraint.
- ⚠⚠ **Stream scope had NEVER run on a real DB** (`DEF-066`) — see
  [[inmemory-provider-hides-db-refusals]]. `DEF-068`'s landmine: **a stream-scoped policy is RESOURCE-ONLY**.
- **Stale branches** (all pre-date `4c1b356`, so they carry `DEF-064`'s broken `ar.json`):
  `chore/design-update-round2`, `chore/docs-v8-local-design`, `feat/budget-notification-observer`,
  `feat/p13-webex-integration`, `docs/defer-p14-tarseem`, `feat/audit-adr`.

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
- ⚠ **Env one-offs:** the keycloak container's `docker exec` shell has no `KC_BOOTSTRAP_ADMIN_PASSWORD` (read `/run/secrets/kc_bootstrap_admin_password`); Windows `python3` cannot see Git Bash's `/tmp`.
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
