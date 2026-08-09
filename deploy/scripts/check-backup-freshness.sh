#!/usr/bin/env bash
# ACMP backup FRESHNESS check (OQ-068 option (e)). Answers the one question backup.sh cannot:
# "have backups stopped happening?"
#
#   deploy/scripts/check-backup-freshness.sh
#
# WHY THIS EXISTS. DEF-032 made backup.sh alert when a backup FAILS. A backup that never RUNS
# produces no failure, no exit code and no alert — so failure alerting is blind to absence, which for
# a box that is stopped when idle is by far the likelier state. This is the fifth variant of one shape
# in this project (DEF-023/030/031/032): a control that can tell, watching something that never
# happens. An alert on failure presumes attempts.
#
# WHY IT IS NOT A CLOUDWATCH ALARM, which is how OQ-068 originally framed option (e). An alarm on
# "newest backup older than N hours" would sit in ALARM permanently while the instance is stopped,
# because no backups legitimately occur then. That is exactly OQ-067's trap — an alarm in its expected
# state most of the time is one that gets ignored, which is functionally no alarm at all. So this runs
# ON THE BOX, at boot and daily, i.e. only while somebody could actually act on the answer, and it
# cannot fire spuriously into an empty room.
#
# Config: ACMP_BACKUP_BUCKET (required), ACMP_ALERT_TOPIC_ARN (optional; skipped-but-LOUD if unset),
#   ACMP_BACKUP_MAX_AGE_HOURS (default 26 — just over a daily cycle, right for an always-on box;
#   UAT sets it higher because the box is deliberately stopped for days at a time).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
ENV_FILE="${ACMP_ENV_FILE:-deploy/.env}"
# DEF-022 ALL OVER AGAIN, and it was in this file within an hour of being written. The first version
# sourced the env file only `[ -f ]` it existed and otherwise carried on, so running without
# ACMP_ENV_FILE on a cloud box — where the file is .env.cloud, not .env — left ACMP_BACKUP_BUCKET
# empty and the script exited 0 reporting "nothing to check". A freshness check that reports nothing
# to check is INDISTINGUISHABLE FROM HEALTHY, which makes it worse than absent: it is a control that
# actively reassures while doing nothing. Refuse instead, exactly as backup.sh was fixed to.
if [ -f "$ENV_FILE" ]; then
  set -a; . "$ENV_FILE"; set +a
elif [ -z "${ACMP_BACKUP_BUCKET:-}" ]; then
  echo "check-backup-freshness: env file '$ENV_FILE' not found and ACMP_BACKUP_BUCKET is not set —" >&2
  echo "        refusing to report 'nothing to check', which reads exactly like 'backups are fine'." >&2
  echo "        Set ACMP_ENV_FILE (cloud: /opt/acmp/deploy/.env.cloud). The crontab sets it; this" >&2
  echo "        refusal is the net, not the plan (DEF-022)." >&2
  exit 1
fi

BUCKET="${ACMP_BACKUP_BUCKET:-}"
TOPIC="${ACMP_ALERT_TOPIC_ARN:-}"
MAX_AGE_H="${ACMP_BACKUP_MAX_AGE_HOURS:-26}"
log() { printf '[freshness %s] %s\n' "$(date +%H:%M:%S)" "$*"; }

if [ -z "$BUCKET" ]; then
  # Also a refusal, not a shrug: an env file that exists but sets no bucket means local-only backups,
  # which are not backups (NFR-058). Reporting "nothing to check" would hide that.
  echo "check-backup-freshness: ACMP_BACKUP_BUCKET is unset in '$ENV_FILE' — there is no" >&2
  echo "        off-instance copy to check, which is itself the finding (NFR-058)." >&2
  exit 1
fi

# Newest object under sql/. --output text keeps this to one line per object; the last field is the key.
newest="$(aws s3api list-objects-v2 --bucket "$BUCKET" --prefix sql/ \
  --query 'sort_by(Contents,&LastModified)[-1].[LastModified,Key]' --output text 2>/dev/null || true)"

if [ -z "$newest" ] || [ "$newest" = "None" ]; then
  age_h="never"; detail="NO backup objects exist under s3://$BUCKET/sql/ at all."
else
  last_mod="${newest%%$'\t'*}"; key="${newest##*$'\t'}"
  last_epoch="$(date -u -d "$last_mod" +%s 2>/dev/null || echo 0)"
  now_epoch="$(date -u +%s)"
  age_h=$(( (now_epoch - last_epoch) / 3600 ))
  detail="Newest backup is ${age_h}h old: $key (${last_mod})."
fi

if [ "$age_h" != "never" ] && [ "$age_h" -le "$MAX_AGE_H" ]; then
  log "OK — $detail Threshold ${MAX_AGE_H}h."
  exit 0
fi

log "STALE — $detail Threshold ${MAX_AGE_H}h."
if [ -z "$TOPIC" ]; then
  log "NO ALERT SENT: ACMP_ALERT_TOPIC_ARN is unset, so this is visible ONLY in this log."
  exit 0
fi
if aws sns publish --region "${AWS_REGION:-us-east-1}" --topic-arn "$TOPIC" \
     --subject "ACMP backups are STALE on $(uname -n 2>/dev/null || echo unknown)" \
     --message "ACMP backup freshness check FAILED.

host      : $(uname -n 2>/dev/null || echo unknown)
when      : $(date -u '+%Y-%m-%dT%H:%M:%SZ') UTC
bucket    : s3://$BUCKET/sql/
threshold : ${MAX_AGE_H}h
finding   : $detail

Nothing has FAILED — this is the absence of backups, which failure alerting cannot see.
If this box is stopped for long periods that may be expected; if it is not, the schedule
has stopped working. Run one by hand to confirm the path still works:
  sudo ACMP_ENV_FILE=/opt/acmp/deploy/.env.cloud /opt/acmp/deploy/scripts/backup.sh
Context: OQ-068. Runbook: deploy/runbooks/cloud-backup-dr.md" >/dev/null 2>&1; then
  log "staleness alert published to $TOPIC"
else
  log "ALERT PUBLISH FAILED to $TOPIC — the staleness above is UNREPORTED. Check that the instance"
  log "  role grants sns:Publish on that topic (deploy/aws/03-iam.sh, SnsPublishFailureAlerts)."
fi
# Exit 0 deliberately: staleness is a REPORT, not a run failure. A non-zero exit here would make cron
# logs look like a broken job and would train the operator to ignore it.
exit 0
