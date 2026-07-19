#!/usr/bin/env bash
# ACMP warm-standby promotion (P18b, deployment.md §7 + §6.2). Run ON THE STANDBY VM after the primary fails:
# restore the newest synced backup, bring the stack up, and smoke it. The final DNS / reverse-proxy cutover is a
# deliberate MANUAL step (announce first) — this script prepares the standby and stops before redirecting traffic.
#
#   deploy/scripts/promote.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
log() { printf '[promote %s] %s\n' "$(date +%H:%M:%S)" "$*"; }

log "1/4 bring up the stack (secrets + prod overlay)"
sh deploy/scripts/up.sh --prod

log "2/4 restore the newest synced backup"
sh deploy/scripts/restore.sh

log "3/4 smoke /healthz + /readyz (via the web nginx)"
WEB_URL="${ACMP_WEB_URL:-http://localhost}"
for path in healthz readyz; do
  code="$(curl -sk -o /dev/null -w '%{http_code}' "$WEB_URL/api/$path" || echo 000)"
  log "   /$path -> HTTP $code"
  [ "$code" = "200" ] || log "   WARNING: /$path not healthy — investigate before cutover"
done

log "4/4 standby is READY. MANUAL cutover remaining: repoint DNS / the org reverse proxy to this VM, then"
log "     announce restoration to the committee (in-app notification). RTO budget: <= 8h (NFR-056)."
