#!/usr/bin/env sh
# ACMP — single-command bring-up (NFR-052). Materializes Docker secret files (gen-secrets.sh), then
# `docker compose up`. Because "secrets everywhere" needs the secret files present at compose-parse time, a bare
# `docker compose up` is not enough on its own — this wrapper is the supported single command.
#
#   deploy/scripts/up.sh            # base dev/e2e stack
#   deploy/scripts/up.sh --prod     # base + production overlay (docker-compose.prod.yml)
set -eu

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

ENV_FILE="deploy/.env"
[ -f "$ENV_FILE" ] || ENV_FILE="deploy/.env.example"

FILES="-f deploy/docker-compose.yml"
if [ "${1:-}" = "--prod" ]; then
  FILES="$FILES -f deploy/docker-compose.prod.yml"
  shift
fi

sh deploy/scripts/gen-secrets.sh
# shellcheck disable=SC2086
docker compose $FILES --env-file "$ENV_FILE" up -d --wait "$@"

# DEF-054 — `up --wait` above is NOT the end of the bring-up. It returns while a one-shot is still
# mid-flight (measured; see assert-oneshot.sh), so without this the dev stack and ON-PREM PRODUCTION
# came up green while the realm reconciliation may have failed silently. CI (DEF-051) and the cloud
# deploy (08-bootstrap-box.sh wait_oneshot) already assert it; this was the only path that did not.
#
# Skipped — out loud — when a service subset was asked for, because then keycloak-config legitimately
# was not started. `exec` is gone from the line above precisely so this can run.
if [ "$#" -eq 0 ]; then
  # shellcheck disable=SC2086
  sh deploy/scripts/assert-oneshot.sh keycloak-config $FILES --env-file "$ENV_FILE"
else
  echo "up.sh: services were named explicitly ($*) — skipping the keycloak-config reconcile assertion."
fi
