# Memory Index — ACMP

> One line per entry; detail lives in topic files and the package. Read the linked file before acting.
> ⚠⚠ **MEASURED CEILING: 200 LINES** (not bytes — the old byte figure was disproven and the wrong
> dimension). Past 200 is dropped **silently**, and it had already eaten the "Standing rules" section.
> ⭐ **A limit disproven in one unit is not a limit disproven.** `wc -l` before adding; keep under ~140.


## ★★★ 2026-08-29 · `SL-033` + `DW-080` BOTH DONE · **NEXT = `WBS-25.2` (`DW-079`, doc-only)**

`WBS-24.8` merged (`#323` → `24738d4b`, ten green). `FR-165` via `SC-037`/`DEC-086`; `AC-154`/`AC-155` `Met`.
⚠ **Which rows are at `Review` is NOT written here** — `readiness_check(scope="slice", id="SL-033")` only.
⚠ Live state is `prm-next.md`, never this file. `DEC-086` = the four sizing rulings (isolate / list-only
materials / audit the successful read / new FR not `FR-159`).

- ⭐⭐⭐ **WHEN AN ITEM ASKS YOU TO ADD THE THING WHOSE *ABSENCE* IS THE GUARANTEE, ISOLATE IT.** `/session`'s
  security was that no parameter could name someone else's slot. `WBS-24.8` needed exactly that parameter →
  own query (no `Guest` in `AllowedRoles`), own endpoint group **outside** the guest allowlist, own guarded
  route. Never make the shared path conditional.
- ⚠⚠⚠ **A DEFENCE LAYER CAN BE STRUCTURALLY INVISIBLE TO ANY FRONT-DOOR TEST.** I claimed adding `Guest` to
  the preview's `AllowedRoles` was *"the single mutation"*; **measured false — all 10 API tests stay GREEN**,
  because the path gate intercepts guests first, i.e. the very population that layer refuses. Only a
  unit-level `AuthorizationBehavior` test catches it. ⭐ **Pin each refusal to a DISTINGUISHABLE SIGNATURE**
  (header vs. audit row); three tests all asserting `403` test whichever layer runs first.
- ⚠⚠ **A REFACTOR CAN PUSH A FILE UNDER THE PER-FILE COVERAGE FLOOR WITH NO NEW UNTESTED LINE** — moving
  ~36 **covered** lines out of `GetMySession.cs` left the same 3 untested guards over a smaller denominator.
  **Numerator never moved.** Don't read it as "the new code is untested".
- ⛔ **RUN THE GATE, NOT THE TESTS** (`WBS-24.5`'s lesson, repeated 3 days later): `dotnet test` per project
  passed while `check-coverage.mjs` failed on 4 files. With Docker up the local gate reproduced CI **exactly**
  (466 files, 99.56%). ⚠ Every uncovered line was an **early-return guard I never forced** — I proved the
  *authorization* guards and trusted the *data* guards.
- ⚠⚠ **`DEF-115`, FOUND ONLY BY LOOKING:** `/session` rendered `10:40–10:55 · ١٥ دقيقة` — two digit systems
  on one line, because only the number path had `ar-u-nu-arab`. **Also an `INV-014` divergence** (`DEC-037`
  quotes the reference as `١٠:٤٠–١٠:٥٥`). ⭐ **`numberLocale()` is exported for ANY `Intl` formatter that
  emits digits — a DATE formatter is one.** No gate reads pixels.
- ⭐⭐ **`LL-001`'s HASH-AND-VERIFY CAUGHT A REAL CORRUPTION**: re-typing `DW-028`'s title to flip its status
  flattened **six em dashes** to hyphens and reworded a phrase. **Always hash before, verify after.**
- ⚠ **`DW-088` (new):** `TopicDetail`'s download button is hardcoded `disabled` — **no principal but a guest
  presenter can open a topic attachment anywhere in the product.** Filed BEFORE the fork it bore on reached
  the operator, so the ruling was not prejudged.
- ⚠ **THIRTY-SEVENTH stale statement:** a **lifecycle status written inline in prose** (`⚠ Review — your
  verdict`) in 3 item blocks, all since promoted. `THIRTY-FIRST`'s rule was written about *lessons* and
  nobody carried it to `WBS-` rows. **A rule written about one register is not a rule about one register.**
  ⭐ Found by grepping `prm-next.md` for **file names**, not ids — a status is not an id.
- ⭐ **Two hollow passes of my own**: a failed guest invite still deserialised into a defaulted record (so
  `NotBeNull` passed with no guest); and a mutation whose patch **silently never applied** yet reported a
  clean pass on both suites (trap 12). Re-read the file; don't trust the exit code.
- ⚠ **`AcmpWebApplicationFactory.WithIdentityProvider()`** is opt-in — without it the guest-invite path is
  unreachable and the API answers look like feature bugs.
- ⚠ Axe count **92 → 94** (+2 = one test × two browsers). **This file said 90** — re-measure, never quote.

✅ **`DW-080` PHASE B IS DONE** (`#325` → `1d7cb04b`): api+worker on `aspnet:10.0-noble-chiseled-extra`.
326→258 MB, CVEs **75→11**. The block that stood here PREDICTED THE WRONG FAILURE and the correction is
the durable part:
- ⛔⛔ **A NO-ICU BASE DOES *NOT* THROW AT STARTUP.** It starts, exits 0, silently enters invariant mode, and
  throws **only when a non-invariant culture is TOUCHED**. ACMP's API touches none (all 10 `CultureInfo` =
  `InvariantCulture`; `RequestLocalization`/`IStringLocalizer`/`.resx` = **0 of 693 files**). So plain
  chiseled looks perfectly healthy while **arming a trap**. Silent, not loud — worse than predicted.
- ⭐⭐ **THE RISK IS THE ICU *VERSION*, NOT musl.** alpine-extra ICU **78.1** vs chiseled-extra/Debian
  **74.2**; CLDR moved `ar-SA`'s calendar in between — same binary renders **Hijri vs Gregorian**, no
  exception, Arabic only. ⛔ **`-extra` is load-bearing; a digest bump changing the ICU major needs the
  Arabic render checked, not a green suite.**
- ⚠ **`LCID 1025` IS A SQL SERVER CONCERN** — EF migrations using `LANGUAGE 1025` inside `sqlserver-fts`,
  an image `NFR-054` excludes. The api base cannot affect Arabic FREETEXT.
- ⚠⚠ **A GREEN e2e DID NOT PROVE THE NEW HEALTHCHECK** (`DW-091`): both api dependents use
  `service_started`, so **nothing consumes its verdict** — `DEF-078`/`DEF-079` a 3rd time, with the compose
  comment asserting the opposite. ⭐ Probe then forced BOTH ways through Docker's plumbing.
- ⚠ **`DW-090`: no AC for `NFR-054`** — its verification names a CI check that **does not exist**. A size
  check ALONE passes on Debian at 326 MB, i.e. reports compliance with a *minimal-base* clause from the
  base it excludes. ⚠ `DW-066`'s 257 MB is stale by a runtime major.
- ⭐ **`AppContext.TryGetSwitch` REPORTS THE SWITCH, NOT THE EFFECTIVE MODE** — it said `off` on images that
  WERE invariant. Attempt a culture; never read the flag.

★★★ [**`SL-033` per-item findings**](sl033-slice-findings.md) — the earlier six items, **six different ways a
row misled**; the two bidi rules and why one does not transfer; the i18n formatter no-op; the three-place
`DbContext` registration.

- ⭐⭐ **STILL THE HABIT THAT PAYS, now on all EIGHT items:** read the row's own text, then sweep the
  NARRATIVE docs **and** the ADR/decision/OQ registers **by keyword** before sizing (`LL-008`, `LL-025`).
  `24.8` added a ninth way a row misleads: **the row was accurate and complete, and the trap was in the code
  it pointed at.**
- ⚠⚠ **`24.6`: A ROW CAN ACCURATELY QUOTE A SUPERSEDED CLAUSE AND NO REGISTER VIEW SEES IT.** ⭐⭐
  **DISCRIMINATOR: an ADR that NAMES the rows it will amend — check that list against every row quoting it.**
- ⛔ **NEVER apply `PageSize.Clamp` to an export** — on a compliance artifact it becomes silent truncation.
- ⚠⚠ **`ReadAsStringAsync` STRIPS THE BOM** — assert BYTES. ⭐ Scan a popover **with it OPEN**.
- ⚠ **Approved ACs are IMMUTABLE, including against being marked superseded** — `AC-147`'s NULL
  `superseded_by` was ACCEPTED; do not "repair" it.
- ⛔ **`SEC-080` asserts a legal hold overrides any purge and NO HOLD MECHANISM EXISTS** (`OQ-080`).
- ⛔⛔ **A RED FROM `SearchProvidersFtsTests` IS REAL — STOP, DO NOT RE-RUN** (`DEC-077` d3). ⚠
  **`readiness_check` is `ready:FALSE` ON PURPOSE** (`DEF-108`) — do NOT soften or convert it.
- ⚠⚠ **`scripts/**` is NOT path-ignored** (`DEC-077` d2) — it goes via PR, and **poll CI to completion after
  ANY direct push to `main`**. ⛔ Never propose path-ignoring it; several `check-*.mjs` **are** the gates.
- ⚠ **`DEF-109`**: `Acmp.Api.Tests` ran 20m35s / 17 failed between two normal runs. ⛔ The mitigation cannot
  be credited — the run before it was green too. Append an occurrence; don't re-run into silence.
- ⚠⚠ **CI CAUGHT A VACUOUS TEST I HAD "FIXED" LOCALLY** — replace the GLOBAL (`vi.stubGlobal`) and assert the
  OBSERVABLE. ⭐ Tuning until green is not a fix.
★★★ [**`DW-082` / Dependabot arc**](dw082-sweep-and-vitest4.md). ⚠⚠ **NEVER lower `ADR-0016`'s 95%**: v3
credited lines wrapping *uninvoked* inline handlers, so files with **no test file** scored ≥95%.
⚠ **`DEF-107`: approving+pinning a lesson does NOT make it bind** — run `handoff_emit` in the SAME batch.
⚠ **Push package writes BETWEEN merge cycles** — every push to `main` re-stales every open PR.
⭐ **Instruments to USE, not re-derive:** `scripts/coverage-triage.mjs` · `gen-lesson-docket.mjs` ·
`gen-slice-review-slate.mjs` · `count-prompt-ids.py` · `src/Acmp.Web/scripts/number-render-scan.mjs`.

## ★★ 2026-08-20 · the disposition session — durable rules only

- ⚠⚠⚠ [**AN ID IS A POINTER, NOT A REFERENCE**](an-id-is-a-pointer-not-a-reference.md) — the operator
  **refused an interview** over it. `LL-011`, pinned. Anything they read to DECIDE carries each record's
  full text inline, generated from the JSONL. ⭐ `G-IDS` checks FKs, **not ids in prose** (`DEF-101`).
- ⚠⚠ **A REQUIREMENT'S STATUS AND ITS `DW-` ROW'S STATUS ARE UNRELATED COLUMNS AND NOTHING COMPARES**
  them — activating a `DW-` row → check its requirement in the same breath. ⚠ `assumptions-current`'s
  field is a FUTURE due date; more will redden and that is the control working. ⛔ `DEF-087` untouched.
- ⚠ **I reported "four" truncated assumption titles; it was EIGHT** — measuring inside the set you are
  already holding is not measuring the register.
★★ [**Durable rules from batches 13–21**](batches-13-21-durable-rules.md) — `Met`-verdict scope, the
enforcing-mechanism trap, never leave a Pending AC, Hangfire process-globals, union coverage, `$?` after
a pipe, and production's reconciled state.

## Earlier 2026-08 — durable findings only

- ★★★ [**`DEF-078`: a green control can be blind**](a-green-control-can-be-blind.md) — a healthcheck that
  evaluated ZERO checks; gitleaks passing 153 commits over an allowlist covering every markdown file.
  ⚠ Read `ADR-0043`, **not** `ADR-0042` (Superseded).
- ★★ [**An absence needs a proven instrument**](an-absence-needs-a-proven-instrument.md) — `DEF-056`'s
  "measured blocker" was not real: the helper read a column that is NULL on the rows it counted, and its
  two `NotContain` controls passed **VACUOUSLY**.
- ⚠⚠ [**v4 store + 4.4.x mechanics**](tamheed-v4-and-liveness.md) — `status` → `lifecycle_status`; build
  payloads from the JSONL; `WVR-` operator-only; progress has a `correction` event; approving a lesson
  **refuses without `operator_confirm: true`**.
- ★★ **Requirement status measures whether anyone WROTE an AC, not whether it was built** — a requirement
  advances only via the AC auto-advance trigger. `DEF-012` is Won't-fix (`DEC-055`).
- ⚠⚠ **Stream scope had NEVER run on a real DB** (`DEF-066`) — see
  [[inmemory-provider-hides-db-refusals]]. `DEF-068`'s landmine: **a stream-scoped policy is RESOURCE-ONLY**.
- **6 stale branches still exist** (verified 2026-08-21, `git branch -a`), all pre-dating `4c1b356` so
  **all carry `DEF-064`'s broken `ar.json`**. ⚠ Merged `feat/`/`fix/` branches also linger on `origin`
  against the "delete branch" half of the branching rule.

## Shipped, reference only (detail in the package)

- **ADR-0039 `AC-090`** (#239) per-request revalidation — ⚠ **an unknown subject must be ALLOWED** (ADR-0004 provisions JIT, so failing closed refuses every first login).
- **`DEF-052`: there is NO read-side role gate** — every named policy is a WRITE capability; fixed by `GuestSurfaceMiddleware`, deny-by-default. ⚠ The hourly guest-expiry sweep skips an **invited** member (role `Guest`, null window).

## Standing rules & gotchas (read before editing)

- [★ Read the implementation before calling it a defect](read-before-calling-it-a-defect.md) — **ten** instances, never caught by a gate. **Read the predicate, not the doc comment describing it**; read the guard, not the count of guards.
- [★ The InMemory provider hides DB refusals](inmemory-provider-hides-db-refusals.md) — `DEF-066`: stream assignment had **NEVER** worked on SQL Server under four green suites. Always ask "has this write ever run against SQL Server?" ⚠ Only `Acmp.Integration.Tests` is real SQL Server.
- [★ Controls must DETECT **and** TELL](controls-must-detect-and-tell.md) — **nine** instances; the "tell" half is normally the untested one.
- [★ Verify mechanically, not carefully](verify-mechanically-not-carefully.md) — `entity_upsert` replaces FULL rows; the JSONL flushes on EVERY write, so git HEAD is a live baseline. ⚠ **A measurement that indicts known-good code is measuring itself.** ⚠ PowerShell: always `--body-file` / `-F <file>`, never `-m` with backticks.
- ⚠ **PowerShell joins arrays with SPACES** — `[IO.File]::WriteAllText(path,$array)` writes one space-joined line and nearly **destroyed the SSM env file**. Join explicitly and verify the line count.
- ⚠ **`open_question.lifecycle_status` is a CHECK** over `Draft/Proposed/Approved/Rejected/Deferred/Implemented/Superseded/Obsolete` — "Resolved" rolls the whole batch back. `defect.fixed_by` is a **FK**; PR refs go in `custom_attributes`.
- ⚠ **Env one-offs:** the keycloak container's `docker exec` shell has no `KC_BOOTSTRAP_ADMIN_PASSWORD` (read `/run/secrets/kc_bootstrap_admin_password`); Windows `python3` cannot see Git Bash's `/tmp`.
- [⚠ Baselines are numbers, not properties](baselines-as-numbers-not-properties.md) — a count-based test on a shared topic can never discriminate.
- [⚠ Immutable history → cleanup is asymmetric](immutable-history-cleanup-asymmetry.md) — deleting a Keycloak user ORPHANS its member rows forever. **Disable, never delete.**
- [A static file cannot configure a live realm](a-static-file-cannot-configure-a-live-realm.md) — `realm-export.json` reaches **fresh stacks only**; `reconcile.sh` is the only seam to prod/UAT.
- [Write the handoff LAST](write-the-handoff-last.md) — it found `DEF-053`/`DEF-054` last time. Stamp superseded files with ⛔ immediately.
- [Commit package writes before git ops](commit-package-writes-before-git-ops.md) · [Tamheed stale .lock + PID reuse](tamheed-stale-lock-pid-reuse.md) · [Tamheed data repair](tamheed-data-repair.md) · [migration history](tamheed-migration-reverted.md)
- [Localhost CI hides load races](localhost-ci-hides-load-races.md) · [Git push hang → `gh auth setup-git`](git-push-hang-fix.md) · [Run CI gates locally pre-push](ci-gates-run-locally-pre-push.md) · [Always stage .claude/memory in commits](always-stage-claude-memory-in-commits.md)
- [Coverage & E2E mandate](coverage-and-e2e-mandate.md) — ≥95% FE+BE + adversarial E2E. ⚠ Playwright is **NOT UAT-only** (7 services + real Keycloak per PR) **but runs `KEYCLOAK_ADMIN_ENABLED=false`**, so it never touches the ADR-0038 write path.
- [E2E local run (non-destructive)](e2e-local-run-nondestructive.md) — **`-p acmpe2e` ONLY**, never `npm run e2e:up`. · [Dev-stack rebuild pitfall](dev-stack-rebuild-pitfall.md) — **never `up --build`** the long-lived dev stack.
- [Exact design fidelity + visual loop](exact-design-fidelity-visual-loop.md) · [A green suite is not a look](a-green-suite-is-not-a-look.md) — ⚠ the throwaway harness must import **only** the stylesheets the real route imports.
- [Design: breadcrumb spacing](breadcrumb-spacing-rule.md) · [i18n parity ≠ completeness](i18n-parity-not-completeness.md) · [Web visual-verify cache busting](web-visual-verify-cache-busting.md)
- ⚠ **`.adm-detail-card` has no padding and clips its children** — anything opening a popover needs `.adm-card-overflow`. · **`userEvent.setup()` installs its own clipboard stub** — define a clipboard spy *after* it.
- [User prefers simple English](user-prefers-simple-english.md) · [Phase prompt Standard Footer](phase-prompt-standard-footer.md) · [Install the schedule, not just the daemon](install-the-schedule-not-just-the-daemon.md) · [Arabic rename is a grammar rule](arabic-rename-grammar-not-substitution.md) · [A clean scan must prove it had a subject](scan-must-prove-it-had-a-subject.md) · [Guard the property, not the value](guard-the-property-not-the-value.md) · [The suite assumed a fresh database](e2e-assumes-a-fresh-database.md) · [The feature is often already half-built](check-before-building.md)
- ⚠ **AC id cells in markdown tables must stay BARE** (`| AC-001 |`, never bolded) — bolding breaks the Keystone G-PROGRESS gate.
- ⚠ **A new advisory can turn `main` red with no code change** — `GHSA-q939-rpr3-3284` (SSH.NET) blocked every merge mid-session. "It's only tests" is how a blocking gate becomes advisory.
- ⚠ **A compose `secrets:` entry whose FILE IS MISSING fails the WHOLE stack** — any mounted secret must be written **unconditionally** by `gen-secrets`.

## ⚠ Topic files this index does NOT link — an unlinked file is invisible to recall

The ladder files below are covered by the blanket note in the last section. **These eleven are not, and
nothing points at them**: `absence-claims-need-untruncated-search` · `ask-every-time-never-bank-answers` ·
`audit-slice-literal-ac017` · `body-assertions-miss-the-envelope` · `package-mechanics-proven-2026-08-18` ·
`reconciliation-and-voting-eligibility` · `substring-checks-bind-to-prose` · `topic-prepare-ui-gap-d15` ·
`wbs233-csp-spike` · `wbs234-reclassify` · `webex-coverage-gate-async-exclusion`.
⚠ **Their current value is NOT assessed here** — this line exists so they are findable, not to vouch for
them. Found 2026-08-26 by checking every topic file for an inbound link; do that after any compaction.

## Completed ladder P1–P19 + PH-5 (reference only — do not re-open)

Detail lives in this directory's topic files (`ph5-*`, `p17a-*`, `p18-*`, `p19-*`, `keystone-*`, the `p6a-*`…`p16-*` ladder plans) — all superseded by the package's slice rows. `ls` the directory when you need one.
