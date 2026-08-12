#!/usr/bin/env sh
# Assert that a compose ONE-SHOT actually ran AND succeeded — and say so out loud (DEF-054).
#
#   sh deploy/scripts/assert-oneshot.sh <service> <compose-arg>...
#   sh deploy/scripts/assert-oneshot.sh keycloak-config -f deploy/docker-compose.yml --env-file deploy/.env
#
# WHY THIS EXISTS. `docker compose up --wait` RETURNS WHILE A ONE-SHOT IS STILL MID-FLIGHT. That is
# measured, not assumed — .github/workflows/e2e.yml records the observation, and it is why nothing
# that ends in `up --wait` can say anything at all about keycloak-config. deploy/scripts/up.sh ended
# in exactly that, so the dev stack AND ON-PREM PRODUCTION came up fully healthy — api, web and
# keycloak all green — while the realm reconciliation may have failed silently.
#
# That matters most on on-prem, because reconcile.sh is the ONLY seam that reaches an EXISTING realm
# (Keycloak never re-imports realm-export.json). A silent failure there means the deployed realm
# quietly lacks whatever the release added — the acmp-web origin, the admin client, its grant — and
# every health check stays green. Third occurrence of "a control that DETECTS but does not TELL"
# (DEF-023, DEF-051, now DEF-054).
#
# THE EXIT CODE IS THE ASSERTION, NOT A PROXY FOR IT: reconcile.sh reads its own post-conditions back
# (the origin, and the service account's grant compared as an exact set) and returns non-zero if they
# did not take. So this asserts the OUTCOME, not merely that a script was invoked.
#
# THE LOG IS PRINTED UNCONDITIONALLY, on purpose. Compose only dumps a one-shot's stdout on the
# failure path, and a green check that leaves no trace is precisely what produced this family of
# defects. Same shape as the e2e workflow step and 08-bootstrap-box.sh's wait_oneshot — deliberately
# not a fourth way of doing it.
set -eu

svc="${1:?usage: assert-oneshot.sh <service> <compose-arg>...}"
shift

# Poll rather than read once: inspecting the exit code while the container is still running is the
# bug PE-154 recorded in the local boot gate, and the e2e step's own first run reported `running:0`.
timeout="${ONESHOT_TIMEOUT:-180}"

cid="$(docker compose "$@" ps -aq "$svc" 2>/dev/null || true)"
if [ -z "$cid" ]; then
  echo "assert-oneshot: '$svc' has no container — the one-shot never started." >&2
  exit 1
fi

# `running:x` rather than empty: with the deadline already past the loop body never runs, and the
# final comparison must still fail loudly instead of tripping `set -u`.
state="running:x"
deadline=$(( $(date +%s) + timeout ))
while [ "$(date +%s)" -lt "$deadline" ]; do
  state="$(docker inspect -f '{{.State.Status}}:{{.State.ExitCode}}' "$cid" 2>/dev/null || echo unknown:x)"
  case "$state" in exited:*) break ;; esac
  sleep 3
done

echo "--- $svc log ---"
docker logs "$cid" 2>&1 || true
echo "--- end $svc log ---"

if [ "$state" != "exited:0" ]; then
  echo "assert-oneshot: '$svc' did not converge (state=$state) — see its log above." >&2
  exit 1
fi
echo "assert-oneshot: '$svc' exited 0."
