#!/usr/bin/env bash
# 01 — security groups (SL-020; RISK-023, RISK-005). One SG per environment in the default VPC.
#
# INBOUND = 443 (public HTTPS) ONLY. No port 22 (we use SSM Session Manager, OQ/Q6 default —
# the instance role in 03-iam.sh carries AmazonSSMManagedInstanceCore; SSM needs no inbound).
# No port 80 either: TLS is issued via Let's Encrypt DNS-01 (P25), so HTTP-01 is never needed.
# OUTBOUND = all (SSM + ECR pull + S3 + Let's Encrypt + Webex all dial out on 443).
set -euo pipefail
. "$(dirname "$0")/_common.sh"
require_aws

VPC_ID="$(aws ec2 describe-vpcs --filters Name=isDefault,Values=true \
  --query 'Vpcs[0].VpcId' --output text)"
[ "$VPC_ID" != "None" ] || die "no default VPC in $REGION — create one or set a VPC explicitly"
log "default VPC: $VPC_ID"

for env in "${ENVS[@]}"; do
  SG_NAME="${PROJECT}-${env}-web"
  sg_id="$(aws ec2 describe-security-groups \
    --filters Name=group-name,Values="$SG_NAME" Name=vpc-id,Values="$VPC_ID" \
    --query 'SecurityGroups[0].GroupId' --output text 2>/dev/null || echo None)"
  if [ "$sg_id" = "None" ] || [ -z "$sg_id" ]; then
    log "creating security group $SG_NAME"
    sg_id="$(aws ec2 create-security-group --group-name "$SG_NAME" \
      --description "ACMP $env web ingress (443 only; SSM for admin)" \
      --vpc-id "$VPC_ID" --query GroupId --output text)"
    aws ec2 create-tags --resources "$sg_id" --tags $TAGS Key=Env,Value="$env"
  else
    log "security group $SG_NAME exists ($sg_id)"
  fi
  # Idempotent ingress: authorize 443, ignore the DuplicatePermission error on re-run.
  aws ec2 authorize-security-group-ingress --group-id "$sg_id" \
    --protocol tcp --port 443 --cidr 0.0.0.0/0 >/dev/null 2>&1 \
    && log "  + tcp/443 from 0.0.0.0/0" || log "  tcp/443 already open"
  log "  $env SG = $sg_id  (record this for the instance launch in P25)"
done
log "done. No SSH (22) and no HTTP (80) rules by design (SSM + DNS-01)."
