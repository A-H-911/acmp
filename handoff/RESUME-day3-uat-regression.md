# RESUME — Day 3, then the UAT regression, then prod

**Written 2026-08-11 at the end of the session that landed Days 1–2.** Every fact below was
verified immediately before writing — `git`, `gh`, `aws`, and the package — not carried forward.

---

## 0. Orient first (2 minutes, do not skip)

```
server_info() · package_open("tamheed-package") · gate_run()
```

The approved plan is `~/.claude/plans/the-3-of-the-abstract-ember.md`. It contains the 9-day
ordering, the delivery pipeline, and two rounds of adversarial review of itself. **Read it, but
read §6 of this file first** — several of the plan's own claims were falsified during execution
and the plan text still carries the originals.

---

## 1. State of the world — measured 2026-08-11

| | |
|---|---|
| `main` | **`b340e62`**, working tree clean, **0 open non-dependabot PRs** |
| Landed | **#222–#230** (Days 1–2) and **#232** (Day 3, `e403e18`) |
| **Production** | `i-04d9717feea79204b` · **running** · https://acmp.anas7ammo.dev |
| ⚠ **Prod is pinned to `1c7f2ba`** | **None of Days 1–2 is deployed.** SSM `/acmp/prod/env` still pins `ACMP_IMAGE_TAG=1c7f2ba…`. Everything merged since is invisible to the 26 users. |
| **UAT** | `i-07ac28ac2fedab921` · **stopped** (stop-when-idle, by decision) |
| Budget | spend **$2.097** of $100. The 2.3% trigger is **ACTUAL**, not forecast, so it fires on realised spend — **days away, not hours** |
| Defects | 44 raised; **open: DEF-012, DEF-036, DEF-038, DEF-039, DEF-041** (unchanged — Day 3's three were raised *and* closed) |
| Package | `gate_run()` 7/7 · latest entries `PE-232`/`PE-233` |

---

## 2. The agreed sequence — where we are

```
[x] 1. merge everything            #222-#227 merged; #221 recreated as #227
[x] 2. package record on main      PE-229/230/231, DEF-036..041
[x] 3. DAY 3                       #232 (e403e18) + PE-232/233, DEF-042/043/044
[ ] 4. start UAT, deploy b340e62   <-- YOU ARE HERE
[ ] 5. smoke.sh + E2E on UAT       the deployed-topology regression
[ ] 6. prod deploy, only if 5 is green
```

⚠ **Step 4 pins `b340e62`, not the `7ffa490` this file originally said.** Confirm CI's **publish**
job ran for whichever sha you pin — a PR run shows `publish  skipping`, so the images come from the
**main-push** run. `deploy/runbooks/cloud-operations.md` §2 step 1 is the ECR proof to run first.

**Do not skip to 6.** The operator explicitly asked for a full regression on UAT before prod.

---

## 3. ✅ DONE 2026-08-11 — Day 3 (#232, `e403e18`)

Shipped as **`deploy/runbooks/cloud-operations.md`** — *not* `operations.md`: `runbooks/README.md`
is already titled "ACMP Operations Runbook" and is **on-prem**, and two files named "operations" for
different topologies is precisely how the `promote.sh` defect below happened. The `cloud-*.md` glob
then covers it with no special case.

The guard is **`scripts/check-runbook-drift.mjs`**, wired into the CI `deploy` job. It was written
**first and run red (7 findings) before any fix**, so the sites were found mechanically rather than
by eye — and it found **five** stale budget sites where this file predicted four.

Three things reading caught that a numbers-only fix would have shipped broken:

1. **The raise invalidated the *reasoning*, not just the figures.** `cloud-provisioning.md` rule 1
   said *"never leave two instances running"* — which now contradicts the design, since prod is
   always-on and UAT starts for a session. The real constraint is two **always-on** boxes (~$84/mo,
   above the 80% notification). Rule 4's *"over half the budget"* is under a third of $100.
2. **`02-s3.sh:35`** was not on anyone's list: stale `$45` *and* a claimed "120%" budget action that
   does not exist — both are at 100% ACTUAL.
3. **The stack self-starts**, verified *before* writing the start procedure (docker enabled at boot,
   `restart: unless-stopped`, ECR credential helper rather than a 12h token). Had that been false, a
   "start the box and wait" runbook would have reproduced the DEF-026 shape exactly.

Also: `aws ec2 wait instance-running` returns while the box is still **booting**, so the recipe
polls the SSM agent for `Online`; and the **2-hour warm-up** (`AC-084`/`OQ-067`) was carried into
the start section, because step 5 is an E2E run on a just-started box that would otherwise run cold
and fail falsely.

⚠ **One in-scope item was NOT done — see §5.6.** The plan scoped the `Streams.NameAr` prod check to
this day.

<details><summary>Original task text (for reference)</summary>

A new `deploy/runbooks/operations.md` that **fixes** the defects rather than documenting around
them. All three were verified by direct inspection:

1. **`deploy/runbooks/cloud-backup-dr.md:93` tells you to run `promote.sh` for cloud rollback.**
   That is the P18b **on-prem warm-standby** script whose premise (a standby VM) does not exist in
   the cloud topology — and `promote-image.sh`'s own header says so explicitly, having been written
   to prevent this exact confusion. `promote-image.sh` is the real one.
2. **`ec2 start-instances` / `stop-instances` appear NOWHERE** — verified with an untruncated
   `git grep … | wc -l` = **0** — yet UAT's whole operating model is stop-when-idle. **This is the
   blocker for step 4**: UAT cannot be brought up from the repo as it stands.
3. **Stale `$60`** at `cloud-provisioning.md:27,48,270` and `aws/README.md:21`. The budget is
   **$100** (raised to fund always-on prod, amending `ADR-0034`).

Add a **guard** so `$60` and cloud-`promote.sh` cannot silently return — a grep assert in CI, the
same assert-zero shape as `src/Acmp.Web/src/test/rtl-logical-css.test.ts`.

The guide should cover what this session actually proved: the redeploy recipe (with `1c7f2ba` as a
worked example), the ECR-before-re-pin check (`DEF-019`), the drift guard and what to do when it
trips, `smoke.sh`, the crontab `diff`, reaching Keycloak admin via SSM port-forward to box port
**80**, and the two alerts that are **correct** after a restart (backup-freshness, CPU credits) with
an explicit *do not tune these* — that threshold has been rejected twice.

</details>

---

## 4. Decisions the operator owes you (do not guess)

| Row | Question |
|---|---|
| **DEF-038** | The roster lists only members who have logged in (1 of 26 when observed) — members are JIT-provisioned from the token `sub`. Accept, or list invited-but-unseen accounts from Keycloak? |
| **DEF-039** | **Four of six** health tiles are unmonitored in **every** environment — the API registers only two checks (`api`, `sqlserver`). Register real checks server-side, or label the catalog as forward-looking? |
| **DEF-041** | Voting eligibility cannot be changed from the UI (the toggle is absent from the accessibility tree). Who may change it — Chairman? Administrator? There are SoD implications. |
| **DEF-036** | `/session` is a placeholder no nav links to. Build, route, or delete? It has no FR/AC. |

---

## 5. Operator-only actions (blocking or security)

1. **Webex UAT space + bot** — **blocks Day 4.** `OQ-062` does **not** ban Webex in UAT; it says
   *"off until a separate UAT space + bot exist"*. Creating them satisfies its own exit condition.
2. **`Streams.NameAr` on prod** — see **item 6 below**, which supersedes this line with the real
   table/column names and the exact query. The Arabic rename (`DEC-032`) cannot reach admin-entered
   data.
3. **MFA on `acmp-admin`, rotate its password, stop using root as the default profile.**
   *(Every AWS action this session ran as `acmp-admin`, never root.)*
4. **Delete `C:\Users\ahammo\OneDrive\Desktop\acmp-users.csv`** (26 temp passwords, syncing to
   OneDrive) and the orphaned scratchpad holding prod secrets — see the superseded resume file §7.
6. **✅ `Streams.NameAr` on prod — DONE 2026-08-11, VERIFIED CLEAN (`PE-236`).**
   **351 nvarchar/nchar columns across 83 tables, in BOTH the `Acmp` and `keycloak` databases, hold
   neither half of the renamed term family.** `membership.streams` has **0 rows**, which also
   confirms `BL-024` (stream creation has no UI). Run read-only by the operator, because the
   permission classifier refuses that command shape and it was not worked around.

   ⚠ **The column count is the load-bearing part of that evidence.** "Zero matches" and "the scan
   looked at nothing" print identically, so the verdict is only admissible because the scan proves it
   had a subject. Two earlier attempts were wrong: an unqualified `streams` returns *Invalid object
   name* (every module owns a schema), and the first scan searched only `الهندسة` — but `DEC-032`
   renamed a whole **term family**, so `الثابت المعماري` would have passed a check I was one step
   from recording as Met. The final scan covers the `معمار` root too.

   **Does not cover:** streams created through any future UI. Re-run if that ships.

<details><summary>Original text (superseded)</summary>

   The plan scopes this to item 2 ("worth one query against prod **while doing item 2**"), so it is
   a Day-3 gap. I attempted it and the **permission classifier blocked** the SSM command — it reads
   the SA password secret and execs into the production database container. Not worked around.
   The real target is **`membership.streams`**, column **`name_ar`**. Two corrections, both proven
   against the live database: the C# names `Streams.NameAr` do **not** exist in SQL, and the table is
   **not** in the default schema — an unqualified `streams` fails with *Invalid object name*, which
   is exactly what the first attempt returned. Every ACMP module owns a schema
   (`MembershipDbContext.Schema = "membership"`). Expected zero rows holding `الهندسة` because stream
   creation has no UI (`BL-024`) — but **expected ≠ verified**, which is the entire point. On the box:

   ```bash
   c=$(docker ps --format '{{.Names}}' | grep sqlserver | head -1)
   docker exec "$c" sh -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
     -P "$(cat /run/secrets/mssql_sa_password)" -C -No -d Acmp -h -1 -W \
     -Q "SET NOCOUNT ON; SELECT code, name_ar FROM membership.streams;"'
   ```

   Note the `sh -c` wrapper: without it the `$(cat ...)` is evaluated on the **host**, where the
   secret does not exist, and sqlcmd fails with a misleading *Login failed for user sa*.

</details>

   Easier alternative with no secrets: sign in and read Administration → المسارات.

5. **`AC-085` leg 1** — when spend crosses **$2.30**, run
   `bash deploy/scripts/check-budget-notification.sh` and `audit_record` the **printed body**. The
   body is the evidence; a count is not. The SQS observer is already proven end-to-end.

---

## 6. ⚠ Where the approved plan is WRONG — corrected during execution

The plan file still carries the original text for these. Trust this section over it.

| Plan says | Truth |
|---|---|
| Playwright is UAT-only, so the suite has never covered this work | **False.** `.github/workflows/e2e.yml` brings up the **full 7-service stack with real Keycloak** and drives the **real PKCE login on every PR**. All 10 checks were green on every merge. UAT adds *deployed-topology* validation (nginx 443, real TLS, S3 not MinIO, SSM secrets) — not application logic. |
| D-A fires on "every route change" | **False.** `AuthProvider`'s `useRef` guard holds within a session — it is **once per app mount**. The 14 rows in 60s came from full-page navigations during the audit sweep. |
| The health banner is "green because it isn't looking" | **False.** `SystemHealth.tsx:55` documents *"reflects only what is monitored"* — deliberate. |
| `/wiki` is missing an `<h1>` every register has | **False.** The design puts `t.navWiki` in a **breadcrumb**; the wiki is *designed* without a page title. |
| `/actions`: design and domain disagree | **False.** `ActionItem.SourceKey` says *"snapshot for the Linked column"* — they agree. Only the picker was missing. |
| MinIO tile is dead "on prod and UAT" | **False.** Four of six tiles are unmonitored **everywhere**. |

---

## 7. Gotchas this session paid for — do not rediscover them

- **⚠ NEVER call something a defect until you have read the implementation that produces it.**
  This happened **six times** (rows in §6 plus `DEF-040`). Every one was caught before shipping, and
  **not one was caught by a gate** — each was caught by reading the thing itself. **Four would have
  shipped a change that broke deliberate behaviour.** This is the single most expensive pattern of
  the session.
- **`gh pr merge` can stick at `mergeable: UNKNOWN`.** Normal right after the base moves — poll for
  `MERGEABLE`. But #221 stayed UNKNOWN through **nine** attempts and five base changes with green CI
  and an unprotected `main`. The fix is to **recreate the branch** (`git cherry-pick` its commits onto
  current `main`) — that merged first try.
- **Package writes are branch-scoped.** `tamheed-package/data` is git-tracked, so writing from a
  feature branch fragments the record and `progress_update` will refuse with *"data/ changed on disk"*.
  **Only write the package from `main`, right after a merge.**
- **`defect.fixed_by` is a FOREIGN KEY**, not free text — a PR string fails with `FOREIGN KEY
  constraint failed` and rolls back the whole batch. Put refs in `custom_attributes`.
- **Coverage is per-file ≥95%** (`perFile: true`). A new component needs its loading, error and
  each-branch paths tested or the gate fails while every test passes — it caught a new file at 92.23%.
- **PowerShell here-strings mangle `git commit -m`** — the message is parsed as arguments. Write the
  message to a file and use `git commit -F <file>`.
- **`vi.clearAllMocks()` in `beforeEach`**, not just `mockReturnValue` — otherwise a "was this called?"
  assertion sees the previous test's clicks.
- **`PYTHONIOENCODING=utf-8`** for anything touching Arabic, and prefer the `Edit` tool over regex
  surgery on JSON — a heredoc'd regex silently failed mid-edit.
- **An absence claim needs an untruncated search** — `| head` in the pipeline means you may not say
  "it isn't there". Publish the `wc -l` count.

---

## 8. Do NOT

- **Point Playwright at production.** `e2e/global-setup.ts` refuses a prod host by design — the suite
  seeds fixed-password accounts and writes governance rows the immutable chain can never remove. Use
  `deploy/scripts/smoke.sh <host>` for prod.
- **Relax `09-put-env.sh`'s UAT Webex guard to create a test bed.** Replace it with a *targeted*
  space-id check instead — that **strengthens** `AC-083` rather than weakening it.
- **Touch `docs/`** (frozen archive) or rename inside `tamheed-package/` (those files quote prose as
  written at the time).
- **Tune the backup-freshness or CPU-credit alarm** after a restart — both firing is *correct*, and
  threshold-lowering has been rejected twice.
- **`up --build` the long-lived dev stack** — SQL volume/password mismatch.
- **Deploy to prod before UAT is green.** The operator asked for the full regression first.
