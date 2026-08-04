#!/usr/bin/env bash
# PH-5 spike gates U3 + U2 (SL-021). Proves the two unproven assumptions behind the cloud
# topology BEFORE any AWS spend. Run locally with Docker Desktop up:
#
#   bash deploy/scripts/spike-cloud-gates.sh
#   bash deploy/scripts/spike-cloud-gates.sh --keep     # leave the stack up for poking
#
# SAFETY — this cannot touch your long-lived dev stack:
#   * isolated compose project name (acmpspike) => its own containers AND its own volumes;
#   * its own generated env file under the OS temp dir, never deploy/.env;
#   * publishes NO host ports (every check runs inside the compose network), so it cannot
#     collide with the dev stack's 1433/8085/8088;
#   * teardown is `down -v` scoped to the acmpspike project only.
# Never run `up --build` against the `acmp` dev project — the SQL volume/password state
# mismatches and the container comes up unhealthy.
#
# U3: SQL Server Express really does carry Full-Text Search + the Arabic word-breaker.
# U2: Keycloak 26 boots in PRODUCTION mode on KC_DB=mssql, imports the realm, and survives a
#     restart with its data intact (AC-077). The browser PKCE leg is stage 3, see the tail.
set -euo pipefail

# WINDOWS/GIT-BASH NOTE: MSYS rewrites Unix-style paths into Windows paths before a native exe
# (docker) sees them. That is REQUIRED for host paths (compose -f / --env-file) but WRONG for
# in-container paths (/opt/mssql-tools18/bin/sqlcmd would become C:/Program Files/Git/opt/...).
# So we do NOT disable conversion globally; only the in-container `docker exec` calls below are
# prefixed with MSYS_NO_PATHCONV=1 (a no-op on Linux/macOS).

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PROJECT=acmpspike
CF="$ROOT/deploy/docker-compose.cloud.yml"
ENV_FILE="${TMPDIR:-/tmp}/acmp-spike.env"
KEEP=0; [ "${1:-}" = "--keep" ] && KEEP=1
PASS=0; FAIL=0

log()  { printf '\n\033[1m[spike] %s\033[0m\n' "$*"; }
ok()   { PASS=$((PASS+1)); printf '  \033[32mPASS\033[0m %s\n' "$*"; }
bad()  { FAIL=$((FAIL+1)); printf '  \033[31mFAIL\033[0m %s\n' "$*"; }
dc()   { docker compose -p "$PROJECT" -f "$CF" --env-file "$ENV_FILE" "$@"; }
SA_PW=""   # populated right after the spike env file is written, below
# exec by resolved container id with plain `docker exec` (no compose host-path args), so we can
# safely disable MSYS path conversion for the in-container command target.
dex()  { local svc="$1"; shift; MSYS_NO_PATHCONV=1 docker exec -i "$(dc ps -q "$svc")" "$@"; }
sq()   { # run a query as sa inside the sqlserver container; prints the bare scalar
  # Strip sqlcmd's informational "Changed database context/language" lines that a USE emits,
  # or they pollute scalar reads (e.g. a COUNT parsed as "Changed database context...\n1").
  dex sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
    -P "$SA_PW" -C -No -h -1 -W -Q "SET NOCOUNT ON; $1" 2>/dev/null \
    | tr -d '\r' | sed -e '/^Changed database context/d' -e '/^Changed language/d' -e '/^$/d'
}

cleanup() {
  if [ "$KEEP" = "1" ]; then
    log "--keep: leaving the $PROJECT stack running. Tear down with:"
    printf '  docker compose -p %s -f %s --env-file %s down -v\n' "$PROJECT" "$CF" "$ENV_FILE"
  else
    log "tearing down the isolated $PROJECT stack (volumes included)"
    dc down -v --remove-orphans >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

docker info >/dev/null 2>&1 || { echo "Docker daemon is not running — start Docker Desktop first."; exit 1; }

# --- spike env: local, http, no S3 needed for these gates --------------------------------
log "writing spike env -> $ENV_FILE"
cat > "$ENV_FILE" <<'ENV'
ACMP_REGISTRY=acmp
ACMP_IMAGE_TAG=spike
AWS_REGION=us-east-1
KEYCLOAK_HOSTNAME=http://localhost:8088/kc
KEYCLOAK_AUTHORITY=http://localhost:8088/kc/realms/acmp
KEYCLOAK_ORIGIN=http://localhost:8088
KC_BOOTSTRAP_ADMIN_USERNAME=admin
KC_BOOTSTRAP_ADMIN_PASSWORD=SpikeKC_2026#x
ACMP_KC_DB_NAME=keycloak
ACMP_KC_DB_USER=keycloak_svc
ACMP_KC_DB_PASSWORD=SpikeKCDB_2026#x
MSSQL_SA_PASSWORD=SpikeStrong_2026#x
ACMP_DB_NAME=Acmp
ACMP_DB_USER=acmp_svc
ACMP_DB_PASSWORD=SpikeSvc_2026#x
ACMP_DB_TRUSTCERT=True
ACMP_S3_ENDPOINT=s3.us-east-1.amazonaws.com
ACMP_S3_BUCKET=acmp-spike
ACMP_S3_ACCESS_KEY=spike
ACMP_S3_SECRET_KEY=spike
SEQ_FIRSTRUN_ADMINPASSWORDHASH=unused-in-spike
ACMP_REQUIRE_HTTPS_METADATA=false
WEBEX_ENABLED=false
ACTION_REMINDERS_SWEEP_CRON="0 6 * * *"
ENV
# The backup dir must be visible to BOTH sides: the container writes the .bak (as uid 10001) and the
# host-side scripts then list it. A bare /tmp path is NOT that on Docker Desktop — it resolves inside the
# Linux VM, so the host never sees the file. Use a repo-local, Docker-shared path, world-writable because
# the mssql user owns neither it nor this shell.
if command -v cygpath >/dev/null 2>&1; then SPIKE_BAK="$(cygpath -m "$ROOT")/.spike-backups"; else SPIKE_BAK="$ROOT/.spike-backups"; fi
mkdir -p "$SPIKE_BAK"; chmod 0777 "$SPIKE_BAK" 2>/dev/null || true
echo "ACMP_BACKUP_DIR=$SPIKE_BAK" >> "$ENV_FILE"
SA_PW="$(grep '^MSSQL_SA_PASSWORD=' "$ENV_FILE" | cut -d= -f2-)"
# Secrets are file-backed (ADR-0032) and the compose file resolves them relative to deploy/,
# so the spike must write into deploy/secrets. It is backed up and restored on exit.
# deploy/.env is NOT touched: gen-secrets honours ACMP_ENV_FILE.
printf '\n\033[33m[spike] NOTE: deploy/secrets/ is temporarily rewritten with spike values and\n'
printf '        restored on exit. Your deploy/.env is untouched. If your dev stack is running,\n'
printf '        do not restart it until this finishes.\033[0m\n'
BK="$(mktemp -d)"; cp -a "$ROOT/deploy/secrets/." "$BK/" 2>/dev/null || true
restore_secrets() { cp -a "$BK/." "$ROOT/deploy/secrets/" 2>/dev/null || true; }
trap 'cleanup; restore_secrets' EXIT
ACMP_ENV_FILE="$ENV_FILE" sh "$ROOT/deploy/scripts/gen-secrets.sh" >/dev/null

# =========================================================================================
log "U3 — SQL Server Express + Full-Text Search + Arabic word-breaker"
dc build sqlserver >/dev/null
dc up -d sqlserver >/dev/null
printf '  waiting for SQL Server'
for i in $(seq 1 40); do
  st="$(docker inspect -f '{{.State.Health.Status}}' "$(dc ps -q sqlserver)" 2>/dev/null || echo starting)"
  [ "$st" = "healthy" ] && { printf ' healthy\n'; break; }
  printf '.'; sleep 5
done
[ "$st" = "healthy" ] || { bad "SQL Server never became healthy"; exit 1; }

edition="$(sq "SELECT CAST(SERVERPROPERTY('Edition') AS nvarchar(128));")"
case "$edition" in *Express*) ok "edition is Express (DEF-014): $edition";; *) bad "expected Express, got: $edition";; esac

fts="$(sq "SELECT CAST(SERVERPROPERTY('IsFullTextInstalled') AS int);")"
[ "$fts" = "1" ] && ok "IsFullTextInstalled = 1 under Express" || bad "IsFullTextInstalled = '$fts' (expected 1)"

ar="$(sq "SELECT COUNT(*) FROM sys.fulltext_languages WHERE lcid = 1025;")"
[ "${ar:-0}" -ge 1 ] && ok "Arabic word-breaker present (LCID 1025)" || bad "Arabic word-breaker (LCID 1025) missing"

# End-to-end FTS proof: index an Arabic row and match it with CONTAINS. Each DDL step is its
# OWN batch (a full-text catalog/index cannot be created in the same batch that defines the
# table), and the PK carries a NAMED constraint so KEY INDEX has a known name.
sq "IF DB_ID('ftsspike') IS NULL CREATE DATABASE ftsspike;" >/dev/null
sq "USE ftsspike; IF OBJECT_ID('dbo.t') IS NULL CREATE TABLE dbo.t (id int NOT NULL CONSTRAINT pk_t PRIMARY KEY, body nvarchar(400));" >/dev/null
sq "USE ftsspike; IF NOT EXISTS (SELECT 1 FROM dbo.t) INSERT INTO dbo.t VALUES (1, N'قرار لجنة الهندسة المعمارية بشأن النظام');" >/dev/null
cat_err="$(sq "USE ftsspike; IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name='ftc') CREATE FULLTEXT CATALOG ftc AS DEFAULT;" 2>&1)"
idx_err="$(sq "USE ftsspike; IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('dbo.t')) CREATE FULLTEXT INDEX ON dbo.t(body LANGUAGE 1025) KEY INDEX pk_t WITH CHANGE_TRACKING AUTO;" 2>&1)"
idx_cnt="$(sq "USE ftsspike; SELECT COUNT(*) FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('dbo.t');")"
if [ "${idx_cnt:-0}" -ge 1 ]; then
  ok "Arabic full-text index created on dbo.t"
  # This poll carried two defects that together made U3 fail 4/4 on 2026-08-03 with no diagnosis.
  # (1) It called FULLTEXTCATALOGPROPERTY WITHOUT `USE ftsspike`. sq() connects with no -d, so the
  #     query ran in master, where catalog 'ftc' does not exist — full-text catalogs are scoped to
  #     the current database (CREATE FULLTEXT CATALOG: "unique among all catalog names in the
  #     current database"). The function returned NULL, `${pst:-1}` masked NULL as "still
  #     populating", the loop never broke, and the wait degenerated into a blind 45s sleep that
  #     never observed the crawl at all. The CONTAINS below then simply RACED the populate — which
  #     is why this passed on a fast/idle machine and failed on a loaded one. Not flaky: unobserved.
  # (2) PopulateStatus is deprecated, and Microsoft warns that looping on the CATALOG-level property
  #     "takes CPU cycles away from the database and full-text search processes, and causes
  #     timeouts", directing callers to the TABLE-level property instead. So we now ask
  #     OBJECTPROPERTYEX(...,'TableFulltextPopulateStatus') — right database, right property.
  # A NULL/empty read is now LOUD ('!') instead of being indistinguishable from "busy", and a
  # crawl that never goes idle is reported as such rather than silently handing CONTAINS a race.
  # (3) Waiting on "populate status is idle" ALONE is itself a race, and a nastier one: status 0
  #     means "no population in progress", which is also true BEFORE the crawl has started, and
  #     CHANGE_TRACKING AUTO queues the crawl asynchronously — the DDL above returns before the
  #     FTS host picks up the work. Breaking on 0 could therefore exit on the first iteration in
  #     under a second and hand CONTAINS the same race, which on a fast machine is WORSE than the
  #     45s sleep it replaced. Gate on the row actually being INDEXED instead: TableFulltextItemCount
  #     is "the number of rows that were successfully full-text indexed". Idle AND >=1 item, or keep
  #     waiting. `0:0` (quiet but nothing indexed) is the not-started-yet state and is NOT success.
  printf '  waiting for the full-text populate crawl'
  pst=""
  for i in $(seq 1 40); do
    pst="$(sq "USE ftsspike; SELECT CONCAT(OBJECTPROPERTYEX(OBJECT_ID('dbo.t'),'TableFulltextPopulateStatus'),':',OBJECTPROPERTYEX(OBJECT_ID('dbo.t'),'TableFulltextItemCount'));")"
    case "$pst" in
      0:0)             printf ',' ;;                        # idle but empty = crawl not started yet
      0:[1-9]*)        printf ' indexed\n'; break ;;        # idle AND the row is searchable
      ''|*NULL*)       printf '!' ;;                        # wrong DB / missing object — LOUD
      *)               printf '.' ;;                        # populating
    esac
    sleep 3
  done
  case "$pst" in
    0:[1-9]*) : ;;
    *) printf '\n'; bad "populate crawl never indexed the row (last status:itemcount = ${pst:-<empty>}); the CONTAINS below is racing it" ;;
  esac
  hit="$(sq "USE ftsspike; SELECT COUNT(*) FROM dbo.t WHERE CONTAINS(body, N'الهندسة');")"
  case "$hit" in
    ''|*[!0-9]*) bad "Arabic CONTAINS() query errored: ${hit:-<empty>}" ;;
    *) [ "$hit" -ge 1 ] && ok "Arabic CONTAINS() matched the indexed row (hit=$hit)" \
                        || bad "Arabic CONTAINS() returned 0 rows (populate may be slow)" ;;
  esac
else
  bad "full-text index was not created — catalog err: ${cat_err:-none} | index err: ${idx_err:-none}"
fi

# =========================================================================================
log "U2 — Keycloak 26 production mode on KC_DB=mssql"
# Block until a one-shot container has exited before asserting on its effects.
wait_oneshot() {
  for _ in $(seq 1 40); do
    s="$(docker inspect -f '{{.State.Status}}' "$(dc ps -aq "$1" | head -1)" 2>/dev/null || echo missing)"
    [ "$s" = "exited" ] && return 0; sleep 2
  done; return 1
}
# `up keycloak` runs its dependency sqlserver-init (which creates the keycloak DB + guarded
# RCSI) first, then Keycloak itself. RCSI is asserted below, once the DB provably exists.
dc up -d keycloak >/dev/null
printf '  waiting for Keycloak (production mode, mssql)'
kst=starting
for i in $(seq 1 48); do
  kst="$(docker inspect -f '{{.State.Health.Status}}' "$(dc ps -q keycloak)" 2>/dev/null || echo starting)"
  [ "$kst" = "healthy" ] && { printf ' healthy\n'; break; }
  printf '.'; sleep 5
done
if [ "$kst" = "healthy" ]; then
  ok "Keycloak reached healthy in production mode on SQL Server"
else
  bad "Keycloak never became healthy — logs:"; dc logs --tail 40 keycloak; exit 1
fi

# RCSI is now reliable: sqlserver-init ran (as keycloak's dependency) and the DB exists.
# sys.databases, not DATABASEPROPERTYEX — the latter has no such property and returns NULL.
rcsi="$(sq "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = 'keycloak';")"
[ "$rcsi" = "1" ] && ok "keycloak DB has READ_COMMITTED_SNAPSHOT ON (the fixed guard fired)" || bad "RCSI = '$rcsi' (expected 1)"

# Realm imported + schema really landed in SQL Server (not an embedded H2 fallback).
tables="$(sq "SELECT COUNT(*) FROM keycloak.INFORMATION_SCHEMA.TABLES;")"
[ "${tables:-0}" -gt 50 ] && ok "Keycloak schema is in SQL Server ($tables tables)" \
                          || bad "keycloak DB has only '$tables' tables — schema may not be on mssql"
realm="$(sq "SELECT COUNT(*) FROM keycloak.dbo.REALM WHERE NAME = 'acmp';")"
[ "${realm:-0}" = "1" ] && ok "realm 'acmp' imported" || bad "realm 'acmp' not found (got '$realm')"

# The load-bearing idempotency test: re-run sqlserver-init NOW, while Keycloak's connection
# pool is live against the keycloak DB. This is the exact condition under which an UNGUARDED
# `ALTER DATABASE ... SET READ_COMMITTED_SNAPSHOT ON` would block/deadlock (it needs exclusive
# access) and hang every redeploy. The DATABASEPROPERTYEX guard must make it a clean no-op.
log "U2 — sqlserver-init idempotency with Keycloak connected (the RCSI guard)"
dc up -d --force-recreate sqlserver-init >/dev/null 2>&1 || true
if wait_oneshot sqlserver-init; then
  ec2="$(docker inspect -f '{{.State.ExitCode}}' "$(dc ps -aq sqlserver-init | head -1)" 2>/dev/null || echo 1)"
  [ "$ec2" = "0" ] && ok "init re-ran cleanly under a live Keycloak connection (guard held)" \
                   || bad "init re-run exited $ec2 under a live connection"
else
  bad "init re-run HUNG under a live Keycloak connection — the RCSI guard is not working"
fi

# AC-077: a restart must preserve realm + users.
log "U2 — restart persistence (AC-077)"
dc restart keycloak >/dev/null
for i in $(seq 1 48); do
  kst="$(docker inspect -f '{{.State.Health.Status}}' "$(dc ps -q keycloak)" 2>/dev/null || echo starting)"
  [ "$kst" = "healthy" ] && break; sleep 5
done
realm2="$(sq "SELECT COUNT(*) FROM keycloak.dbo.REALM WHERE NAME = 'acmp';")"
[ "$kst" = "healthy" ] && [ "${realm2:-0}" = "1" ] && ok "realm survived a Keycloak restart" \
  || bad "realm did not survive restart (healthy=$kst realm=$realm2)"

# OIDC discovery through Keycloak itself (the nginx /kc/ hop is exercised in stage 3).
iss="$(dex keycloak sh -c 'exec 3<>/dev/tcp/127.0.0.1/8080; printf "GET /realms/acmp/.well-known/openid-configuration HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n" >&3; cat <&3' 2>/dev/null | tr ',' '\n' | grep -o '"issuer":"[^"]*"' | head -1)"
[ -n "$iss" ] && ok "OIDC discovery served: $iss" || bad "OIDC discovery document not served"

# =========================================================================================
# U4 (P23) — the backup/restore drill, on the database whose restore path is NEW. Keycloak used to be
# backed up with pg_dump and restored with psql into a container that ADR-0036 deleted; it is now just
# another SQL Server database. Three things are proved here without any AWS involvement:
#   * BACKUP works on EXPRESS with no WITH COMPRESSION (the clause the old script used, which Express rejects);
#   * restore.sh really replaces the database contents (3 -> 0 -> 3, AC-080's local half);
#   * Keycloak comes back HEALTHY afterwards, i.e. the restored database is usable, not merely present.
# A probe table is used rather than deleting realm rows: it measures the restore without fighting Keycloak's
# own foreign keys, and it is dropped with the isolated stack.
log "U4 — backup + restore drill on the keycloak database (Express, no COMPRESSION)"
# WINDOWS: backup.sh/restore.sh run IN-CONTAINER paths (/opt/mssql-tools18/bin/sqlcmd). Under MSYS those are
# rewritten to C:/Program Files/Git/opt/... before docker sees them — the first drill run failed with
# `sh: C:/Program: not found`, not because of anything in the scripts. Turn conversion off for these
# invocations and hand compose Windows-form HOST paths so -f/--env-file still resolve. No-op on Linux, which
# is where these scripts actually run in production.
if command -v cygpath >/dev/null 2>&1; then
  CF_H="$(cygpath -w "$CF")"; EF_H="$(cygpath -w "$ENV_FILE")"
else
  CF_H="$CF"; EF_H="$ENV_FILE"
fi
DRILL_ENV=(env MSYS_NO_PATHCONV=1 COMPOSE="docker compose -p $PROJECT -f $CF_H --env-file $EF_H"
           ACMP_ENV_FILE="$ENV_FILE" ACMP_BACKUP_DIR="$SPIKE_BAK" ACMP_DB_NAMES="keycloak")

sq "IF OBJECT_ID('keycloak.dbo.acmp_restore_probe') IS NOT NULL DROP TABLE keycloak.dbo.acmp_restore_probe;
    CREATE TABLE keycloak.dbo.acmp_restore_probe(id int);
    INSERT INTO keycloak.dbo.acmp_restore_probe(id) VALUES (1),(2),(3);" >/dev/null
n0="$(sq "SELECT COUNT(*) FROM keycloak.dbo.acmp_restore_probe;")"
[ "${n0:-0}" = "3" ] && ok "probe seeded: 3 rows" || bad "probe seed failed (got ${n0:-none})"

DRILL_LOG="${TMPDIR:-/tmp}/acmp-spike-drill.log"
if "${DRILL_ENV[@]}" bash "$ROOT/deploy/scripts/backup.sh" >"$DRILL_LOG" 2>&1; then
  ok "backup.sh produced a .bak on Express with no COMPRESSION clause"
else
  bad "backup.sh failed on Express — check the BACKUP statement"; sed -n '1,12p;$p' "$DRILL_LOG" | sed 's/^/      | /'
fi

sq "DELETE FROM keycloak.dbo.acmp_restore_probe;" >/dev/null
n1="$(sq "SELECT COUNT(*) FROM keycloak.dbo.acmp_restore_probe;")"
[ "${n1:-x}" = "0" ] && ok "probe emptied: 0 rows (the state a restore must undo)" || bad "probe not emptied (got ${n1:-none})"

if "${DRILL_ENV[@]}" bash "$ROOT/deploy/scripts/restore.sh" >"$DRILL_LOG" 2>&1; then
  n2="$(sq "SELECT COUNT(*) FROM keycloak.dbo.acmp_restore_probe;")"
  [ "${n2:-0}" = "3" ] && ok "restore.sh restored the database: 3 -> 0 -> 3" \
                       || bad "restore.sh did not restore the rows (got ${n2:-none})"
else
  bad "restore.sh failed"; sed -n '1,12p;$p' "$DRILL_LOG" | sed 's/^/      | /'
fi

# The row count says the FILES came back. Only a healthy Keycloak says the DATABASE is usable.
for i in $(seq 1 48); do
  kst4="$(docker inspect -f '{{.State.Health.Status}}' "$(dc ps -q keycloak)" 2>/dev/null || echo starting)"
  [ "$kst4" = "healthy" ] && break; sleep 5
done
[ "$kst4" = "healthy" ] && ok "Keycloak healthy again after its database was restored" \
                        || bad "Keycloak did not come back healthy after restore (state=$kst4)"

# =========================================================================================
printf '\n\033[1m[spike] RESULT: %d passed, %d failed\033[0m\n' "$PASS" "$FAIL"
cat <<'NOTE'
Still MANUAL (stage 3) — the browser PKCE leg of U2, which needs the web image + nginx:
  docker compose -p acmpspike -f deploy/docker-compose.cloud.yml --env-file <spike env> \
    up -d --build web
  cd src/Acmp.Web && E2E_WEB_URL=http://localhost:8088 \
    E2E_KEYCLOAK_URL=http://localhost:8088/kc npx playwright test auth.spec.ts
(That leg also needs ACMP_HTTP_PORT=8088 in the spike env so `web` publishes a host port.)
NOTE
[ "$FAIL" -eq 0 ]
