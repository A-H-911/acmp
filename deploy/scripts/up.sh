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
exec docker compose $FILES --env-file "$ENV_FILE" up -d --wait "$@"
