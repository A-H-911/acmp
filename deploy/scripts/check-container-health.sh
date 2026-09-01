#!/usr/bin/env sh
# ACMP container health check — the STEADY-STATE consumer the healthcheck's verdict never had
# (WBS-26.4 / SC-042, from DW-091; DEF-079 is why there is no interlock to restore).
#
#   sh deploy/scripts/check-container-health.sh -f deploy/docker-compose.yml --env-file deploy/.env
#
# WHY THIS EXISTS, AND WHAT THE GAP ACTUALLY IS — the narrower claim, because the wider one is false.
# DW-091 said "nothing consumes the api healthcheck's verdict". Measured 2026-09-01: BOTH boot paths
# consume it. deploy/scripts/up.sh ends in `docker compose up -d --wait`, which waits for every
# service with a healthcheck to become healthy and returns non-zero if one does not; and the cloud
# bootstrap (deploy/aws/08-bootstrap-box.sh) runs wait_healthy for sqlserver, keycloak, seq, api,
# worker and web and exits 1 if any never gets there. So at BOOT the verdict is consumed twice over.
#
# WHAT IS GENUINELY UNCONSUMED IS THE STEADY STATE. After boot nothing observes a health TRANSITION.
# DEF-079 records that web and worker DID declare `condition: service_healthy`, that the total-outage
# consequence was measured, that it was put back to the operator, and that they chose to keep the
# signal and drop the gate — so by design no dependent restarts when api goes unhealthy at 03:00. The
# compose comment concedes exactly this: "the honest health signal is kept (docker ps still reports
# unhealthy); only the interlock is dropped." Nothing then reads that signal. The container sits
# unhealthy, every job stays green, and the only way anyone finds out is by running `docker ps`.
#
# ⛔ THIS DOES NOT RESTORE THE INTERLOCK AND MUST NOT. /readyz reaches SQL Server, Hangfire and object
# storage and is deliberately slow on a cold start; gating startup on it means a 30-second blip in any
# of them stops a service from starting at all. That trade is settled (DEF-079) and reopening it needs
# a decision superseding that choice, not a script. This check runs BESIDE the stack and cannot fail
# it — the worst it can do is report.
#
# IT DETECTS; SEQ NOTIFIES — DEC-099 d2's SPLIT, APPLIED HERE BY OPERATOR DECISION. That decision settled
# the same question for the C-INS-01 anomaly signals: the threshold is evaluated in versioned, testable
# code and the EVENT is what a Seq rule notifies on, because a Seq rule on its own is configuration no
# gate can see (DEF-078, DEF-079 and DW-091 are three separate instances of a control nobody could check).
# So the detection below is committed and forced-refusal tested, and the CLEF event it emits is the signal
# a Seq alert fires on. ⚠ The Seq ALERT itself is not versionable — Seq is provisioned here as a container
# with a first-run password and nothing else — so the runbook names the exact event type to alert on, and
# that half is documentation by necessity rather than by choice.
#
# IT IS check-backup-freshness.sh's SHAPE, DELIBERATELY, and not a fifth way of doing this. That
# script answers "have backups STOPPED?" — the question failure-alerting cannot ask, because absence
# produces no failure. This one answers "is anything sitting unhealthy RIGHT NOW?" — the question boot
# assertions cannot ask, because they only ever run at boot.
#
# Config: ACMP_SEQ_INGEST_URL (default http://localhost:5341/ingest/clef — the operator-mapped host port
#   from docker-compose.yml; skipped-but-LOUD if the post is refused), ACMP_ALERT_TOPIC_ARN (optional;
#   skipped-but-LOUD if unset, which is the on-prem case).
set -eu

[ "$#" -gt 0 ] || {
  echo "usage: check-container-health.sh <compose-arg>..." >&2
  echo "   eg: check-container-health.sh -f deploy/docker-compose.yml --env-file deploy/.env" >&2
  exit 2
}

TOPIC="${ACMP_ALERT_TOPIC_ARN:-}"
SEQ_URL="${ACMP_SEQ_INGEST_URL:-http://localhost:5341/ingest/clef}"
log() { printf '[health %s] %s\n' "$(date +%H:%M:%S)" "$*"; }

# THE SIGNAL A SEQ ALERT FIRES ON. One CLEF line, @l Error, carrying a distinctive EventType so an alert
# keys on a name rather than on a message substring — which is how a Seq rule survives someone editing
# this script's prose. Deliberately structured: Unhealthy and Checked are properties, not interpolated
# text, so the alert can also threshold on how many.
emit_to_seq() {  # <unhealthy-list> <checked-count>
  body=$(printf '{"@t":"%s","@l":"Error","@mt":"ACMP container health: {Unhealthy} unhealthy of {Checked} checked on {Host}","EventType":"Deploy.ContainerUnhealthy","Unhealthy":"%s","Checked":%s,"Host":"%s"}' \
    "$(date -u '+%Y-%m-%dT%H:%M:%S.000Z')" "$1" "$2" "$(uname -n 2>/dev/null || echo unknown)")
  if printf '%s\n' "$body" | curl -sS -f --max-time 10 -X POST "$SEQ_URL" \
       -H 'Content-Type: application/vnd.serilog.clef' --data-binary @- >/dev/null 2>&1; then
    log "Deploy.ContainerUnhealthy emitted to Seq at $SEQ_URL"
  else
    # The same rule as every other telling here: a notifier that cannot notify must SAY so. A silent
    # failure to reach Seq would leave the operator's alert permanently un-fired while the log looks
    # like a normal alerting run - DEF-023/051/054's shape, one level out.
    log "SEQ EMIT FAILED to $SEQ_URL — no Seq alert can fire for this finding. Check that Seq is up and"
    log "  that ACMP_SEQ_INGEST_URL points at its /ingest/clef endpoint."
  fi
}

services="$(docker compose "$@" ps --services 2>/dev/null || true)"
if [ -z "$services" ]; then
  # Not a shrug. A compose project that names no services means the arguments are wrong or the stack
  # is down, and "nothing to check" reads exactly like "everything is fine" — the failure
  # check-backup-freshness.sh was fixed for, in this same directory.
  echo "check-container-health: 'docker compose ps --services' named no services — the compose" >&2
  echo "        arguments are wrong or the stack is not up. Refusing to report health." >&2
  exit 1
fi

checked=0
bad=""
for s in $services; do
  cid="$(docker compose "$@" ps -q "$s" 2>/dev/null || true)"
  # No running container: one-shots have exited by design, and whether they exited 0 is
  # assert-oneshot.sh's question, not this one. Asking it here would duplicate a guard that exists.
  [ -n "$cid" ] || continue
  st="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$cid" 2>/dev/null || echo unknown)"
  case "$st" in
    none)    continue ;;   # no healthcheck declared — there is no verdict to consume
    healthy) checked=$(( checked + 1 )); log "OK          $s (healthy)" ;;
    *)       checked=$(( checked + 1 )); bad="$bad $s($st)"; log "UNHEALTHY   $s ($st)" ;;
  esac
done

# LL-013 / trap 31. A pass over zero subjects is the shape of a green control that never looked, and
# this project has filed that defect four times. The count is what makes a clean run mean something.
if [ "$checked" -eq 0 ]; then
  echo "check-container-health: inspected 0 running containers that declare a healthcheck —" >&2
  echo "        refusing to report healthy on an empty set." >&2
  exit 1
fi

if [ -z "$bad" ]; then
  log "OK — $checked container(s) with a healthcheck, all healthy."
  exit 0
fi

log "UNHEALTHY —$bad ($checked checked)"

# Seq FIRST, and unconditionally. It is the notification path that exists in every environment — on-prem
# has no SNS topic — so making it depend on anything else would reproduce the gap this item is closing.
emit_to_seq "$(echo "$bad" | sed 's/^ //')" "$checked"

if [ -z "$TOPIC" ]; then
  # On-prem has no SNS topic. Saying so is the point: a control that silently cannot tell is the
  # DEF-023/051/054 family, and this line is what keeps it out of that family.
  log "NO SNS ALERT SENT: ACMP_ALERT_TOPIC_ARN is unset, so the push path is Seq only (above), plus"
  log "  this log and the non-zero exit below. On-prem that is expected; in cloud it is a finding."
  exit 1
fi
if aws sns publish --region "${AWS_REGION:-us-east-1}" --topic-arn "$TOPIC" \
     --subject "ACMP container(s) UNHEALTHY on $(uname -n 2>/dev/null || echo unknown)" \
     --message "ACMP container health check FAILED.

host      : $(uname -n 2>/dev/null || echo unknown)
when      : $(date -u '+%Y-%m-%dT%H:%M:%SZ') UTC
unhealthy :$bad
checked   : $checked container(s) declaring a healthcheck

Nothing has crashed and no dependent has restarted — by design. DEF-079 records that
web and worker once declared \`condition: service_healthy\`, that the total-outage
consequence was measured, and that the interlock was dropped by operator decision so a
brief blip cannot stop the stack. The health SIGNAL was kept; until now nothing read it
after boot.

The api healthcheck probes /readyz, which reaches SQL Server, Hangfire and object
storage — so an unhealthy api usually means one of those, not the api itself.
  docker compose -f deploy/docker-compose.cloud.yml --env-file deploy/.env.cloud ps
  docker compose -f deploy/docker-compose.cloud.yml --env-file deploy/.env.cloud logs --tail=200 api
Context: WBS-26.4 / SC-042. Runbook: deploy/runbooks/cloud-provisioning.md" >/dev/null 2>&1; then
  log "unhealthy alert published to $TOPIC"
else
  log "ALERT PUBLISH FAILED to $TOPIC — the finding above is UNREPORTED. Check that the instance"
  log "  role grants sns:Publish on that topic (deploy/aws/03-iam.sh, SnsPublishFailureAlerts)."
fi

# ⚠ NON-ZERO, WHERE check-backup-freshness.sh DELIBERATELY EXITS 0 — the difference is stated because
# the two look alike. Backup staleness is LEGITIMATELY EXPECTED on a box that is stopped when idle, so
# a non-zero exit there would train the operator to ignore cron. A container sitting unhealthy while
# the box is up is never an expected steady state, so the exit code carries information and gives the
# check a second consumer for free: a runbook or a deploy step can chain on it.
exit 1
