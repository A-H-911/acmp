# Cloud provisioning runbook — AWS EC2 (PH-5 / SL-025)

The path from **no instance** to an **all-healthy stack on a real hostname with valid TLS**.
This is the document AC-075 means by *"bootstrapped from the runbook"*.

> **Scope.** EC2 + Docker Compose per ADR-0034, S3 per ADR-0035, Keycloak on SQL Server per
> ADR-0036, two subdomain environments per ADR-0037. Once the box exists, day-to-day operation —
> start/stop, deploying a commit, alerts — is [cloud-operations.md](cloud-operations.md). For
> backup/restore/DR see [cloud-backup-dr.md](cloud-backup-dr.md). The on-prem runbook ([README.md](README.md)) is
> **superseded for anything on EC2** — its commands use `up.sh` and `docker-compose.prod.yml`,
> which are the wrong files for a cloud box.

> **Who runs these.** All `deploy/aws/*.sh` scripts are **operator-run by default** from an IAM admin
> identity (`acmp-admin`), never as root and never by CI. The assistant runs them **only on explicit
> operator delegation** — first exercised 2026-08-11 for the UAT deploy of `e403e18`. They are
> idempotent (check-then-create), so re-running is safe.
>
> ⚠ **From Git Bash on Windows, export `MSYS_NO_PATHCONV=1` first.** MSYS rewrites any argument
> beginning with `/`, so `--name /acmp/uat/env` reaches AWS as `C:/Program Files/Git/acmp/uat/env`.
> SSM answers `ParameterNotFound` rather than `AccessDenied` for a name you cannot see, so this
> presents as a **missing IAM permission** and very nearly bought an unnecessary policy widening.
> Same family: `smoke.sh`'s S3 leg passes `--body $(mktemp)`, a POSIX path the Windows `aws.exe`
> cannot open — that leg fails on Windows and passes on the box. Neither is an environment fault.

---

## 0. Cost — read before launching anything

| | Prod (always-on) | UAT (on-demand) |
|---|---|---|
| t3.medium @ $0.0416/hr | $30.37/mo | ~$1.70/mo @ 40 hrs |
| gp3 root 50 GB | $4.00 | $4.00 |
| Public IPv4 / Elastic IP | $3.65 | $3.65 |
| **Subtotal** | **~$38/mo** | **~$9/mo** |

Budget is **$100/mo**, with an IAM-deny action at 100% and an EC2 stop action armed by `07-launch.sh`
— both at 100% ACTUAL, alongside 50/80/100% notifications. It was raised from $60 on 2026-08-09 to
fund an always-on production box (amending ADR-0034); the two subtotals above are **~$47/mo of
$100** in steady state.

**Rules that keep it under the ceiling:**

1. **Never leave two instances running _always-on_.** Prod always-on plus UAT started for a session
   is the intended shape and fits. Two *always-on* t3.mediums is ~$84/mo, which sits above the 80%
   notification with no headroom before the brake — think before adding one.
2. **Stop UAT when idle.** Stopped it costs only EBS + Elastic IP (~$7.65/mo). This is what AC-082
   exists to make safe — the stop/start cycle is a cost control, not just an acceptance criterion.
   The commands are in [cloud-operations.md](cloud-operations.md).
3. **Do not launch prod until UAT is proven.** Saves ~$38/mo across P25 and P27.
4. **Never create a NAT gateway.** ~$32/mo — a third of the budget, for nothing this design needs.
   The instance sits in a public subnet of the default VPC behind the IGW.
5. Elastic IPs are allocated **at launch, per environment**. An allocated-but-unassociated address
   bills the same $0.005/hr for nothing.

The account also carries an unrelated `My Monthly Cost Budget` at $10 that alerts long before the
ACMP brake. Do not read its alerts as the brake firing.

---

## 1. One-time landing zone (per account)

```bash
bash deploy/aws/00-account.sh      # AS ROOT, once. IAM admin user, SNS, $100 budget, IAM deny brake
# then switch to the acmp-admin profile for everything below
bash deploy/aws/01-network.sh      # security groups: inbound 443 ONLY (no 22 — SSM; no 80 — DNS-01)
bash deploy/aws/02-s3.sh           # recordings + backups buckets, BPA, SSE, versioning, lifecycle
bash deploy/aws/03-iam.sh          # per-env app user (emits an S3 key) + instance role/profile
bash deploy/aws/04-ecr.sh          # four repositories, scan-on-push, keep-last-10-commits
bash deploy/aws/06-github-oidc.sh  # CI push role, no long-lived keys
```

**Console-only afterwards:** enable MFA on `acmp-admin`, confirm the SNS subscription email, and
**stop using the root user** — the IAM deny brake binds `acmp-admin` and cannot constrain root at
all, so while you operate as root the brake reads armed and protects nothing.

---

## 2. Publish the environment configuration

```bash
cp deploy/.env.cloud.example /secure/place/uat.env
# fill in EVERY CHANGE_ME; paste the S3 key/secret 03-iam.sh emitted;
# generate a real Seq hash:  docker run --rm datalust/seq config hash
bash deploy/aws/09-put-env.sh uat /secure/place/uat.env
```

`09-put-env.sh` strips full-line comments before publishing. This is **required, not tidiness**: a
raw `.env.cloud` is ~5.5 KB against SSM Parameter Store's 4 KB Standard-tier limit, and with the
account default tier at Intelligent-Tiering an oversized value is silently promoted to a **billed**
Advanced parameter. Stripped, it is ~1.1 KB and stays free.

It refuses to publish if any `CHANGE_ME` survives, and — for **uat** — if `WEBEX_ENABLED` is
anything but `false` or any Webex credential carries a value (AC-083 / OQ-062: UAT must not be able
to post into the prod Webex space).

---

## 3. Launch the instance

```bash
bash deploy/aws/07-launch.sh uat            # t3.medium by default
```

This launches with `CpuCredits=standard` (ADR-0034 — `unlimited` bills surplus CPU credits with no
ceiling), a 50 GB encrypted gp3 root, IMDSv2 required, **allocates and associates an Elastic IP**,
and re-points the budget stop-action at every live ACMP instance id.

It is idempotent on the `Name` tag and will never launch a second instance for an environment.
**Re-run it after any instance replacement** so both the brake and the Elastic IP follow the new id.

---

## 4. Point DNS — once

```bash
bash deploy/aws/05-route53.sh uat <public-ip>     # the IP 07-launch.sh printed
```

Because the address is Elastic, **this never needs running again for this instance.** A stop/start
returns on the same address, which is what AC-082 relies on.

---

## 5. Bootstrap the box

```bash
bash deploy/aws/08-bootstrap-box.sh uat <commit-sha>
```

Delivered over **SSM Run Command**, not SSH — there is no inbound 22 and no key material anywhere.
It installs Docker + compose (pinned) + git, creates a 4 GiB swapfile with its `/etc/fstab` entry,
clones the repo at the pinned commit into `/opt/acmp`, creates `/opt/acmp/backups` owned by uid
10001, reads the environment from SSM, generates secrets, configures the ECR credential helper,
writes a **self-signed TLS placeholder** so nginx can start before certbot has ever run, pulls, and
brings the stack up — then **asserts** every service reached `healthy` and every one-shot exited 0.

**It refuses on image-tag drift.** The box checks out the sha you pass, but runs whatever
`ACMP_IMAGE_TAG` / `ACMP_WEB_TAG` the SSM parameter pins — frozen at whatever was current when the
environment was last published. The first rebuild ran `749071e` images against a `2ec8c14` checkout
and nothing noticed. If the guard fires, re-publish the environment with this commit's tags (CI writes
the **full 40-char sha**, and `web` carries the `-uat` / `-prod` suffix — DEF-019) and re-run:

```bash
# edit ACMP_IMAGE_TAG=<full-sha> and ACMP_WEB_TAG=<full-sha>-uat, then
bash deploy/aws/09-put-env.sh uat <path-to-env-file>
```

A deliberate rollback to older images is `ACMP_ALLOW_TAG_DRIFT=1`.

> ⚠ **The first run of this script is a test OF THE SCRIPT.** It had never executed before P25.
> Expect it to be wrong somewhere and fix it there. DEF-019 and DEF-020 were both in deploy code
> that was carefully written, reviewed, and never run.

---

## 6. Issue TLS (AC-081)

On the box, as root:

```bash
dnf install -y certbot python3-certbot-dns-route53
# --non-interactive + --agree-tos are REQUIRED, not optional polish: there is no inbound SSH, so this
# step is delivered over `aws ssm send-command`, which has no TTY. Without them certbot blocks on the
# terms-of-service prompt and the run dies with no certificate. Swap in -m <address> if you want
# expiry mail; --register-unsafely-without-email declines it deliberately.
certbot certonly --non-interactive --agree-tos --register-unsafely-without-email \
  --dns-route53 -d uat.acmp.anas7ammo.dev \
  --deploy-hook /opt/acmp/deploy/scripts/certbot-deploy-hook.sh
systemctl enable --now certbot-renew.timer     # ensure Persistent=true so a stopped box catches up
systemctl is-enabled certbot-renew.timer       # certbot CLAIMS it did this; it does not. Check.
```

> ⚠ **`certbot-deploy-hook.sh` must be executable in git** (`100755`). certbot `exec`s the hook
> directly rather than through an interpreter, and refuses a non-executable one with *"hook command
> deploy exists, but is not executable"* — **aborting the whole `certonly` run**. It was committed
> `100644`, so the first attempt on a fresh box issued no certificate at all. The give-away is that
> the SSM invocation still reports **Success**: without `set -e` the exit status is the *last*
> command's, so `systemctl is-active` returning `active` masks a certbot that never ran. Fixed by
> `git update-index --chmod=+x`; found by the AC-075 rebuild, and only because the check below is
> phrased as "read the subject" rather than "did the command exit 0".

DNS-01 is used because the security group allows **no inbound 80**, so HTTP-01 can never work. The
instance role is scoped to write `<host>`/A **and** `_acme-challenge.<host>`/TXT in one hosted zone —
nothing else. The deploy hook dereferences certbot's symlinks, installs the files 0640 root:101 so
the container's nginx user can read them, runs `nginx -t`, and reloads.

**Verify it worked from OFF the box** — from inside, a placeholder looks identical to a real cert:

```bash
curl -sSI https://uat.acmp.anas7ammo.dev | grep -i strict-transport-security
openssl s_client -connect uat.acmp.anas7ammo.dev:443 -servername uat.acmp.anas7ammo.dev </dev/null \
  | grep -E 'subject=|Verify return code'
certbot renew --dry-run          # on the box — exercises the full DNS-01 challenge
```

If the subject reads `CN=ACMP-PLACEHOLDER-DO-NOT-TRUST`, certbot has not succeeded and the site is
serving the placeholder. That is the failure this naming exists to make impossible to miss.

---

## 7. Seed the committee accounts

```bash
cd /opt/acmp
ACMP_KC_INTERNAL_URL=http://localhost/kc \
  ACMP_ENV_FILE=/opt/acmp/deploy/.env.cloud \
  bash deploy/scripts/seed-users.sh
```

`ACMP_KC_INTERNAL_URL` is **required**: the script's default (`http://localhost:8088/kc`) is the
on-prem published port and does not exist in the cloud topology. Idempotent; every account is
created with a temporary password and `UPDATE_PASSWORD` pending.

---

## 8. Verify the backup schedule

**`08-bootstrap-box.sh` now installs this — there is nothing to do here by hand.** It runs
`crontab -u root deploy/scripts/crontab.example` and prints the installed lines. This step used to say
`crontab -e`, i.e. the daemon was provisioned by the bootstrap while the *schedule* — the only thing
that actually causes a backup — was left to a human reading a runbook. A box rebuilt from the scripts
alone therefore had `crond` running and an empty crontab, which is indistinguishable from a scheduled
box until the day you need a backup. Same family as **DEF-026**.

Confirm it rather than assuming it — the bootstrap prints the table, and on the box:

```bash
crontab -u root -l          # nightly 02:00, Sun-Thu 4-hourly, @reboot backup, @reboot + daily freshness
```

The `@reboot` backup line exists because cron only runs while the instance runs, and this environment
stops the box when idle — so the clock-based slots are missed on almost every session (**OQ-068**,
resolved as option (b)). It sleeps 600s first: `backup.sh` alerts on failure, and firing before SQL
Server is healthy would email impatience rather than a fault. It does **not** deliver NFR-056's 4h RPO
on a stopped-when-idle box; nothing running *on* the box can, since cron cannot fire while the
instance is off. Production is always-on, where the clock-based lines hold on their own.

`crontab.example` also sets `ACMP_ENV_FILE=/opt/acmp/deploy/.env.cloud` at the top — **keep that line.**
It used to be absent, so `backup.sh` fell back to `deploy/.env` (a file the cloud bootstrap never
writes), swallowed the miss, left `ACMP_BACKUP_BUCKET` unset and **silently skipped the off-instance
S3 copy** while still reporting success — backups sharing fate with the box they back up, which is
what NFR-058 exists to prevent. That was **DEF-022**. `backup.sh` now refuses to run instead of
falling through to defaults, and names the skip in the log when there is genuinely no bucket, so the
failure can no longer be silent from either end.

**Verify the copy rather than assuming it** — run `deploy/scripts/backup.sh` once by hand and confirm
objects appear under `s3://<backup-bucket>/sql/`. A cron line that has never been run is not evidence.

One-time prerequisite from [cloud-backup-dr.md](cloud-backup-dr.md): confirm `/opt/acmp/backups`
exists and is owned by uid 10001 — the bootstrap creates it, but verify before trusting a backup.

---

## 9. Verify the environment (the AC evidence)

| Check | Command | Satisfies |
|---|---|---|
| All services healthy | `docker compose -f deploy/docker-compose.cloud.yml --env-file deploy/.env.cloud ps` | AC-075 |
| TLS valid + HSTS | `curl -sSI https://<host>` from off the box | AC-081 |
| Renewal actually works | `certbot renew --dry-run` on the box | AC-081 |
| Stop → start ≤ 10 min | stop the instance, start it, then poll `https://<host>/api/...` from off the box until it answers | AC-082 |
| Webex isolation | `aws ssm get-parameter --name /acmp/uat/env --with-decryption` → `WEBEX_ENABLED=false`, no credentials | AC-083 |
| e2e run + no credit alarm | **warm the box ~2 h first** (see below), then run the suite and confirm `acmp-uat-cpu-credits-low` is `OK` | AC-084 |

### ⚠ Warm the box for ~2 hours before any e2e run (AC-084, OQ-067)

**A STOPPED t3 EARNS NO CPU CREDITS.** The alarm reads `OK` while the instance is stopped only
because a stopped instance publishes no `CPUCreditBalance` datapoints at all — it is not evidence of
a healthy balance, and it flips back to `ALARM` within minutes of a start. A `t3.medium` in
`standard` mode earns 24 credits/hour **only while running**, and the net rate observed on this box
is ~15–18/hour once its own idle draw is subtracted. From a cold start it therefore needs roughly
**two hours** of running time before `acmp-uat-cpu-credits-low` can clear (measured 2026-08-09:
~9 credits at boot → cleared at 50.09 after ~1h50m). A re-bootstrap *spends* credits, so warm up
**after** deploying, not before.

The e2e run itself is nearly free and is **not** what trips the alarm: measured `CPUCreditUsage`
across the two 5-minute periods covering a full run was **0.426 and 0.498**, against **2.0 credits
earned** in the same window — about a quarter of what the box earns while running it. The balance
*rose* during the run. So the only thing that ever trips this alarm is cold-start warm-up.

**Do not lower the threshold to make it green.** That was considered and rejected twice: it would
rebuild the un-failable check the alarm exists to prevent. The alarm is telling the truth — during
warm-up the box genuinely has no burst headroom and would be throttled to its 20% baseline under
load. The condition is real, bounded, and self-resolving; the warm-up is the cost of the
stop-when-idle operating model that keeps this environment inside its $100/month budget.

**AC-082 must be measured with an EXTERNAL probe that reaches the API**, not with `compose ps` and
not against `/`. `web` can be healthy while `api` is crash-looping — daemon-driven container
restarts do **not** re-evaluate `depends_on` — so a probe of `/` proves only that nginx and TLS are
up. Use a route under `/api/`; any API-originated status (200 or 401) is proof of life, while
502/504 means the stack is not actually back.

**Then stop the instance** if you are not actively using it.

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| `no security group 'acmp-<env>-web'` | `01-network.sh` has not run in this account/region |
| `SSM parameter /acmp/<env>/env not found` | run `09-put-env.sh` first |
| `payload is NNNN B, over the 4096 B Standard-tier limit` | env file has too much content — trim it; do **not** switch to Advanced tier |
| Browser refuses the certificate | still the placeholder — certbot has not succeeded; check `certbot certificates` |
| nginx will not start | almost always the certificate: missing file, or a key the container's UID 101 cannot read |
| Seq restart-looping | `SEQ_FIRSTRUN_ADMINPASSWORDHASH` is not a real Base-64 hash (DEF-020) |
| Search returns nothing, no errors | `AUTO_CLOSE` on a SQL Server Express database (DEF-018) — `sqlserver-init.sh` handles it |
