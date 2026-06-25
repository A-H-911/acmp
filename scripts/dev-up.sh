#!/usr/bin/env bash
# Bring up the full local stack (api, web, sqlserver, seq, minio).
set -euo pipefail
cd "$(dirname "$0")/.."
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
