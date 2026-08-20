# Memory Index — ACMP

> Compacted 2026-08-20 (9th). One line per memory; detail lives in topic files and the package.
> Read the linked file before acting. ⚠ Keep this file under ~17KB — past its read limit
> the tail is SILENTLY dropped on load, so an over-long index is worse than a short one.

## ★ 2026-08-20 · **batches 14–18** · `gate` 7/7, `readiness` `ready:TRUE`

- ⭐⭐ **`DEF-099` FIXED (#300 → `1da0e05`) — and it had TWO disjoint causes, the second visible only while
  fixing the first.** api: bare endpoint + gRPC default → 404. **worker: NO `OTEL_` vars AT ALL**, so its
  exporter fell back to `http://localhost:4317`; its own comment claimed the endpoint came from "the same
  `OTEL_*` env vars the API reads" — **compose env is PER-SERVICE and inherits nothing.** `NFR-028` then
  closed (`AC-138`), having been **deliberately held back** in batch 17: Met "no PII in traces" while no
  trace arrived would have been **true for the wrong reason.** ⚠ `DW-065` is still `Open` and its **title is
  now wrong** ("never been observed") — fix the title, keep the status.
- ⚠⚠ **The original finding: OTLP traces had NEVER reached Seq in any environment.** Both compose files set the bare
  `/ingest/otlp` endpoint and **`OTEL_EXPORTER_OTLP_PROTOCOL` is set NOWHERE**; the .NET exporter defaults
  to **gRPC**, which posts the endpoint verbatim. Live: `POST /ingest/otlp` → **404**,
  `POST /ingest/otlp/v1/traces` → **200**. The SDK swallows export failures silently.
- ⭐⭐ **THE DISCRIMINATOR IS THE REUSABLE PART.** Under shipped config, 8 `/readyz` calls grew Seq's events
  **679 → 739** while DB spans stayed at **exactly 72**, newest timestamp frozen. Same host, port and
  container — **log path delivers, trace path does not.** A count alone proves nothing. **Verify any fix by
  sending traffic and asserting spans MOVE, never by a clean boot.** This is also the real explanation of
  `DW-065`: not "nobody looked" — **nothing ever arrived**. `AC-133` rests on spans that never land.
- ⭐ **`NFR-028` evidence, now Met:** every SQL literal arrives as `?` and the decisive case is **not a
  parameter** — the healthcheck's `SELECT 1` arrives as `SELECT ?`, so SqlClient sanitizes **literals**.
  Log half: **24** logging sites in all of `src` (680 files), none naming a person, email, vote or content.
  ⚠ The masking enricher matches by property NAME and covers **email but not names or vote content** — two
  of three hold by CONVENTION, so the census is load-bearing and the enricher is only defence in depth.
- ⚠ **HOW TO RUN A STACK HERE — copy this exactly.** `docker ps` empty but **5 populated volumes** exist and
  `dev-up.sh` is `up -d --build`, the documented breaker. Use an **isolated project on FRESH volumes**, same
  compose + env file so the config under test is the shipped one; **tag an existing CI image** to the name
  compose expects to skip the 3.62 GB FTS build; bring up `sqlserver`+`seq` ALONE and confirm healthy first;
  tear down `down -v` and **remove the tag** so a later `up` cannot reuse a stale image.
- ⭐ **`NFR-034` Met (`AC-137`) — the "browser batch" closed WITHOUT a browser.** ⚠ **`axe-core` was already
  a dependency** with three uncatalogued a11y artifacts including a **live Playwright sweep in both locales**.
  What separates the WCAG four is **what each instrument can DO**: axe **never presses a key** (`DW-070`);
  it passes on **3 routes of 52** (`DW-071`); the contrast gate covers **one of two thresholds and none of
  four states** (`DW-072`). **Check what exists before believing a "needs a browser" label.**
- ⭐⭐ **`NFR-039` → `DW-069`, now `LL-010` (pinned): when a requirement says X is the single source for Y,
  CHECK X EXISTS BEFORE MEASURING Y.** The glossary is a **circular pointer between two English documents**
  — clause two is *undecidable*, not unverified. ⚠ 76 AR divergences, but **Arabic adjectives agree in
  gender**: 55 morphological (correct), 21 lexical, and even those are candidates.
- ⭐⭐ **`LL-008` (pinned): sweep registers by KEYWORD, not just id.** Batch 14: **0 id hits for 6 of 7**;
  the keyword sweep found `ADR-0035` (ratified) replacing the MinIO `NFR-027` still mandated. Batch 15:
  **1 id hit vs 24 keyword hits**, three of the extras decided the batch.
- ⭐⭐ **`LL-009` (pinned): two instruments agreeing is ONE instrument when they share a mechanism.** Two
  scanners, identical 13/7, both blind to C# **target-typed `new(`**. See [[scan-must-prove-it-had-a-subject]].
  Fired again in batch 17: I read Seq for **flat dotted** property names and got zero — Seq nests them
  (`db`→`query`→`text`), so "no spans" was my reader, not the stack.
- ⚠⚠ **A BROKEN MEASUREMENT PRINTED A CONFIDENT WRONG VERDICT.** An inline `python -c` inside `$(...)`
  fails silently on this shell — both sides returned empty, compared equal, and printed "shipped config does
  NOT export" before any evidence existed. **Trap 2.** Use a script FILE that prints count, max and the wall
  clock together so a silent failure cannot look like a result.
- ⚠ **A comment-only `.csproj` edit BROKE THE BUILD** (XML forbids `--` in a comment; `MSB4025`, 0.05 s).
  ⚠ **`DEF-097`:** two classes registering a **process-global** `ActivityListener` recorded each other's
  spans — 1 in 8 runs; fixed by serializing both, **proven by FORCING the overlap** (5 fail without, 1133
  with). ⚠ **`entity_upsert` NOT NULL on UPDATE:** `defects.title/severity`,
  `scope_changes.decision_ref/description`; nullable fields preserved by omission; **approving a lesson is
  NOT an edit** — content must come back byte-identical.
- ⭐⭐ **A DERIVATION IS A COMPUTATION, NOT A FACT YOU CAN UPDATE** — and **an id-and-status verifier cannot
  see a stale INSTRUCTION or a PROSE NUMBER.** All 124 ids verified live while the file still carried wrong
  counts. **Read the prose; the mechanical pass is the easy half.**

## ★ batches 14+15 — detail folded into the head above; only what is not repeated there

- ⚠ **THE READER-CLOSABLE PHASE OF `DW-029` IS EXHAUSTED.** The rule now yields **NINE**
  (`NFR-018 019 023 028 031 032 033 034 038`) and every one needs a stack, a browser, a scanner, an
  unconfirmed org policy or an undeferred `P14`. After batch 16 and 17, `NFR-034` is Met and `NFR-031`/`032`/
  `033` have `DW-` rows, so **re-run the rule rather than trusting this sentence.** `SL-031` stays `Approved`.
- ⚠ **`NFR-037` nearly got `Met` on two-thirds of itself** (→ `DW-068`): 31 locale-aware date sites, but the
  SPA has **exactly two** `Intl.NumberFormat` sites and the requirement says *"date, time, **and number**"*.
  **Count the requirement's clauses before you count your findings.**
- **Next: a SCANNER batch** (`NFR-018` DAST + pentest, `NFR-019` TLS scan) and the remaining stack items —
  `NFR-052` single-command startup, the ops group (`NFR-015 017 044 052 062`, `PE-485`), and several of the
  `DW-043`…`DW-060` performance rows measured from trace data, **which needs `DEF-099` fixed first.**
  `DW-066` (alpine/distroless) is operator-only and its **risk is musl, not the two `FROM` lines**.

## ★ batch 13 — compacted; full record in `prm-next.md` §1

- ✅⚠ **`DEF-093` fixed upstream (4.4.2) — the old "never report gates green" rule is RETIRED.** ⚠⚠ The token
  rule changed in ONE direction: **journal text is exempt; every live ENTITY row is still screened**
  (`title`/`statement`/`description`) — there, name the concept or backtick the token.
- ⭐⭐ **Classifying rule, used every batch since: surface that DOES NOT EXIST can be excluded BY NAME in a
  `Met` verdict; surface that EXISTS but is unproven CANNOT.**
- ⚠⚠ **A COUNT OF THE ENFORCING MECHANISM IS NOT A MEASURE OF THE PROPERTY** (`LL-006`) — the `NFR-021`
  census read as a 38-command validation hole; all four commands carrying scalar input are guarded **in the
  domain**. Validator *FILES* returns **1**; there are **78**. ⚠ A defect row's predicted cause is a
  HYPOTHESIS (`DEF-067`).
- ⚠ **THE METHOD: never leave a Pending/Partial AC** — `acs-met` counts by `retired_in` and ignores
  `lifecycle_status`. **A part-verified requirement gets a `DW-` row.**
- ⚠ **`tsc --noEmit -p tsconfig.json` in `src/Acmp.Web` EXITS 0 OVER ZERO FILES** (`DEF-091`); `vitest` does
  NOT typecheck — use `npm run build` or `-p tsconfig.app.json`.
- ⚠ **`DEF-096`: `NFR-054`'s 500 MB cap is UNSATISFIABLE** (sqlserver-fts **3.62 GB**). `SC-024` narrowed the
  SIZE clause; the operator **REJECTED** relaxing minimal-base → **`DW-066`**. **Do not change a base image.**
- ⚠⚠ **Hangfire's `JobStorage.Current` + `GlobalJobFilters` are PROCESS-GLOBAL**; it never hands a filter the
  job's own exception — record `InnerException`.
- ⚠ **Coverage must be UNIONED, never summed.** ⚠ **Trap 25:** `gh pr merge` can merge remotely then abort
  locally — verify by CONTENT. ⚠ **Trap 38:** never push to a branch with CI in flight.
- ⭐ **`SL-030` MERGED** (#295 → `1a52dba`). ⚠ `SC-021` is Merged but `WBS-22.3` still reads "notification
  bodies" — the SC row IS the correction. ⚠ `MoveTopicPriority` is deliberately UNFILTERED.
  ⚠ `Timeline.tsx`/`Calendar.tsx` are honest SHELLS.
- ⭐ [**Store mechanics proven by experiment**](package-mechanics-proven-2026-08-18.md) — slice-scope
  `wbs-done` is vacuous for the 28 old slices (`DEF-087`); `G-TRACE` needs three legs.

## ★ Earlier state — durable facts only

- ⚠ **`PH-3` stays `Approved` ON PURPOSE** — `WBS-20.4` is the email adapter vs a hard constraint
  (`DEC-055`). Do **not** "repair" it; that is the manufactured-status move `DEF-010` records.
- **Nine lessons are Approved + PINNED** (`LL-001`…`LL-009`) and bind every session via the auto-loaded
  note. Nothing is awaiting an operator interview.
- ⚠⚠ **`$?` AFTER A PIPE IS THE PIPE'S LAST COMMAND** — redirect to a file and read `$?` on the bare command.
- **PRODUCTION IS DEPLOYED AND RECONCILED**; `/readyz` 200 on all four checks. `RISK-007` adoption clock
  started 2026-08-17.

## Earlier 2026-08 — durable findings only

- ★★★ [**`DEF-078`: a green control can be blind**](a-green-control-can-be-blind.md) — a healthcheck that
  evaluated ZERO checks; gitleaks passing 153 commits over an allowlist covering every markdown file.
  ⚠ Read `ADR-0043`, **not** `ADR-0042` (Superseded).
- ★★ [**`DEF-056`'s "measured blocker" WAS NOT REAL**](an-absence-needs-a-proven-instrument.md) — the helper
  read a column that is NULL on the rows it was counting, and its two `NotContain` controls passed
  **VACUOUSLY**. **An absence is only evidence if the instrument is proven present.**
- ⚠⚠ [**v4 store + 4.4.x mechanics**](tamheed-v4-and-liveness.md) — `status` → `lifecycle_status`; build
  payloads from the JSONL; `WVR-` operator-only; progress has a `correction` event; approving a lesson
  **refuses without `operator_confirm: true`**.
- ★★ **`DW-029` re-frames every status in the package**: a requirement advances ONLY via the AC
  auto-advance trigger, so **requirement status measures whether anyone WROTE an AC, not whether it was
  built**. `DEF-012` is Won't-fix (`DEC-055`) — the one mechanical rule that would "fix" `v_backlog` closes
  `WBS-20.4`, the **email adapter**, against a hard constraint.
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

Topic files in this directory carry the detail: `ph5-sl025-uat-live`, `ph5-aws-deployment`,
`p19-release-readiness`, `p18-deployment`, `p17a-test-hygiene`, `next-p17-p18-p19`,
`audit-slice-literal-ac017`, `topic-prepare-ui-gap-d15`, `keystone-package-migration`,
`keystone-migration-gap-remediation`, and the `p6a-*`…`p16-*` ladder plans (superseded by the
package's slice rows).
