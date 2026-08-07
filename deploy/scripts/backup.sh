#!/usr/bin/env bash
# ACMP nightly backup (P18b ADR-0033; re-shaped for the cloud topology in P23 / PH-5). Runs on the HOST via
# cron against the running stack. Backs up EVERY SQL Server database that holds state — Acmp AND keycloak,
# which since ADR-0036 lives in SQL Server too — then copies the .bak files off-instance to S3 (NFR-058).
#
#   deploy/scripts/backup.sh
#
# Config (env or deploy/.env): ACMP_BACKUP_DIR (host dir, bind-mounted to sqlserver:/backups), ACMP_DB_NAMES
#   (space-separated, default "Acmp keycloak"), ACMP_BACKUP_BUCKET (S3 off-instance copy; skipped if unset),
#   STANDBY_HOST + STANDBY_DIR (legacy on-prem scp target; skipped if unset), BACKUP_RETENTION_DAYS (30).
#
# WHAT CHANGED IN P23 AND WHY (ADR-0034/0035/0036 superseded the on-prem topology):
#   * WITH COMPRESSION is GONE. Backup compression is an Enterprise/Standard feature; on SQL Server Express
#     — the edition this deployment runs (DEF-014) — it fails the BACKUP outright. Uncompressed .bak files
#     are larger on disk and in S3; that is the price of a free production licence.
#   * The pg_dump leg is GONE: Keycloak no longer has a Postgres of its own, it is a database on the same
#     SQL Server instance, so it is simply another name in the loop below.
#   * The mc-mirror leg is GONE: objects live in S3 (ADR-0035), where bucket VERSIONING is the backup —
#     mirroring a bucket you do not host would be copying AWS's durability onto a 4 GiB box.
# Running against the legacy on-prem stack still works, with its compose files and DB list:
#   COMPOSE="docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml" \
#   ACMP_DB_NAMES=Acmp deploy/scripts/backup.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
# ACMP_ENV_FILE lets a drill point at its own env instead of the operator's (gen-secrets.sh convention),
# so a restore rehearsal can never inherit production values by accident.
ENV_FILE="${ACMP_ENV_FILE:-deploy/.env}"
# DEF-022: this line used to end in `|| true`, and that swallow is what made a cloud backup lie. cron
# sets no ACMP_ENV_FILE, so ENV_FILE defaulted to deploy/.env — a file the cloud bootstrap never
# writes (it writes deploy/.env.cloud). The miss was swallowed, ACMP_BACKUP_BUCKET stayed unset, the
# guard below went false and the off-instance copy was skipped ENTIRELY. The .bak files were still
# written locally, so the run "succeeded" and the cron log was clean — while the backups never left
# the box they were backing up, which is the one thing NFR-058 exists to prevent.
#
# So: refuse rather than fall through to defaults. The escape hatch is narrow and explicit — a caller
# that has genuinely configured everything in the environment already has ACMP_BACKUP_BUCKET set, and
# that is the only case where a missing file is not a misconfiguration.
if [ -f "$ENV_FILE" ]; then
  set -a; . "$ENV_FILE"; set +a
elif [ -z "${ACMP_BACKUP_BUCKET:-}" ]; then
  echo "backup: env file '$ENV_FILE' not found and ACMP_BACKUP_BUCKET is not set — refusing to run a" >&2
  echo "        backup that would silently skip the off-instance copy (NFR-058, DEF-022). Set" >&2
  echo "        ACMP_ENV_FILE to the environment file for this box (cloud: /opt/acmp/deploy/.env.cloud)." >&2
  exit 1
fi

COMPOSE="${COMPOSE:-docker compose -f deploy/docker-compose.cloud.yml}"
BACKUP_DIR="${ACMP_BACKUP_DIR:-/opt/acmp/backups}"
# Every stateful database. Keycloak's realm/users are as load-bearing as the governance data: restoring Acmp
# alone leaves a system nobody can log into (AC-080 checks BOTH).
DB_NAMES="${ACMP_DB_NAMES:-${ACMP_DB_NAME:-Acmp} keycloak}"
RETENTION="${BACKUP_RETENTION_DAYS:-30}"
TS="$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR"

log() { printf '[backup %s] %s\n' "$(date +%H:%M:%S)" "$*"; }

# 0) Preflight the backup mount. SQL Server writes the .bak ITSELF, from inside the container, as
#    uid 10001 (mssql) — not as the user running this script. Docker auto-creates a missing bind source
#    ROOT-OWNED 0755, so BACKUP then dies with the famously unhelpful
#        Cannot open backup device '/backups/x.bak'. Operating system error 5(Access is denied.)
#    at 02:00, having reported nothing wrong until then. Check it up front and say how to fix it.
if ! $COMPOSE exec -T sqlserver sh -c 'touch /backups/.acmp-write-probe && rm -f /backups/.acmp-write-probe' >/dev/null 2>&1; then
  cat >&2 <<EOF
backup: SQL Server (uid 10001) cannot write to /backups — host path: $BACKUP_DIR
        Docker created it root-owned, or it predates this deployment. Fix on the host, once:
          sudo install -d -o 10001 -g 10001 -m 0755 "$BACKUP_DIR"
EOF
  exit 1
fi

# 1) SQL Server — one native .bak per stateful database, into the /backups bind-mount (= host $BACKUP_DIR).
#    CRITICAL: any failure aborts the run (cron alerts on the non-zero exit). BACKUP requires
#    sysadmin/db_backupoperator, so it connects as sa (password from the secret file), NOT as acmp_svc.
#    A database that does not exist is a hard error, not a skip: silently backing up less than asked is
#    exactly how a restore drill discovers there was never a keycloak backup.
for DB in $DB_NAMES; do
  log "SQL: BACKUP DATABASE [$DB] -> /backups/${DB}_${TS}.bak (no COMPRESSION — Express)"
  $COMPOSE exec -T sqlserver sh -c \
    "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"\$(cat /run/secrets/mssql_sa_password)\" -C -No -b -Q \
     \"BACKUP DATABASE [$DB] TO DISK='/backups/${DB}_${TS}.bak' WITH INIT, STATS=10;\""
  log "SQL: done ($(ls -1 "$BACKUP_DIR/${DB}_${TS}.bak" 2>/dev/null || echo 'MISSING'))"
done

# 2) Off-instance copy to S3 (NFR-058 — the copy must not share fate with the instance). Guarded on the
#    bucket name so a local-only run still works. Credentials come from the instance profile (03-iam.sh
#    grants the role s3:PutObject on the backup bucket); no keys are read or echoed here.
#    NOT guarded as "non-fatal": a backup that exists only on the box being backed up is not a backup, so
#    a failed upload fails the run — the local .bak is still on disk for the operator to retry by hand.
if [ -n "${ACMP_BACKUP_BUCKET:-}" ]; then
  if command -v aws >/dev/null 2>&1; then
    for DB in $DB_NAMES; do
      log "S3: cp ${DB}_${TS}.bak -> s3://${ACMP_BACKUP_BUCKET}/sql/"
      aws s3 cp "$BACKUP_DIR/${DB}_${TS}.bak" "s3://${ACMP_BACKUP_BUCKET}/sql/${DB}_${TS}.bak" --only-show-errors
    done
  else
    echo "backup: ACMP_BACKUP_BUCKET is set but the aws CLI is missing — refusing to report success" >&2
    exit 1
  fi
else
  # SAY SO. The skip itself is legitimate (local-only drills), but DEF-022 was invisible precisely
  # because it looked identical to a normal run in the log. A named skip cannot be mistaken for a copy.
  log "NO OFF-INSTANCE COPY: ACMP_BACKUP_BUCKET is unset, so the .bak files exist ONLY on this box (NFR-058 unmet)."
fi

# 3) Legacy on-prem off-box copy to a warm-standby VM. There is no standby VM in the cloud topology (S3
#    above is its replacement), so this stays skipped unless STANDBY_HOST is set. (deployment.md §7)
if [ -n "${STANDBY_HOST:-}" ]; then
  for DB in $DB_NAMES; do
    log "Standby: scp ${DB}_${TS}.bak -> ${STANDBY_HOST}:${STANDBY_DIR:-/opt/acmp/backups}/"
    scp -q "$BACKUP_DIR/${DB}_${TS}.bak" "${STANDBY_HOST}:${STANDBY_DIR:-/opt/acmp/backups}/" \
      || log "Standby: scp FAILED (non-fatal — local backup retained)"
  done
fi

# 4) Retention prune (local disk only). The S3 side is pruned by the bucket's own lifecycle rule
#    (deploy/aws/02-s3.sh) — doing it from here would need s3:DeleteObject on the backup bucket, i.e. a
#    credential on the box that can erase its own off-instance backups.
log "Prune: removing local *.bak older than ${RETENTION}d"
find "$BACKUP_DIR" -maxdepth 1 -type f -name '*.bak' -mtime "+${RETENTION}" -delete || true

log "backup complete: $(for DB in $DB_NAMES; do printf '%s_%s.bak ' "$DB" "$TS"; done)"
