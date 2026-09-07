#!/usr/bin/env bash
# Self-check for scripts/sql-startup-sample.sh: proves the CLASSIFIER reaches all three outcomes, using
# a tiny image with the run and readiness commands overridden. It says nothing about SQL Server — the
# harness's real calibration is the current FTS image producing a captured crash (WBS-32, LL-060). Both
# matter: this one is cheap and runs first in the workflow, so a broken classifier cannot pass as a clean arm.
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
img=alpine:3.20

expect() { # <label> <tally-file> <field> <value>
  local got; got=$(sed -n "s/.*\"$3\":\([0-9]*\).*/\1/p" "$2")
  [[ "$got" == "$4" ]] || { echo "FAIL $1: $3=$got, expected $4"; exit 1; }
  echo "ok   $1: $3=$4"
}

# 1. ok — the container stays up and the readiness command succeeds (two starts, both counted).
RUN_CMD="sleep 60" READY_CMD=true BOUND_SECONDS=20 POLL_SECONDS=1 \
  "$here/sql-startup-sample.sh" "$img" 2 "$tmp/ok" >/dev/null
expect ready "$tmp/ok/tally.json" ok 2
expect ready "$tmp/ok/tally.json" crash 0

# 2. crash — the container exits non-zero before readiness; the log is kept.
RUN_CMD="sh -c exit_7" READY_CMD=true BOUND_SECONDS=20 POLL_SECONDS=1 \
  "$here/sql-startup-sample.sh" "$img" 1 "$tmp/crash" >"$tmp/crash.out" || true
# (`exit_7` is not a command, so sh exits 127 — a non-zero exit is all the classifier needs.)
expect crash "$tmp/crash/tally.json" crash 1
[[ -f "$tmp/crash/start-001.log" ]] || { echo "FAIL crash: no start-001.log kept"; exit 1; }
grep -q 'exit=127' "$tmp/crash.out" || { echo "FAIL crash: exit code not reported"; cat "$tmp/crash.out"; exit 1; }
echo "ok   crash: start-001.log kept, exit=127 reported"

# 3. timeout — the container stays up but readiness never succeeds within the bound.
RUN_CMD="sleep 60" READY_CMD=false BOUND_SECONDS=3 POLL_SECONDS=1 \
  "$here/sql-startup-sample.sh" "$img" 1 "$tmp/timeout" >/dev/null
expect timeout "$tmp/timeout/tally.json" timeout 1

# 4. sidecar — a contention container is started alongside, its state is reported, and it is cleaned up.
SIDECARS="$img" RUN_CMD="sleep 60" READY_CMD=true BOUND_SECONDS=20 POLL_SECONDS=1 \
  "$here/sql-startup-sample.sh" "$img" 1 "$tmp/side" >"$tmp/side.out"
expect sidecar "$tmp/side/tally.json" ok 1
# alpine's default shell exits at once with no TTY, so `exited` is the state a live sidecar of THIS image shows.
grep -q 'sidecar=exited' "$tmp/side.out" || { echo "FAIL sidecar: state not reported"; cat "$tmp/side.out"; exit 1; }
echo "ok   sidecar: state reported on the line"
[[ -z "$(docker ps -aq --filter ancestor="$img")" ]] || { echo "FAIL sidecar: containers left behind"; exit 1; }
echo "ok   sidecar: nothing left behind"

echo "PASS: classifier reaches ok, crash and timeout; sidecar reported and cleaned up"
