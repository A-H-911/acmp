# Cloud backup, restore & rollback (PH-5 / P23)

Operating procedures for the AWS topology (ADR-0034 EC2 host, ADR-0035 S3 objects, ADR-0036 Keycloak on
SQL Server). The on-prem procedures in [README.md](README.md) still apply to the legacy stack; where the two
differ, this file wins for anything running on EC2.

Covers NFR-056 (RPO ≤ 4h), NFR-057 (RTO ≤ 8h), NFR-058 (off-instance copy), and AC-080.

## What is backed up, and what is not

| Data | Mechanism | Where |
|---|---|---|
| `Acmp` database | nightly + 4-hourly `BACKUP DATABASE` → `.bak` | local `$ACMP_BACKUP_DIR`, copied to `s3://<project>-<env>-backups/sql/` |
| `keycloak` database | same — it is a database on the same instance since ADR-0036 | same |
| Recordings & attachments | **not copied** — S3 bucket versioning + SSE is the backup | `s3://<project>-<env>-recordings` |
| Seq logs, Hangfire state | not backed up — reconstructible / operational only | — |

Two things that trip people coming from the on-prem scripts:

- **No `WITH COMPRESSION`.** Backup compression is an Enterprise/Standard feature. On **Express** — the
  edition this deployment runs on a free production licence (DEF-014) — including that clause fails the
  `BACKUP` outright. `.bak` files are correspondingly larger, on disk and in S3.
- **No `pg_dump`, no `mc mirror`.** Keycloak has no Postgres of its own any more, and mirroring an S3 bucket
  onto a 4 GiB instance would be copying AWS's own durability into the thing you are protecting against.

## Backup

### One-time prerequisite: the backup directory must be writable by uid 10001

**SQL Server writes the `.bak` itself**, from inside its container, as `uid 10001 (mssql)` — not as the user
running cron. If `$ACMP_BACKUP_DIR` does not already exist, Docker creates the bind source **root-owned
`0755`**, and every backup then fails with:

```
Cannot open backup device '/backups/Acmp_….bak'. Operating system error 5(Access is denied.)
```

which surfaces at 02:00, having looked fine until then. Create it correctly, once, before the first run:

```bash
sudo install -d -o 10001 -g 10001 -m 0755 /opt/acmp/backups
```

`backup.sh` now probes this before it starts and refuses with that same remediation line, so a
misconfigured directory fails immediately and loudly instead of silently producing no backups.

### Running it

Installed by cron on the instance (`deploy/scripts/crontab.example`): nightly 02:00 plus 08/12/16/20 on
business days, which is what holds RPO ≤ 4h.

```bash
cd /opt/acmp && deploy/scripts/backup.sh
```

Required in `deploy/.env`: `ACMP_BACKUP_DIR`, `ACMP_BACKUP_BUCKET`, and — only if you want something other
than both databases — `ACMP_DB_NAMES`.

The S3 upload uses the **instance profile**, not a key in the env file. If `ACMP_BACKUP_BUCKET` is set and
the upload fails, **the whole run fails**: a backup that exists only on the box being backed up is not a
backup. The local `.bak` is still on disk to retry by hand.

## Restore (the drill — run it before you need it)

**Destructive.** Overwrites the live databases, and stops `api`, `worker` and `keycloak` while it runs.

```bash
cd /opt/acmp
aws s3 cp s3://<project>-<env>-backups/sql/Acmp_20260803_020000.bak     "$ACMP_BACKUP_DIR/"   # if restoring from S3
aws s3 cp s3://<project>-<env>-backups/sql/keycloak_20260803_020000.bak "$ACMP_BACKUP_DIR/"
deploy/scripts/restore.sh                      # newest .bak per database
deploy/scripts/restore.sh Acmp=/path/a.bak keycloak=/path/k.bak   # or name them explicitly
```

The script resolves **every** backup before it touches **any** database, so a missing file stops the run
instead of leaving you with a new `Acmp` beside a stale `keycloak`.

It stops the dependents first on purpose. `SET SINGLE_USER WITH ROLLBACK IMMEDIATE` on its own is a race:
it evicts current sessions, and the api or Keycloak reconnects within milliseconds and takes the one
permitted single-user slot, leaving `RESTORE` to fail with *database is in use*.

**Verification is not optional and not just the row counts.** The script checks `decisions.decisions` in
`Acmp` and `dbo.REALM` in `keycloak`, then restarts the services. Finish with a real login — that, not a
count, is what proves the Keycloak restore. `AC-080`'s drill is the 3 → 0 → 3 shape on **both** databases;
its local half runs unattended in `deploy/scripts/spike-cloud-gates.sh` (gate U4).

## Application rollback (bad deploy, database intact)

Images are promoted by digest (ADR-0037), so a rollback is a re-point, not a rebuild:

```bash
cd /opt/acmp
deploy/scripts/promote.sh <previous-digest-or-tag>     # or set ACMP_IMAGE_TAG to the previous tag
docker compose -f deploy/docker-compose.cloud.yml up -d
```

Roll the **database** back only if the bad release ran migrations. EF migrations here are forward-only, so
that means `restore.sh` from the pre-deploy backup — take one before any release that carries a migration.

## Keycloak wedged: `DATABASECHANGELOGLOCK`

**Symptom.** Keycloak never becomes healthy and its log repeats *"Waiting for changelog lock..."*.

**Cause.** Keycloak upgrades its schema with Liquibase, which takes a row lock in
`keycloak.dbo.DATABASECHANGELOGLOCK` first. If the container is killed mid-upgrade — OOM, a `docker compose
down` at the wrong moment, an instance stop — the row stays `LOCKED = 1` and every later boot waits on a
holder that no longer exists. It does not time out on its own.

**Fix.** Confirm no Keycloak is actually running, then clear the row:

```bash
cd /opt/acmp
docker compose -f deploy/docker-compose.cloud.yml stop keycloak

docker compose -f deploy/docker-compose.cloud.yml exec -T sqlserver sh -c \
  '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$(cat /run/secrets/mssql_sa_password)" -C -No -Q \
   "SELECT ID, LOCKED, LOCKGRANTED, LOCKEDBY FROM keycloak.dbo.DATABASECHANGELOGLOCK;"'

# Only after confirming LOCKED = 1 with no Keycloak running:
docker compose -f deploy/docker-compose.cloud.yml exec -T sqlserver sh -c \
  '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$(cat /run/secrets/mssql_sa_password)" -C -No -b -Q \
   "UPDATE keycloak.dbo.DATABASECHANGELOGLOCK SET LOCKED = 0, LOCKGRANTED = NULL, LOCKEDBY = NULL WHERE ID = 1;"'

docker compose -f deploy/docker-compose.cloud.yml start keycloak
```

**Check the running container first, every time.** Clearing the lock while a Keycloak really is mid-upgrade
lets a second one start the same migration concurrently, and *that* is how you corrupt the schema rather
than merely wait on it. If Keycloak still fails after the unlock, the interrupted migration left the schema
half-applied: restore the `keycloak` database from the last good `.bak` instead of retrying the unlock.
