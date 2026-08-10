> # ⛔ SUPERSEDED — 2026-08-11
> **Start from [`RESUME-day3-uat-regression.md`](RESUME-day3-uat-regression.md) instead.**
>
> The Batch C rename shipped (`DEC-032`, `1c7f2ba`) and production was redeployed onto it
> (`PE-227`). Days 1–2 of the follow-on plan then landed as PRs #222–#230.
>
> **Most of the state below is now stale** — the PH-5 rollup, the defect counts, the "next task",
> and §4's redeploy target all moved. Two things here are still worth reading and are NOT repeated
> in the new file: **§5's gotchas** (Keycloak admin via SSM port-forward to box port 80, the
> `session-manager-plugin` POSIX-path trap, `PYTHONIOENCODING`, never `sed -i` a CRLF file,
> prod/UAT differing on purpose) and **§7's operator actions**, which include the exact path of the
> orphaned scratchpad still holding live production secrets.

# RESUME — Arabic term rename (Batch C), and everything else outstanding

**Written 2026-08-10 at the end of the session that seeded production.** Every fact below was
verified immediately before writing, not carried forward from an earlier note.

---

## 0. Orient first (2 minutes, do not skip)

```
server_info() · package_open("tamheed-package") · gate_run()
```

Then read the approved plan for the task below:
`C:\Users\ahammo\.claude\plans\c-users-ahammo-onedrive-desktop-acmp-use-crystalline-lemon.md`

It contains the full Arabic mapping, the file list, the exclusion list, and the verification gate.
It was written after an adversarial review and its §2 records which assumptions were **verified**
versus merely believed. Do not re-derive it; do check anything it claims that you are about to rely on.

---

## 1. State of the world — measured 2026-08-10

| | |
|---|---|
| `main` | `b66377d`, working tree clean, all PRs merged |
| **Production** | **LIVE** at https://acmp.anas7ammo.dev · `i-04d9717feea79204b` · EIP `52.23.105.56` · **running, always-on by decision** |
| **UAT** | `i-07ac28ac2fedab921` · EIP `35.173.149.191` · **stopped** (stopped-when-idle by decision) |
| Budget | **$100/mo** (raised from $60 to fund always-on prod, amends ADR-0034). Spend $1.869 |
| Committee | **26 real members seeded on prod**, correct roles, real names. AC-079 **Met** |
| PH-5 rollup | **12 Met, 1 Partial** — only `AC-085` (leg 1) is open |
| Defects | **DEF-012 is the only open one** of 35 (v_backlog residue, package-data) |
| Open questions | **zero unresolved** of 68 |

**Prod is running deliberately** — 26 people hold credentials. Do not "tidy" it to match UAT; the two
environments are configured differently on purpose (see §5).

⚠ **The running prod images predate the DEF-034 and theme fixes.** Those shipped in `b66695c` but
were never deployed. A redeploy is needed before anyone sees them — see §4.

---

## 2. ✅ DONE 2026-08-10 — Batch C, the Arabic rename

> **Landed in `1c7f2ba` (PR #218), recorded as `DEC-032`.** 28 files, 94 `معمار` + 57 `الهندسة`
> occurrences retired, assert-zero gate at 0 residue, CI green on all 9 checks. **Do not redo it.**
> The section below is kept as the brief that was executed, with two corrections it earned:
>
> - **The mapping in the approved plan was wrong for indefinite sources.** Definiteness is
>   **preserved** — definite → `الهيكلة`, indefinite → bare `هيكلة`. Collapsing both (as the plan
>   said) ships ungrammatical Arabic after `كل` and breaks adjective agreement. See `DEC-032`.
> - **`spike-cloud-gates.sh:149`/`:200` no longer need manual coordination** — both now hold the
>   *identical* string, so the FTS gate is self-consistent by construction.
>
> ⚠ **Still open, and it is a DATA task the rename could not reach:** streams are created by
> admins through the UI, so prod may hold `الهندسة` in `Streams.NameAr` (or in Arabic-typed
> topic/ADR titles). Nothing in `deploy/` seeds them — `seed-users.sh` creates Keycloak accounts
> only and the app JIT-provisions members from the token `sub` — so there are probably zero rows,
> but **query prod before calling the rename complete**. The login tagline reading
> `منصة إدارة لجنة الهيكلة` next to a stream chip still reading the old term is the failure mode.

### The brief, as executed

Rename the Arabic term for "Architecture" from **الهندسة المعمارية** to **الهيكلة**, product-wide.

Two decisions already taken by the operator, do not re-ask:
- **Scope: the whole term family, including the 17 `.dc.html` design reference files.**
- **Form: the noun in إضافة** — `ثوابت الهيكلة`, not `الثوابت الهيكلية`.

The exact phrase is only ~16 occurrences, but **89 more Arabic occurrences also mean Architecture**
(`معماري`, `المعمارية`, standalone `الهندسة`). A literal replace leaves the product half-renamed —
and `ar.json` is *already* inconsistent (`:2` = `لجنة الهندسة المعمارية`, `:1573` = `لجنة الهندسة`),
so the literal-only route makes it **worse**, not better.

**Never touch** `مهندس` / `المهندسون` / `للمهندسين` — "engineers", same root, different word.
Verified letter-by-letter that a literal `الهندسة` replace cannot reach them.

**Two coupled things that will bite:**
- `deploy/scripts/spike-cloud-gates.sh:149` seeds the phrase and **`:200` greps `الهندسة`** to prove
  FTS found it. Change one without the other and the cloud gate fails for a reason that looks
  nothing like a rename.
- Backend notification copy lives in `AdrNotifications.cs` and `InvariantNotifications.cs`
  (7 occurrences each) and is covered by **no** i18n gate.

**Already checked, so don't re-litigate:** the Arabic FTS tests are safe — their query terms are
`"قرار"` and `"architecture"`, neither of which changes. Only seeded values and one exact-match
assertion move.

**The gate is assert-ZERO, not a count.** `git grep -c` counts *lines*, not occurrences — `ar.json:2`
holds two on one line, which already produced a 15-vs-16 disagreement. Use `git grep -l` over tracked
files and require only the allow-list to remain.

---

## 3. Then, in this order

1. **Redeploy prod** so the merged fixes are actually live (§4).
2. **A1 — a separate `acmp-ops-alerts` topic.** `AC-085` leg 1 needs a budget notification *observed
   to arrive*, and `acmp-budget-alerts` now carries three unrelated signal types, so **no count-based
   test on it can ever discriminate** (AV-118). Splitting the topic restores an automated check and
   costs one confirmation click. Trigger is now **$2.30** (2.3% of $100), spend $1.869.
3. **Operator guide** — a new `deploy/runbooks/operations.md`. It must **fix**, not document around:
   `cloud-backup-dr.md:93` tells you to run `promote.sh` for cloud rollback — that is the on-prem
   warm-standby script whose premise doesn't exist here; `promote-image.sh` is the real one. Also:
   start/stop instance commands appear **nowhere** in the repo, and stale `$60` figures remain at
   `cloud-provisioning.md:27,48,270` and `aws/README.md:21`.
4. **Webex, production only** (UAT stays off per OQ-062 — a ratified decision, not an oversight).
   Blocked on a verified gap: `docker-compose.cloud.yml:222-227,273-277` passes only the *non-secret*
   Webex keys, so `WebexOptionsValidator.cs:19-24` **fails boot** without `TokenEncryptionKey`.
5. **DW-020 Confidentiality ABAC** — `security-controls.md` C-AUTHZ-04 specifies `Restricted` topics;
   no such field exists in the codebase. A feature, not a hardening pass.
6. **DEF-012** — the last open defect.

---

## 4. Redeploy recipe (proven)

`main` → CI publishes on push only → **verify the images exist in ECR before re-pinning** (DEF-019)
→ `bash deploy/aws/09-put-env.sh prod <env-file>` with `ACMP_IMAGE_TAG=<full-sha>` and
`ACMP_WEB_TAG=<full-sha>-prod` → `bash deploy/aws/08-bootstrap-box.sh prod <full-sha>`.
The drift guard should **pass on its own**; reaching for `ACMP_ALLOW_TAG_DRIFT=1` means stop.

`08-bootstrap-box.sh` now installs the crontab itself — verify with
`diff <(crontab -u root -l) /opt/acmp/deploy/scripts/crontab.example`.

---

## 5. Gotchas that cost hours here — do not rediscover them

- **⚠ A CHECK THAT LOCATES ITS SUBJECT BY SUBSTRING CAN BIND TO PROSE *ABOUT* THE SUBJECT.** Three
  instances in one day: the seeding script whose body-greps turned an expired admin token into three
  different fake *data* failures; a DNS check that reported the **resolver's** IP as the host's; and
  `contrast.test.ts`, whose `indexOf('[data-theme="dark"]')` matched a **header comment** and graded
  the light palette as dark for the file's entire life. Match on structure, not on substring.
- **Reaching Keycloak's admin API on a cloud box:** SSM port-forward to box port **80**. The 8443
  listener 404s `/kc/admin` and `/kc/realms/master` **by design** (AC-081 rests on it); the 8080
  block has no such deny and inbound 80 is closed at the security group. Never relax the 443 deny.
  A script needing a tunnel should open **its own** and kill it on a trap.
- **`session-manager-plugin`** lives at `/c/Program Files/Amazon/SessionManagerPlugin/bin`. Put it on
  `PATH` in **POSIX** form — a Windows path splits at the drive-letter colon and the binary silently
  vanishes.
- **Arabic + Windows console:** always `PYTHONIOENCODING=utf-8`. Plain `python -c` writes cp1252 and
  turns `—`/`→` into replacement characters — transcribing from that output corrupts requirement text.
- **Never `sed -i` a CRLF file** — it rewrote all 36 line endings of the SSM env payload as a side
  effect of a two-line edit, and the check that said otherwise passed for the wrong reason.
- **`no checks reported` ≠ passed.** Gate a merge on `fail == 0` **and** `pending == 0` **and**
  `total > 0`. #213 was merged past a failing check because the merge was chained onto the wait
  without testing its result.
- **Local `vitest run` ≠ CI.** It does not compute coverage; the per-file ≥95% floor is a separate
  gate. Run `npm run test:cov`.
- **`tamheed-package/data` is git-TRACKED.** Commit the moment `package_close` returns; `git reset
  --hard`/`checkout`/`stash` destroy uncommitted package writes.
- **Prod and UAT differ on purpose.** `ACMP_BACKUP_MAX_AGE_HOURS` is 26 on prod (always-on) and 168
  on UAT (stopped for days). Separate buckets, passwords and IAM keys — that separation is what makes
  the AC-083 isolation test meaningful rather than circular.
- **On restart**, expect two *correct* alerts: backup-freshness (box was off longer than the
  threshold) and the CPU-credit alarm for ~2h. **Do not tune either** — threshold-lowering was
  rejected twice because it rebuilds the un-failable check the alarm exists to catch.

---

## 6. Do NOT

- Touch `docs/` — frozen archive, and it contains a known-wrong NFR-056/057 swap **left wrong on
  purpose**.
- Rename inside `tamheed-package/` — those files quote progress-log prose *as written at the time*;
  editing them falsifies the record. They regenerate via `export_html`.
- Point the Playwright suite at production. `e2e/global-setup.ts` now **refuses** a prod host, because
  the suite seeds fixed-password accounts and writes governance rows that the immutable audit chain
  can never remove. Use `deploy/scripts/smoke.sh <host>` for prod.
- Re-open OQ-062 (Webex off in UAT) or lower any alarm threshold without an explicit operator decision.

---

## 7. ⚠ Operator action — production secrets on disk

This session's scratchpad is about to be orphaned by `/clear`. It contains **live production
secrets**:

```
C:\Users\ahammo\AppData\Local\Temp\claude\C--Users-ahammo-Repos-acmp\0facad2f-a83f-45b0-a92d-7db892b90d5c\scratchpad\
  prod-credentials.txt   Keycloak bootstrap admin + Seq admin passwords
  prod.env               the full prod environment, all DB passwords
  prodkey.txt            acmp-prod-app S3 access key + secret
  roster-*.csv           copies of the committee roster
```

**Move what you need to a password manager and delete that folder.** Everything durable is
recoverable from SSM: `aws ssm get-parameter --name /acmp/prod/env --with-decryption`.

Also still outstanding, and only you can do them:
1. **Stop using root.** Your default AWS profile is `login_session = …:root`. Root **cannot** be
   constrained by the budget IAM-deny brake (AC-085 leg 5), so root CLI work bypasses your cost
   guardrail. Work as `acmp-admin`.
2. **MFA on `acmp-admin`**, and **rotate its password** — set under delegation during seeding.
3. `C:\Users\ahammo\OneDrive\Desktop\acmp-users.csv` holds all 26 temporary passwords and syncs to
   OneDrive. Delete it once delivered, and empty the recycle bin.
