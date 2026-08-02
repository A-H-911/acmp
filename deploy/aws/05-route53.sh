#!/usr/bin/env bash
# 05 — Route 53 A record UPSERT (SL-020 / ADR-0037; AC-082). Points an environment's
# subdomain at a public IP, TTL 60. Idempotent by nature (UPSERT). This same logic runs
# on the on-demand UAT box at boot (the P25 boot-updater), because a stopped instance
# releases its public IP; run it manually for the always-on prod box after launch.
#
#   bash deploy/aws/05-route53.sh <uat|prod> <public-ip>
set -euo pipefail
. "$(dirname "$0")/_common.sh"
require_aws

env="${1:-}"; ip="${2:-}"
[ -n "$env" ] && [ -n "$ip" ] || die "usage: 05-route53.sh <uat|prod> <public-ip>"
host="$(host_for "$env")"
printf '%s' "$ip" | grep -Eq '^[0-9]+(\.[0-9]+){3}$' || die "'$ip' is not an IPv4 address"

log "UPSERT A $host -> $ip (TTL 60) in zone $HOSTED_ZONE_ID"
batch="$(mktemp)"; cat > "$batch" <<JSON
{ "Comment": "ACMP $env", "Changes": [ { "Action": "UPSERT",
  "ResourceRecordSet": { "Name": "$host.", "Type": "A", "TTL": 60,
    "ResourceRecords": [ { "Value": "$ip" } ] } } ] }
JSON
cid="$(aws route53 change-resource-record-sets --hosted-zone-id "$HOSTED_ZONE_ID" \
  --change-batch "file://$batch" --query ChangeInfo.Id --output text)"
rm -f "$batch"
log "submitted change $cid — https://$host will resolve to $ip within ~60s"
