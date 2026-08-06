#!/usr/bin/env bash
# ACMP cloud-topology boot gate (PH-5 / SL-021). Brings up the FULL cloud stack -- api, worker and
# web, not just the database and Keycloak the spike covers -- from the images CI actually published,
# and asserts it reaches a working state.
#
#   bash deploy/scripts/cloud-stack-boot.sh <commit-sha>          # pull from ECR (needs AWS auth)
#   bash deploy/scripts/cloud-stack-boot.sh --local               # build locally, no AWS needed
#   KEEP=1 bash deploy/scripts/cloud-stack-boot.sh <sha>          # leave it up for poking
#
# WHY THIS EXISTS AS A COMMITTED SCRIPT RATHER THAN A ONE-OFF
# The cloud topology had NO validation of any kind: CI's compose job checks docker-compose.yml, the
# ON-PREM file, and the spike only ever started sqlserver + keycloak. api/worker/web under
# docker-compose.cloud.yml had never started anywhere -- and the next place they would have started
# was a paid instance. The first run of this found two deploy-blocking defects: DEF-019 (the compose
# requested a web tag CI never publishes, so the deploy would die mid-pull) and DEF-020 (Seq
# crash-looped on the shipped placeholder while api/web/keycloak all reported healthy, because
# `depends_on: condition: service_started` is satisfied by a container that started and then died).
# DEF-018 was likewise found by RE-RUNNING an existing spike. Scripts that live in the repo get
# re-run; scripts that live in a temp folder evaporate, which is the entire argument for this file.
#
# It is an OPERATOR/DEV gate, not a CI gate: it needs Docker and (unless --local) ECR credentials.
# Same posture as spike-cloud-gates.sh.
#
# SAFETY -- it cannot touch the long-lived dev stack:
#   * isolated compose project (acmpboot) => its own containers AND volumes
#   * publishes only high ports (18080/18443), so it cannot collide with dev's 8088
#   * deploy/secrets/ is backed up and restored by an EXIT trap, because the RUNNING dev stack
#     reads those same files; the trap fires on failure and on Ctrl-C, not just on success
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT" || exit 1
PROJECT=acmpboot
REGION="${AWS_REGION:-us-east-1}"
ACCOUNT_ID="${ACMP_ACCOUNT_ID:-565393059398}"
REGISTRY="${ACMP_REGISTRY:-${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com/acmp}"
WORK="${TMPDIR:-/tmp}/acmp-cloud-boot"
ENV_FILE="$WORK/boot.env"
BACKUP="$WORK/secrets-backup"
PASS=0; FAIL=0

log() { printf '\n\033[1m[boot] %s\033[0m\n' "$*"; }
ok()  { PASS=$((PASS+1)); printf '  \033[32mPASS\033[0m %s\n' "$*"; }
bad() { FAIL=$((FAIL+1)); printf '  \033[31mFAIL\033[0m %s\n' "$*"; }
dc()  { docker compose -p "$PROJECT" -f deploy/docker-compose.cloud.yml --env-file "$ENV_FILE" "$@"; }

sha="${1:-}"
[ -n "$sha" ] || { echo "usage: cloud-stack-boot.sh <commit-sha> | --local"; exit 2; }
docker info >/dev/null 2>&1 || { echo "Docker daemon is not running."; exit 1; }
mkdir -p "$WORK"

restore() {
  if [ -d "$BACKUP" ]; then
    rm -rf "$ROOT/deploy/secrets" && cp -r "$BACKUP" "$ROOT/deploy/secrets"
    printf '\n[boot] deploy/secrets restored (the dev stack reads these)\n'
  fi
  [ "${KEEP:-0}" = "1" ] || dc down -v --remove-orphans >/dev/null 2>&1
}
trap restore EXIT

log "backing up deploy/secrets"
rm -rf "$BACKUP"; cp -r "$ROOT/deploy/secrets" "$BACKUP" 2>/dev/null && ok "backed up $(ls "$BACKUP" | wc -l) secret files"

# A real Seq hash, generated rather than placeheld -- DEF-020 is precisely what happens when this is
# a word instead of Base-64, and this gate must exercise the configuration we ship, not a broken one.
log "generating a real Seq admin password hash"
SEQ_HASH="$(printf 'BootSeq_2026#x' | docker run -i --rm datalust/seq config hash 2>/dev/null | tr -d '\r\n')"
[ -n "$SEQ_HASH" ] && ok "Seq hash generated (${#SEQ_HASH} chars)" || bad "could not generate a Seq hash"

if [ "$sha" = "--local" ]; then
  REG_LINE="ACMP_REGISTRY=acmp"; TAG=boot; WEB_TAG=boot
else
  REG_LINE="ACMP_REGISTRY=$REGISTRY"; TAG="$sha"; WEB_TAG="$sha-uat"
fi

cat > "$ENV_FILE" <<ENV
$REG_LINE
ACMP_IMAGE_TAG=$TAG
ACMP_WEB_TAG=$WEB_TAG
AWS_REGION=$REGION
ACMP_HOST=localhost
ACMP_HTTP_PORT=18080
ACMP_HTTPS_PORT=18443
KEYCLOAK_HOSTNAME=http://localhost:18080/kc
KEYCLOAK_AUTHORITY=http://localhost:18080/kc/realms/acmp
KEYCLOAK_ORIGIN=http://localhost:18080
KC_BOOTSTRAP_ADMIN_USERNAME=admin
KC_BOOTSTRAP_ADMIN_PASSWORD=BootKC_2026#x
ACMP_KC_DB_NAME=keycloak
ACMP_KC_DB_USER=keycloak_svc
ACMP_KC_DB_PASSWORD=BootKCDB_2026#x
MSSQL_SA_PASSWORD=BootStrong_2026#x
ACMP_DB_NAME=Acmp
ACMP_DB_USER=acmp_svc
ACMP_DB_PASSWORD=BootSvc_2026#x
ACMP_DB_TRUSTCERT=True
ACMP_S3_ENDPOINT=s3.${REGION}.amazonaws.com
ACMP_S3_BUCKET=acmp-uat-recordings
ACMP_S3_ACCESS_KEY=bootplaceholder
ACMP_S3_SECRET_KEY=bootplaceholder
ACMP_MEDIA_ORIGIN=https://s3.amazonaws.com
SEQ_FIRSTRUN_ADMINPASSWORDHASH=$SEQ_HASH
ACMP_REQUIRE_HTTPS_METADATA=false
WEBEX_ENABLED=false
ACTION_REMINDERS_SWEEP_CRON="0 6 * * *"
ENV

log "materialising secrets"
ACMP_ENV_FILE="$ENV_FILE" sh deploy/scripts/gen-secrets.sh >/dev/null 2>&1 \
  && ok "gen-secrets accepted the generated config" || { bad "gen-secrets rejected the config"; exit 1; }

if [ "$sha" != "--local" ]; then
  log "pulling the published images (proves every tag the compose asks for exists — DEF-019)"
  aws ecr get-login-password --region "$REGION" 2>/dev/null \
    | docker login --username AWS --password-stdin "${REGISTRY%/acmp}" >/dev/null 2>&1
  dc pull -q >/dev/null 2>&1 && ok "all images pulled by the tags the compose requests" \
                             || bad "pull failed — a referenced tag does not exist (DEF-019 class)"
fi

# --- TLS fixture (SL-025 / AC-081) --------------------------------------------------------------
# The cloud compose now mounts a 443 server block, and nginx REFUSES TO START when ssl_certificate
# points at a file that is not there — so without a certificate this gate would fail at `up`, not at
# an assertion. A self-signed cert is exactly what the real first boot uses before certbot runs.
#
# HONEST LIMIT, stated rather than papered over: this fixture proves the LISTENER, the headers and
# the routing. It does NOT prove cert READABILITY, which is the failure a real Let's Encrypt file
# would hit — privkey.pem is 0600 root:root behind symlinks into ../../archive/, and reproducing
# that needs a root-capable Linux host with ownership-preserving bind mounts. On Docker Desktop the
# bind mount does not preserve Unix ownership at all, so a green result here says nothing about it.
# certbot-deploy-hook.sh owns that problem and Stage 2 is where it gets tested.
CERTS=deploy/nginx/certs
if [ ! -s "$CERTS/fullchain.pem" ]; then
  log "generating a self-signed TLS fixture (nginx will not start without one)"
  mkdir -p "$CERTS"
  openssl req -x509 -newkey rsa:2048 -nodes -days 30 \
    -subj "/CN=ACMP-BOOTGATE-SELF-SIGNED" \
    -keyout "$CERTS/privkey.pem" -out "$CERTS/fullchain.pem" >/dev/null 2>&1 \
    || { echo "openssl could not generate the fixture — cannot test the 443 listener"; exit 1; }
  chmod 0644 "$CERTS/privkey.pem" "$CERTS/fullchain.pem" 2>/dev/null || true
fi

log "bringing the full stack up"
dc up -d >/dev/null 2>&1

# Wait for the long-running services AND the one-shots. Waiting on health alone is a race: the
# first run of this gate reported a false failure because keycloak-config had not exited yet when
# its exit code was read -- it only starts once Keycloak is healthy, so "5 healthy" does not imply
# "the realm import has finished". Asserting on a container that is still running reads as a
# product failure when it is a timing bug in the harness.
log "waiting for health + the one-shots to finish"
for i in $(seq 1 30); do
  sleep 10
  healthy=$(docker ps --filter "label=com.docker.compose.project=$PROJECT" --filter "health=healthy" -q | wc -l)
  done_oneshots=0
  for s in db-migrate sqlserver-init keycloak-config; do
    [ "$(docker inspect "$PROJECT-$s-1" --format '{{.State.Status}}' 2>/dev/null)" = "exited" ] \
      && done_oneshots=$((done_oneshots+1))
  done
  printf '  [%3ds] healthy=%s one-shots-finished=%s/3\n' $((i*10)) "$healthy" "$done_oneshots"
  { [ "$healthy" -ge 5 ] && [ "$done_oneshots" -eq 3 ]; } && break
done

log "asserting the stack actually works (functional, not self-reported)"
for svc in sqlserver keycloak api web seq; do
  st=$(docker inspect "$PROJECT-$svc-1" --format '{{.State.Health.Status}}' 2>/dev/null)
  [ "$st" = "healthy" ] && ok "$svc healthy" || bad "$svc is '$st'"
done
for one in db-migrate:migrations sqlserver-init:db-init keycloak-config:realm-import; do
  svc="${one%%:*}"; what="${one##*:}"
  code=$(docker inspect "$PROJECT-$svc-1" --format '{{.State.ExitCode}}' 2>/dev/null)
  [ "$code" = "0" ] && ok "$what completed (exit 0)" || bad "$what exited $code"
done

code=$(curl -s -o /dev/null -w '%{http_code}' http://localhost:18080/ 2>/dev/null)
[ "$code" = "200" ] && ok "web serves HTTP 200 on the published port" || bad "web returned '$code'"
code=$(docker exec "$PROJECT-api-1" curl -s -o /dev/null -w '%{http_code}' http://localhost:8080/healthz 2>/dev/null)
[ "$code" = "200" ] && ok "api /healthz 200" || bad "api /healthz returned '$code'"
# The issuer must be the EXTERNAL url, not the in-container one, or the SPA's tokens will not validate.
iss=$(docker exec "$PROJECT-api-1" curl -s http://keycloak:8080/realms/acmp/.well-known/openid-configuration 2>/dev/null | tr ',' '\n' | grep -o 'http[^"]*realms/acmp' | head -1)
[ "$iss" = "http://localhost:18080/kc/realms/acmp" ] && ok "Keycloak issuer is the external url ($iss)" \
                                                     || bad "Keycloak issuer is '$iss'"
# Nothing must have been OOM-killed within the declared limits (AC-084's mechanism).
oom=0; for svc in sqlserver keycloak api worker web seq; do
  [ "$(docker inspect "$PROJECT-$svc-1" --format '{{.State.OOMKilled}}' 2>/dev/null)" = "true" ] && { bad "$svc was OOM-killed"; oom=1; }
done
[ "$oom" = "0" ] && ok "no container OOM-killed inside the 3584 MiB budget"

# --- the 443 listener (SL-025 / AC-081) ---------------------------------------------------------
# -k throughout: the fixture is self-signed on purpose. What is under test here is that the listener
# ANSWERS and that its server block carries the right posture — trust is only observable from
# outside against a real certificate, which is AC-081's on-box verification in Stage 2.
log "asserting the 443 listener (AC-081)"
TLSH="$WORK/tls-headers.txt"
if curl -sk -o /dev/null -D "$TLSH" "https://localhost:18443/" 2>/dev/null; then
  ok "TLS handshake succeeds and the 443 listener serves the SPA"

  # HSTS is the header AC-081 names, and it is only meaningful over https — the 8080 block sets it
  # too but browsers ignore it there, so THIS is the one that counts.
  grep -qi '^strict-transport-security: *max-age=31536000; *includeSubDomains' "$TLSH" \
    && ok "HSTS present on the HTTPS response" \
    || bad "HSTS missing or wrong on the HTTPS response"

  # All six, individually. nginx's add_header does not merge across server blocks, so the 443 block
  # having SOME headers is no evidence it has the rest — this is exactly the trap the template
  # comments warn about, and checking one header would not catch it.
  for h in x-content-type-options x-frame-options referrer-policy permissions-policy content-security-policy; do
    grep -qi "^${h}:" "$TLSH" && ok "443 block sets ${h}" || bad "443 block is MISSING ${h}"
  done

  # /kc/admin and the master realm must not be reachable now that 443 is public.
  for path in /kc/admin/ /kc/realms/master/.well-known/openid-configuration; do
    c=$(curl -sk -o /dev/null -w '%{http_code}' "https://localhost:18443${path}" 2>/dev/null)
    [ "$c" = "404" ] && ok "denied over TLS: ${path} ($c)" || bad "${path} returned $c over TLS — expected 404"
  done

  # The SPA's own realm must still work, or the deny rule has taken too much with it.
  c=$(curl -sk -o /dev/null -w '%{http_code}' "https://localhost:18443/kc/realms/acmp/.well-known/openid-configuration" 2>/dev/null)
  [ "$c" = "200" ] && ok "the acmp realm is still reachable over TLS ($c)" || bad "acmp realm returned $c over TLS"

  # /api/ must reach the API through the TLS block, not just serve the SPA fallback. Any
  # API-originated status proves the proxy path; 502/504 means nginx could not reach the backend.
  c=$(curl -sk -o /dev/null -w '%{http_code}' "https://localhost:18443/api/topics" 2>/dev/null)
  case "$c" in
    502|503|504) bad "/api/ over TLS returned $c — nginx did not reach the api" ;;
    "")          bad "/api/ over TLS returned nothing" ;;
    *)           ok  "/api/ proxies to the api over TLS (HTTP $c)" ;;
  esac
else
  bad "no TLS listener on 18443 — the 443 server block did not load"
fi

printf '\n\033[1m[boot] RESULT: %s passed, %s failed\033[0m\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ] || exit 1
