# Resume prompt — PH-5 acceptance close-out (paste into a fresh session)

> Rewritten 2026-08-09. Supersedes the earlier version of this file, which is stale in several
> places — most importantly its claim that CPU credits "recovered while the box sat idle". They do
> not; see the AC-084 section. Everything below is also in the package.

---

Resume the PH-5 acceptance close-out on ACMP. UAT is live and running the current `main`. **Eleven
acceptance criteria are Met; one is Partial**, and the last one needs only an observation — no
engineering remains. Full handoff: `handoff/RESUME-ph5-closeout.md` — read it first.

## Orient before touching anything

1. `server_info()` → expect **2.7.1**. Then `package_open("tamheed-package")`, then `gate_run()` →
   expect **7/7 ready**, audit evidence **105 evidenced / 12 narrated**.
2. Read the reasoning rather than re-deriving it: **AV-106** (AC-080 Met), **AV-115** (AC-076 Met), **AV-112** (AC-084 Met, supersedes AV-109), **AV-117** (AC-085 - read this one, it CORRECTS the closing test), **DEF-030/031/032** (three controls that could not notify), **OQ-066**/**OQ-067** (Approved) and **OQ-068** (PROPOSED - awaiting your decision), and progress entries
   **PE-184 … PE-199**.
3. `git log --oneline -5` and `gh pr list`.
4. **Close the package** (`package_close`) when done reading — it holds a single-writer lock, and
   **commit `tamheed-package/data` the moment it returns**.

## State

- **UAT runs `87b4a8a8`.** Instance **`i-07ac28ac2fedab921`** is the only instance
  in the account; Elastic IP **35.173.149.191**; `https://uat.acmp.anas7ammo.dev`. `/acmp/uat/env`
  pins `ACMP_IMAGE_TAG=87b4a8a8…` and `ACMP_WEB_TAG=87b4a8a8…-uat`, so box and pins agree.
  `main` (`fcb78c1`) is ahead by DEF-033's one-line fix plus governance commits. That fix is
  **inert under cron** (the crontab supplies `ACMP_ENV_FILE`), so no redeploy is needed; it lands on
  the next bootstrap.
- **`export AWS_PROFILE=acmp-admin`** on every AWS call. Never operate as root.
- Acceptance: **AC-075/076/077/078/079/080/081/082/083/084/086 Met** · **AC-085 Partial** (leg 1 only).
- Open defects: **DEF-012** only (v_backlog residue — disclosed by design, no action).
  DEF-027 … DEF-033 are all **Fixed**.
- Seeded accounts `chairman` / `secretary` / `member` / `auditor`, password `Uat_Acmp#2026_Rotated`.
  The three `e2e-*` accounts are **disabled**, not deleted — read DEF-029 before changing that.

## Do these, in this order

### AC-076 is Met — do NOT re-do it (AV-115)

7 passed, 0 failed against live UAT: real Keycloak PKCE, the full core governance loop in 25.6 s,
and RTL/a11y axe-clean in both languages. Closing it needed three defects fixed — DEF-027 (the AC's
own stated method was impossible), DEF-028 (a real product race CI cannot see) and DEF-029 (orphaned
member rows). All three are **Fixed**. The `/kc/` deny was re-verified live at 200/404/404, so
AC-081 is intact.

⚠ **Never delete the `e2e-*` accounts — disable them** (`{"enabled": false}`). They are disabled now.
Deleting them mints new Keycloak subs and permanently duplicates their member rows (DEF-029).

### 1. AC-085 leg 1 → Met — pure observation, no action (~1 day)

Legs 2–5 are Met. **The transition has already happened** (AV-114): spend crossed $1.20 and the 2 %
ACTUAL notification now reads **ALARM** — a genuine OK→ALARM on real spend, which is what the first
attempt could never produce. Arming a threshold *below* current spend goes ALARM instantly with no
transition to notify on, so nothing is ever delivered — a check that cannot succeed.

⚠ **DO NOT use "NumberOfMessagesPublished is non-zero" — that test is INVALID** (AV-117 corrects
AV-114/AV-116, which said it). Fixing DEF-031 and DEF-032 required publishing to this same topic:
**six** messages were published on 2026-08-09, all from those tests, the last at 14:00Z. The metric
is *already* non-zero, so following the old instruction would report leg 1 Met when no budget
notification has ever arrived. That topic now carries **three** signal types — budget notifications,
CloudWatch alarm transitions and backup failures — which is the price of reusing one topic.

**Use the email instead; it never degraded.** The three senders are trivially distinguishable:

| signal | how it identifies itself |
|---|---|
| budget notification | from `budgets@costalerts.amazonaws.com`, naming `acmp-monthly` |
| CPU-credit alarm | names `acmp-uat-cpu-credits-low` |
| backup failure | subject `ACMP backup FAILED on <host>` |

**Corroborate** with a publish datapoint in the 5-minute bucket containing the moment the 2.3 %
notification flips OK→ALARM, with no alarm transition and no backup run in that window.
**Baseline for future deltas: cumulative 6 as of 2026-08-09T14:05Z.** Then delete the 2 %
notification. Current state: spend **$1.286** against the **$1.38** trigger, notification in OK.

**State alone is not arrival** — that distinction is what caught DEF-030, so do not blur it. If the
threshold flips and no budget email arrives, that is a finding in its own right, not a wait.

If you want a cheap automated check back, create `acmp-ops-alerts`, repoint `ACMP_ALERT_TOPIC_ARN`
and the alarm's actions at it, and leave `acmp-budget-alerts` carrying budget traffic only.

### AC-084 is Met — do NOT re-do it (AV-112)

Recorded here because an earlier draft of this file sent a session to close it after it was already
closed. The alarm cleared on its own at 50.09 credits after ~1h50m of running, with **no threshold
tuning**, and the run then held it OK throughout while the balance ROSE, 50.09 → 51.59. The number
that settles it is usage, not balance: **0.426 and 0.498 credits** consumed in the two 5-minute
periods covering the run, against **2.0 earned** in the same window. An e2e run costs about a
quarter of what the box earns while running it — so the clause was never about e2e load at all, only
about cold-start warm-up. ⚠ **A STOPPED t3 EARNS NO CPU CREDITS**; the alarm reads OK while stopped
only because a stopped instance publishes no datapoints. Memory holds too: Keycloak 430.7 MiB of its
448 cap, zero OOM, all six containers healthy. **Keycloak's ~17 MiB of headroom is a standing risk**,
the thinnest in the stack against a 3536/3584 MiB limits total; the slack to fund any raise is in
`worker` and `seq`. See **OQ-067** for the design question this raised.

## Running the suite against UAT (proven recipe)

```bash
# 1. tunnel — session-manager-plugin needs NO install: AWS's SessionManagerPlugin.zip holds a
#    nested package.zip whose bin/session-manager-plugin.exe runs standalone, on PATH.
aws ssm start-session --target i-07ac28ac2fedab921 \
  --document-name AWS-StartPortForwardingSession \
  --parameters '{"portNumber":["80"],"localPortNumber":["8085"]}'
# 2. box port 80 -> the nginx 8080 block, which carries NO /kc/ deny. The public 443 listener
#    still 404s /kc/admin and /kc/realms/master, and must keep doing so (AC-081).
# 3. KC_BOOTSTRAP_ADMIN_USERNAME/PASSWORD come from the box's docker secret. Pull them
#    disk-to-disk; never render them into a transcript.
E2E_WEB_URL=https://uat.acmp.anas7ammo.dev E2E_KEYCLOAK_URL=http://localhost:8085/kc \
  npx playwright test e2e/auth.spec.ts e2e/core-loop.spec.ts e2e/rtl-a11y.spec.ts
# 4. afterwards: DISABLE the e2e-* users (never delete — DEF-029).
```

Only the three specs above are in AC-076's scope. The other 25 are visual-regression specs whose
baselines come from the local stack; they would fail on UAT for reasons that say nothing about UAT.

## Redeploying (proven recipe)

`main` → CI publishes on push only → verify the images exist in ECR **before** re-pinning
(DEF-019) → `bash deploy/aws/09-put-env.sh uat <env-file>` with `ACMP_IMAGE_TAG=<full-sha>` and
`ACMP_WEB_TAG=<full-sha>-uat` → `bash deploy/aws/08-bootstrap-box.sh uat <full-sha>`. The drift
guard should **pass on its own**; if you find yourself reaching for `ACMP_ALLOW_TAG_DRIFT=1`, stop —
that flag is for deliberate rollbacks only.

## Environment gotchas — these cost hours; do not rediscover them

- **`tamheed-package/data` is git-TRACKED.** `git reset --hard` / `checkout` / `stash` destroy
  uncommitted package writes. **Commit the moment `package_close` returns.**
- **⚠ A claim of ABSENCE is never supportable by a truncated search.** `tail`/`head`/`-m` in the
  pipeline means "I did not check", not "it is not there". This put a false lead into DEF-028 and
  cost a retraction; it is the second occurrence of the shape (PE-183 was the first).
- **⚠ In a system with immutable history, cleanup is not symmetric with creation.** Deleting an
  upstream identity ORPHANS what it created downstream, permanently. That is DEF-029.
- **⚠ For every control, ask SEPARATELY whether it can DETECT and whether it can TELL.** Four
  instances in this project: DEF-023 (health probes green on a box nobody could log into), DEF-030
  (budget notifications changing state while every publish was denied), DEF-031 (an alarm with
  `AlarmActions: []`), DEF-032 (a backup regime that could not report its own failure). In all four
  the *detect* half was tested and the *tell* half was asserted in a comment that made it look
  verified. DEF-030 was found only because AWS emailed the account contact.
- **⚠ When a verification test depends on a baseline, record the baseline as a NUMBER AND A
  TIMESTAMP, never as a property like "zero" or "empty".** "Non-zero against a zero baseline"
  embeds an assumption that nothing else will ever write there — and my own later fixes falsified
  it, turning AC-085's closing test into one that passes for the wrong reason. No gate can catch
  that: the gates check row integrity, not whether a stated test still discriminates.
- **An env file sourced with `set -a` beats the command line.** `backup.sh` sources its env *after*
  the process environment, so `FOO=bar backup.sh` is silently ignored for anything the file sets.
  A "forced failure" drill built that way is a check that cannot fail. Override the *file*.
- **`aws ssm send-command` has NO `set -e`.** The invocation status is the *last* command's, so a
  mid-list failure still reports Success. Send the whole script as ONE element under `set -eu`.
- **MSYS mangles leading-slash AWS arguments.** `--name /acmp/uat/env` returns `ParameterNotFound`
  until `MSYS_NO_PATHCONV=1` is set — while `describe-parameters` lists it happily. Confirmed again
  2026-08-09. `07/08/09` set it in-script; `05-route53.sh` needs conversion **on** with `cygpath -m`.
- **Reading SSM output on Windows** needs `PYTHONIOENCODING=utf-8 PYTHONUTF8=1`; commands via
  `file://` JSON; stdout caps at 24,000 chars (`--quiet-pull`).
- **`python -c` with embedded newlines is broken here** — `python` is a pyenv-win `.bat` shim that
  mangles it. Put the script in a `.py` file. Also use `pwd -W`, not `pwd`, when handing a path to a
  Windows binary from MSYS — **but NEVER put a `pwd -W` path on `PATH`**: it contains a drive-letter
  colon, which is PATH's separator under MSYS, so appending it splits into garbage and every tool on
  it silently disappears. Keep both spellings and do not interchange them. (This made
  `session-manager-plugin` report MISSING while sitting right there on disk.)
- **A tunnel opened by another process is not one you can rely on.** SSM sessions idle-time out, so a
  script that needs a port-forward should open its **own** and kill it on a trap.
- **Restarting the box can leave the SSM session worker wedged** (`document process failed
  unexpectedly: ipc messaging received timeout signal`) while `send-command` keeps working perfectly.
  `systemctl restart amazon-ssm-agent` clears it. The signal you happen to be watching is green while
  the capability you need is dead — the same shape as health probes passing on an unreachable box.
- **Python writing to this console dies on non-ASCII** (`cp1252`). Write files with
  `encoding='utf-8'`; keep `→`/`—` out of anything you `print`.
- **Never hand-edit files on the box.** A `chmod` there left the checkout dirty and the next
  bootstrap refused to `git checkout` over it. Rebuild rather than edit.
- **The local cloud boot gate cannot verify a browser login** — the `web` image bakes
  `VITE_OIDC_AUTHORITY` per environment (ADR-0037). It *can* prove the authorization round-trip and
  the `/kc/` headers, and does.
- **CI cannot catch load races.** On localhost the data always wins; over the internet it does not.
  That is DEF-028, and it is why AC-076 exists at all.
- **The Playwright MCP server is unusable**; use the repo's own Playwright. The committed login
  probe is `src/Acmp.Web/uat-login-probe.mjs`.
- **`block-no-verify` falsely blocks a bare `-n` anywhere in the command** — including inside a
  commit message body. Reword rather than fight it.

## Cost discipline — binding, not advisory

Budget **$60/mo**; actual spend **$1.065**. **Never run two instances.** **Never create a NAT
gateway** (~$32/mo). **Stop the box when idle** (~$7.65/mo stopped vs ~$38/mo running) — but note
the AC-084 warm-up tension in OQ-067. On any new instance verify `CpuCredits=standard`, 50 GB gp3,
zero NAT gateways. `/acmp/prod/env` **does not exist**, so prod remains blocked on operator secrets
regardless.

## Do not

- Do not re-litigate settled decisions: Elastic IP over a boot-updater; the widened Route 53 grant;
  `promoted_to` as the DEC→ADR link; the `/kc/admin` + master-realm deny.
- **`docs/` is a frozen read-only archive.** Its `NFR-056`/`NFR-057` swap is **left wrong on
  purpose**. Do not "fix" the archive.
- Do not hand-edit `tamheed-package/` — the MCP tools are the only write path, and corrections to
  the append-only journal are **appended**, never edited.
- Do not start **P14 / Tarseem diagrams** — deferred indefinitely by DEC-028.
- **Do not mark an AC Met on a check that cannot fail** — and note the inverse now has a case too:
  AC-085 leg 1's first attempt was a check that could not *succeed*.
