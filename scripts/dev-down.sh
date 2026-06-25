#!/usr/bin/env bash
# Stop the local stack. Pass --volumes to also drop data.
set -euo pipefail
cd "$(dirname "$0")/.."
docker compose -f deploy/docker-compose.yml --env-file deploy/.env down "$@"
