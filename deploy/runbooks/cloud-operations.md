# Cloud operations runbook — running the two AWS environments (PH-5)

Day-to-day operation of an environment that **already exists**: starting and stopping it, deploying
a new commit to it, proving the deploy landed, and reading the alerts it sends.

> **Scope.** Building a box from nothing is [cloud-provisioning.md](cloud-provisioning.md). Backup,
> restore and rollback are [cloud-backup-dr.md](cloud-backup-dr.md). The on-prem runbook
> ([README.md](README.md)) is a **different topology** — its `up.sh` / `docker-compose.prod.yml` /
> `promote.sh` commands are the wrong ones for an EC2 box.

> **Who runs these.** An operator from the `acmp-admin` IAM identity, **never as root**. Root is not
> a convenience here: the 100%-of-budget IAM-deny brake attaches to `acmp-admin`, and **root cannot
> be restricted by IAM policy at all**, so work done as root bypasses the spend guardrail entirely.
> `acmp-admin` holds static keys and needs no interactive login.

---

## 0. The two environments

| | Prod | UAT |
|---|---|---|
| Instance | `i-04d9717feea79204b` | `i-07ac28ac2fedab921` |
| Hostname | `acmp.anas7ammo.dev` | `uat.acmp.anas7ammo.dev` |
| Operating model | **always-on** | **stopped when idle** |
| Backup max age | 26 h | 168 h |

**They differ on purpose.** Separate buckets, passwords and IAM keys are what make the AC-083
isolation test meaningful rather than circular, and prod is deliberately *not* stop-when-idle so the
cron backup schedule is correct by design rather than by OQ-068's `@reboot` workaround. **Do not
harmonise them.**

⚠ **Instance ids change when a box is replaced** — the previous UAT box (`i-05085d458d886dc08`) no
longer exists. Read them live rather than from this table:

```bash
export AWS_PROFILE=acmp-admin AWS_PAGER=""
aws ec2 describe-instances --region us-east-1 \
  --filters "Name=tag:Project,Values=ACMP" \
  --query 'Reservations[].Instances[].{Id:InstanceId,Name:Tags[?Key==`Name`]|[0].Value,State:State.Name}' \
  --output table
```

---

## 1. Start and stop an environment

UAT's whole operating model is stop-when-idle, and until now these commands existed nowhere in the
repo — every session rediscovered them.

### Start

```bash
export AWS_PROFILE=acmp-admin AWS_PAGER="" ID=i-07ac28ac2fedab921

aws ec2 start-instances --region us-east-1 --instance-ids "$ID"
aws ec2 wait instance-running --region us-east-1 --instance-ids "$ID"

# instance-running is NOT ready: it returns while the box is still booting. Wait for the SSM agent,
# which is the first thing that proves you can actually reach it.
until aws ssm describe-instance-information --region us-east-1 \
        --filters "Key=InstanceIds,Values=$ID" \
        --query 'InstanceInformationList[0].PingStatus' --output text 2>/dev/null | grep -q Online
do sleep 10; done
```

**The stack comes back on its own — there is nothing to re-run.** `08-bootstrap-box.sh` did
`systemctl enable --now docker`, every long-lived service carries `restart: unless-stopped`, and ECR
auth uses the **credential helper** rather than a 12-hour `docker login` token precisely so a restart
past that window can still pull. The one-shots (`sqlserver-init`, `db-migrate`, `keycloak-config`)
are `restart: "no"` and correctly do not re-run. Surviving a stop/start is what **AC-082** asserts.

Confirm from outside rather than assuming — a container can be up while the API crash-loops:

```bash
bash deploy/scripts/smoke.sh uat.acmp.anas7ammo.dev
```

> ⚠ **Warm the box for ~2 hours before any e2e run** (AC-084, OQ-067). A freshly started t3.medium
> has no CPU-credit burst headroom, and `CpuCredits=standard` throttles rather than bills. A
> regression run against a cold box fails on timing and tells you nothing about the code.

### Stop

```bash
aws ec2 stop-instances --region us-east-1 --instance-ids "$ID"
aws ec2 wait instance-stopped --region us-east-1 --instance-ids "$ID"
```

Stopped, UAT still costs ~$7.65/mo for EBS and the Elastic IP — that is expected and is why the
address stays allocated. **Never stop prod to save money**: it is always-on so the clock-based cron
backup slots actually fire.

---

## 2. Deploy a commit

CI publishes images **on push to `main` only**. The sequence, proven on `1c7f2ba`:

```bash
SHA=1c7f2ba...            # the FULL 40-char sha; CI tags with the full one
ENV=prod                  # or uat

# 1. PROVE the images exist before re-pinning anything (DEF-019).
for repo in api worker sqlserver-fts; do
  aws ecr describe-images --region us-east-1 --repository-name "acmp/$repo" \
    --image-ids imageTag="$SHA" --query 'imageDetails[0].imagePushedAt' --output text
done
aws ecr describe-images --region us-east-1 --repository-name acmp/web \
  --image-ids imageTag="$SHA-$ENV" --query 'imageDetails[0].imagePushedAt' --output text

# 2. Re-pin the environment: edit the env file so
#      ACMP_IMAGE_TAG=<full-sha>   ACMP_WEB_TAG=<full-sha>-$ENV
bash deploy/aws/09-put-env.sh "$ENV" <path-to-env-file>

# 3. Re-bootstrap.
bash deploy/aws/08-bootstrap-box.sh "$ENV" "$SHA"
```

`web` is **not** promotable by digest and that is not a defect: ADR-0037 bakes
`VITE_OIDC_AUTHORITY` into the bundle at build time, so the UAT and prod web images are different
artefacts built from the same commit. Promoting web means selecting the `<sha>-prod` build, never
re-pointing the `<sha>-uat` one — get it wrong and production authenticates against UAT, which fails
in the browser with **nothing in the API logs**.

`deploy/scripts/promote-image.sh <uat|prod> <commit-sha>` does step 1 and 2 together and prints the
digests, so the identity of what shipped is auditable.

### When the drift guard trips

`08-bootstrap-box.sh` refuses if the SSM parameter's pinned tags do not prefix-match the sha you
asked it to deploy. The box checks out the sha you pass but **runs whatever the parameter pins**, so
without the guard it deploys the wrong build silently and every health check still passes.

**The guard should pass on its own. Reaching for `ACMP_ALLOW_TAG_DRIFT=1` means stop and re-read**
— it is for a deliberate rollback to older images, nothing else. Normally the fix is that you
skipped step 2: re-publish the environment with this commit's tags.

---

## 3. Prove the deploy actually landed

**All-healthy proves nothing about what shipped.** A stale bundle serves perfectly. Grade the
**served artefact**, not the stack:

```bash
curl -s https://acmp.anas7ammo.dev/ | grep -o '/assets/[^"]*\.js'      # find the hashed bundle
curl -sI "https://acmp.anas7ammo.dev/assets/<file>.js?cb=$(date +%s)"  # Last-Modified ~= CI build time
```

Then fetch that bundle cache-busted and run the **same assert-zero pattern the repo gate uses** for
whatever the release was meant to change. That — not a healthy container — is what proved the
Arabic rename (`DEC-032`) was live.

Backup schedule, after any rebuild — compare mechanically, not by eye:

```bash
diff <(crontab -u root -l) /opt/acmp/deploy/scripts/crontab.example
```

A rebuilt box once had `crond` running and an **empty crontab**, which is indistinguishable from a
scheduled box until the day you need a backup (DEF-026 family). `08-bootstrap-box.sh` installs the
schedule itself now; the `diff` is how you confirm it.

---

## 4. Reaching the Keycloak admin console

There is no inbound SSH and no inbound 80. Port-forward over SSM **to box port 80**:

```bash
export PATH="$PATH:/c/Program Files/Amazon/SessionManagerPlugin/bin"   # POSIX form — a Windows
                                          # path splits at the drive-letter colon and vanishes
aws ssm start-session --region us-east-1 --target "$ID" \
  --document-name AWS-StartPortForwardingSession \
  --parameters '{"portNumber":["80"],"localPortNumber":["8081"]}'
# then browse http://localhost:8081/kc/admin
```

**Port 80, not 8443.** The 443 listener returns 404 for `/kc/admin` and `/kc/realms/master` **by
design** — AC-081 rests on that deny — while the internal 8080 block has none. **Never relax the 443
deny to make this easier.**

---

## 5. Alerts — which ones are correct

After a stop/start, **two alerts fire and both are telling the truth**:

| Alert | Why it is correct |
|---|---|
| Backup freshness | The box was off longer than `ACMP_BACKUP_MAX_AGE_HOURS`. Cron cannot fire while an instance is stopped. |
| CPU credits low | A cold t3.medium genuinely has no burst headroom, for ~2 h. Self-resolving. |

> ⚠ **Do not tune either threshold.** Lowering them has been proposed and **rejected twice**: it
> rebuilds the un-failable check the alarm exists to catch. The condition is real, bounded and
> self-resolving — wait it out.

The account also carries an unrelated `My Monthly Cost Budget` at $10 that alerts long before the
ACMP brake. **Do not read its alerts as the brake firing.** The ACMP budget is $100/mo with
notifications at 50/80/100% and both actions (IAM-deny, EC2-stop) at 100% ACTUAL — *actual* spend,
so they fire on realised cost, not a forecast.

To read what a budget notification actually delivered, rather than counting that something arrived:

```bash
bash deploy/scripts/check-budget-notification.sh
```

It reads the message **body**. On a shared SNS topic a count can never discriminate between one
alert and another (AV-118).

### The container-health alert — the one you have to create in Seq

`deploy/scripts/check-container-health.sh` runs from cron every 15 minutes (see
`deploy/scripts/crontab.example`) and answers the question the boot assertions cannot: **is anything
sitting unhealthy right now?** `up.sh`'s `up -d --wait` and `08-bootstrap-box.sh`'s `wait_healthy` both
consume the healthcheck's verdict — but only at start-up. After that nothing watches it, because
`DEF-079` measured what `condition: service_healthy` costs (a 30-second blip in SQL Server, Hangfire or
object storage stops a dependent from starting at all), put it to the operator, and the decision was to
**keep the signal and drop the gate**. This check is the signal's reader; it runs beside the stack and
cannot fail it.

When it finds an unhealthy container it POSTs one CLEF event to Seq:

| Field | Value |
|---|---|
| `EventType` | **`Deploy.ContainerUnhealthy`** |
| `@l` | `Error` |
| `Unhealthy` | the service names and their states, e.g. `api(unhealthy)` |
| `Checked` | how many containers declaring a healthcheck were inspected |
| `Host` | the box's hostname |

**Create the Seq signal on `EventType = 'Deploy.ContainerUnhealthy'`, not on message text.** The event
type is a stable key; the message is prose and will be reworded. Set the alert to notify however this
environment already routes Seq alerts.

> ⚠ **The Seq alert rule is NOT in this repository and cannot be.** Seq is provisioned here as a
> container with a first-run password and nothing else, so there is no versioned alert definition to
> review or diff — this table is the only record of what to key on. That is exactly the *configuration
> no gate can see* class (`DEF-078`, `DEF-079`), which is why the DETECTION lives in a committed script
> with a forced-refusal suite (`deploy/scripts/check-container-health.test.sh`, run by CI) and only the
> NOTIFICATION is configuration.

The script additionally publishes to `ACMP_ALERT_TOPIC_ARN` when one is set, and says loudly in its log
when it is not — on-prem that is expected, in cloud it is a finding. It exits non-zero when a container
is unhealthy: unlike backup staleness, which is legitimately expected on a box that is stopped when
idle, a container sitting unhealthy while the box is up is never an expected steady state.

---

## 6. Rolling back

A bad deploy with an intact database is an image re-point, not a restore — §2 with the previous sha,
or `promote-image.sh`. Roll the **database** back only if the release ran migrations, which means
`restore.sh` from a pre-deploy backup: see
[cloud-backup-dr.md](cloud-backup-dr.md#application-rollback-bad-deploy-database-intact).

---

## 7. What not to do

- **Do not point the Playwright suite at production.** `e2e/global-setup.ts` refuses a prod host by
  design: the suite seeds fixed-password accounts and writes governance rows, and ACMP audit events
  are hash-chained and append-only (INV-005) while a member can be deactivated but **never deleted**
  (DEF-029). Nothing it writes to a system of record can be undone. Use `smoke.sh` for prod.
- **Do not run `deploy/scripts/up.sh` or `promote.sh` on a cloud box.** Those are the on-prem
  topology. `scripts/check-runbook-drift.mjs` fails CI if a cloud runbook starts recommending them
  again, and if the budget figures here drift from `deploy/aws/_common.sh`.
- **Do not deploy as root** — see the header.
