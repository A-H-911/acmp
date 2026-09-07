#!/usr/bin/env bash
# WBS-32 / SL-039 (DEC-142) — start a SQL Server image N times in sequence and classify every start.
#
# The DEF-121 / DEF-130 crashes happen at container START, before any test runs, at roughly 3.5% of
# CI starts. This is the sampling harness that turns "it crashed again" into a rate per image, so two
# images can be compared (the arms are chosen in .github/workflows/sql-startup-sampling.yml).
#
#   sql-startup-sample.sh <image> <starts> <out-dir>
#
# Per start: docker run -d with the same environment Testcontainers' MsSqlBuilder gives the fixture
# (ACCEPT_EULA, MSSQL_SA_PASSWORD), then poll until ONE of three outcomes:
#   ok       the readiness command succeeds inside the container (default: sqlcmd SELECT 1, the fixture's wait)
#   crash    the container has exited — its exit code, `docker logs` and /var/opt/mssql/log are kept
#   timeout  neither within BOUND_SECONDS
# Every container is removed after classification. A final JSON tally is written to <out-dir>/tally.json
# and printed. The script exits 0 whatever the tally says: a crash is a DATA POINT here, not a failure.
#
# ⚠ LL-060: a harness that never sees the crash reads exactly like a clean image. The workflow runs the
# CURRENT image first as the positive control; nothing this script prints means anything until that arm
# has produced at least one `crash` whose saved log carries a SQLPAL `Reason:` line.
#
# Contention (a real arm option, not just for the self-check):
#   SIDECARS       word-split images started `docker run -d` at the SAME MOMENT as each subject start,
#                  with the same env and no command, and removed after classification. Their state at
#                  classification time is printed on the line (`sidecar=running,exited`), so the reader
#                  can see the contention existed. Why: arm (i) on an idle runner, one container at a
#                  time, gave 0/120 (run 34098382664) while the CI job - where it crashes at ~3.5% - boots
#                  SqlBackstopFixture's 2022 container and MinIO concurrently with it (WBS-32, LL-060).
#
# Overridable for the self-check (scripts/test-sql-startup-sample.sh), never in a real arm:
#   READY_CMD      readiness command run via `docker exec`, word-split (default: the sqlcmd probe)
#   RUN_CMD        command appended to `docker run <image>`, word-split (default: the image's own entrypoint)
#   BOUND_SECONDS  per-start readiness bound (default 300; a healthy start is ~10-40 s, a crash <10 s)
#   POLL_SECONDS   poll interval (default 2)
#
# ⚠ Git Bash on Windows: run with MSYS_NO_PATHCONV=1, or the /opt/... probe path is rewritten to a Windows
#   path before Docker sees it and EVERY healthy start reads as `timeout` (measured; cost ten minutes).
set -euo pipefail

IMAGE="${1:?usage: $0 <image> <starts> <out-dir>}"
STARTS="${2:?usage: $0 <image> <starts> <out-dir>}"
OUT="${3:?usage: $0 <image> <starts> <out-dir>}"
BOUND_SECONDS="${BOUND_SECONDS:-300}"
POLL_SECONDS="${POLL_SECONDS:-2}"
# Ephemeral SA password, built at runtime so no literal sits in the repo (it dies with the container).
SA_PASSWORD="${MSSQL_SA_PASSWORD:-Sample-$(date +%s)-aZ9!}"

if [[ -n "${READY_CMD:-}" ]]; then read -ra ready <<<"$READY_CMD"
else ready=(/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT 1"); fi
run_cmd=(); [[ -n "${RUN_CMD:-}" ]] && read -ra run_cmd <<<"$RUN_CMD"
sidecars=(); [[ -n "${SIDECARS:-}" ]] && read -ra sidecars <<<"$SIDECARS"

mkdir -p "$OUT"
ok=0; crash=0; timeout=0

for ((i = 1; i <= STARTS; i++)); do
  tag=$(printf 'start-%03d' "$i")
  side_ids=()
  for s in "${sidecars[@]}"; do
    side_ids+=("$(docker run -d -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="$SA_PASSWORD" "$s")")
  done
  cid=$(docker run -d -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="$SA_PASSWORD" "$IMAGE" "${run_cmd[@]}")
  t0=$SECONDS
  outcome=timeout
  exit_code=
  while (( SECONDS - t0 < BOUND_SECONDS )); do
    state=$(docker inspect -f '{{.State.Status}} {{.State.ExitCode}}' "$cid")
    if [[ "${state%% *}" == "exited" ]]; then
      outcome=crash; exit_code="${state##* }"; break
    fi
    if docker exec "$cid" "${ready[@]}" >/dev/null 2>&1; then
      outcome=ok; break
    fi
    sleep "$POLL_SECONDS"
  done
  elapsed=$((SECONDS - t0))
  side_state=
  for sid in "${side_ids[@]}"; do
    side_state+="${side_state:+,}$(docker inspect -f '{{.State.Status}}' "$sid" 2>/dev/null || echo gone)"
  done

  if [[ "$outcome" != ok ]]; then
    docker logs "$cid" >"$OUT/$tag.log" 2>&1 || true
    # The SQLPAL crash writes its dump and summary under /var/opt/mssql/log (what CrashArtefacts lifts
    # in the fixture). Keep the small files; a core dump can run to gigabytes and is not what we read.
    if docker cp "$cid:/var/opt/mssql/log" "$OUT/$tag-mssql-log" 2>/dev/null; then
      find "$OUT/$tag-mssql-log" -type f -size +5M -delete || true
    fi
  fi
  docker rm -f "$cid" "${side_ids[@]}" >/dev/null 2>&1 || true

  reason=
  if [[ "$outcome" == crash ]]; then
    reason=$(grep -o 'Reason: 0x[0-9A-Fa-f]*' "$OUT/$tag.log" 2>/dev/null | head -1 || true)
  fi
  case "$outcome" in
    ok) ok=$((ok + 1)) ;;
    crash) crash=$((crash + 1)) ;;
    *) timeout=$((timeout + 1)) ;;
  esac
  printf '%s %-7s %4ss exit=%-3s %s%s\n' "$tag" "$outcome" "$elapsed" "${exit_code:--}" \
    "${side_state:+sidecar=$side_state }" "$reason"
done

tally=$(printf '{"image":"%s","sidecars":"%s","starts":%d,"ok":%d,"crash":%d,"timeout":%d}' \
  "$IMAGE" "${SIDECARS:-}" "$STARTS" "$ok" "$crash" "$timeout")
echo "$tally" | tee "$OUT/tally.json"
