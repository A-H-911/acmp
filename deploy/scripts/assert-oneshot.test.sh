#!/usr/bin/env sh
# The one runnable check behind assert-oneshot.sh (DEF-054).
#
#   sh deploy/scripts/assert-oneshot.test.sh
#
# A guard in the "detects but does not TELL" family has to be proven by FORCING each refusal, not by
# watching it pass on a healthy stack — a guard that always returns 0 looks identical to a working
# one right up until the day it matters. `docker` is stubbed on PATH so every branch is reachable
# without a container runtime, which is also what lets this run in CI's compose job.
#
# CASE 4 IS THE DEFECT ITSELF: a one-shot still running when `up --wait` has already returned.
set -eu

here="$(cd "$(dirname "$0")" && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

cat > "$tmp/docker" <<'STUB'
#!/usr/bin/env sh
case "${1:-}" in
  compose) [ -z "${FAKE_CID:-}" ] || printf '%s\n' "${FAKE_CID}" ;;
  inspect) printf '%s\n' "${FAKE_STATE:-unknown:x}" ;;
  logs)    printf 'RECONCILE LOG MARKER\n' ;;
esac
STUB
chmod +x "$tmp/docker"
PATH="$tmp:$PATH"
export PATH

fails=0

# Runs the guard and checks BOTH halves at once: the verdict (exit code) and the telling (the log
# reaching stdout). Checking only the exit code would re-create the defect one level up.
check() {  # <name> <expect-pass|expect-fail> <expect-log|no-log>
  name="$1"; want="$2"; wantlog="$3"
  set +e
  out="$(sh "$here/assert-oneshot.sh" keycloak-config -f /dev/null 2>&1)"
  code=$?
  set -e

  if [ "$want" = "expect-pass" ] && [ "$code" -ne 0 ]; then
    echo "FAIL  $name: expected exit 0, got $code"; fails=$(( fails + 1 )); return
  fi
  if [ "$want" = "expect-fail" ] && [ "$code" -eq 0 ]; then
    echo "FAIL  $name: expected a non-zero exit, got 0 — the guard did not refuse"; fails=$(( fails + 1 )); return
  fi
  case "$out" in
    *"RECONCILE LOG MARKER"*) got=expect-log ;;
    *)                        got=no-log ;;
  esac
  if [ "$got" != "$wantlog" ]; then
    echo "FAIL  $name: wanted $wantlog, got $got"; fails=$(( fails + 1 )); return
  fi
  echo "OK    $name (exit $code)"
}

FAKE_CID=deadbeef; export FAKE_CID

# 1 — converged. The only case that may pass, and it still prints the log.
FAKE_STATE=exited:0 ONESHOT_TIMEOUT=5 check "exited 0 passes and still prints the log" expect-pass expect-log

# 2 — reconcile.sh returned non-zero. The log MUST survive: a refusal nobody can read is half a guard.
FAKE_STATE=exited:1 ONESHOT_TIMEOUT=5 check "a non-zero exit refuses, with the log" expect-fail expect-log

# 3 — DEF-054 proper: `up --wait` returned while the one-shot was still mid-flight.
FAKE_STATE=running:0 ONESHOT_TIMEOUT=1 check "still running at the deadline refuses" expect-fail expect-log

# 4 — the container was never created at all. Refuses before it can print anything.
FAKE_CID= FAKE_STATE=exited:0 ONESHOT_TIMEOUT=5 check "a missing container refuses" expect-fail no-log

[ "$fails" -eq 0 ] || { echo "assert-oneshot.test: $fails case(s) failed"; exit 1; }
echo "assert-oneshot.test: all 4 cases behaved as specified."
