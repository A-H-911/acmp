#!/usr/bin/env bash
# 07 — launch an environment's EC2 instance AND arm its spend brake in the same run (SL-025, OQ-064).
#
#   bash deploy/aws/07-launch.sh <uat|prod> [instance-type]
#
# WHY THE BUDGET ACTION IS CREATED HERE AND NOT IN 00-account.sh
# 00 arms the IAM half of the brake (a deny policy attached to acmp-admin at 100% of budget). The
# EC2 half CANNOT exist that early: `aws budgets create-budget-action --generate-cli-skeleton`
# shows SsmActionDefinition as {ActionSubType, Region, InstanceIds} -- a list of INSTANCE IDS, with
# no tag filter anywhere in the API (OQ-064, re-verified against the live API). So the action is
# unexpressible until an instance exists, and it is pinned to specific ids once it does.
#
# That pinning is the whole reason this script owns it. The alternative considered was a runbook
# line saying "remember to re-point the budget action after replacing an instance". A stale
# InstanceId does not error -- it silently protects nothing, which is the same failure shape as
# DEF-017 (backups never written, deployment looks backed up) and DEF-018 (search returns nothing,
# logs clean). This script already knows the id it just created, so it re-points automatically and
# the manual step ceases to exist.
#
# WHAT THIS DOES NOT PROTECT AGAINST: the IAM deny binds the acmp-admin USER, and the account ROOT
# user cannot be restricted by IAM policy at all (only an Organizations SCP could, and this is a
# standalone account). If you are operating as root, the brake reads armed and constrains nobody.
set -euo pipefail
. "$(dirname "$0")/_common.sh"
require_aws

env_name="${1:-}"
case "$env_name" in uat|prod) ;; *) die "usage: 07-launch.sh <uat|prod> [instance-type]";; esac
# t3.medium: SQL Server alone has a documented 2 GiB container floor, so t3.micro/small are
# impossible. The cloud compose budgets 3584 MiB of limits against 4 GiB, leaving ~512 MiB for the
# host. Measured locally at idle, Keycloak sits at 98.8% of its 448 MiB cap (AC-084/AV-092) -- if
# this instance OOMs under load, that allocation is the first to raise, funded from that headroom.
type="${2:-t3.medium}"
host="$(host_for "$env_name")"
name="${PROJECT}-${env_name}"
profile="${PROJECT}-${env_name}-instance"

sg_id="${ACMP_SG_ID:-}"
[ -n "$sg_id" ] || sg_id="$(aws ec2 describe-security-groups --region "$REGION" \
  --filters "Name=group-name,Values=${PROJECT}-${env_name}" \
  --query 'SecurityGroups[0].GroupId' --output text 2>/dev/null)"
[ -n "$sg_id" ] && [ "$sg_id" != "None" ] || die "no security group '${PROJECT}-${env_name}' — run 01-network.sh first"

# --- idempotence: never launch a second instance for an environment ------------------------
existing="$(aws ec2 describe-instances --region "$REGION" \
  --filters "Name=tag:Name,Values=$name" "Name=instance-state-name,Values=pending,running,stopping,stopped" \
  --query 'Reservations[].Instances[].InstanceId' --output text 2>/dev/null || true)"

if [ -n "$existing" ] && [ "$existing" != "None" ]; then
  instance_id="$(printf '%s' "$existing" | awk '{print $1}')"
  log "instance for $env_name already exists: $instance_id (not launching another)"
else
  ami="${ACMP_AMI_ID:-}"
  # Amazon Linux 2023 x86_64 via SSM public parameter — resolved, not hard-coded, so this does not
  # rot into a stale AMI id the way a pinned one silently would.
  [ -n "$ami" ] || ami="$(aws ssm get-parameter --region "$REGION" \
      --name /aws/service/ami-amazon-linux-latest/al2023-ami-kernel-default-x86_64 \
      --query 'Parameter.Value' --output text)"
  log "launching $name ($type, $ami, sg $sg_id, profile $profile)"
  instance_id="$(aws ec2 run-instances --region "$REGION" \
    --image-id "$ami" --instance-type "$type" --security-group-ids "$sg_id" \
    --iam-instance-profile "Name=$profile" \
    --block-device-mappings 'DeviceName=/dev/xvda,Ebs={VolumeSize=30,VolumeType=gp3,DeleteOnTermination=true,Encrypted=true}' \
    --metadata-options 'HttpTokens=required,HttpEndpoint=enabled' \
    --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=$name},{Key=Project,Value=ACMP},{Key=Environment,Value=$env_name}]" \
    --query 'Instances[0].InstanceId' --output text)"
  log "launched $instance_id — waiting for it to be running"
  aws ec2 wait instance-running --region "$REGION" --instance-ids "$instance_id"
fi

public_ip="$(aws ec2 describe-instances --region "$REGION" --instance-ids "$instance_id" \
  --query 'Reservations[0].Instances[0].PublicIpAddress' --output text)"
log "instance $instance_id  ip $public_ip  host $host"

# --- arm the EC2 half of the spend brake, pinned to every ACMP instance that exists ---------
# Collected across BOTH environments so launching prod does not silently drop uat from the action.
all_ids="$(aws ec2 describe-instances --region "$REGION" \
  --filters "Name=tag:Project,Values=ACMP" "Name=instance-state-name,Values=pending,running,stopping,stopped" \
  --query 'Reservations[].Instances[].InstanceId' --output text | tr '\t' '\n' | sort -u | tr '\n' ' ')"
log "budget action will target: $all_ids"

role_arn="arn:aws:iam::${ACCOUNT_ID}:role/${PROJECT}-budget-action"
ids_json="$(printf '%s' "$all_ids" | tr -s ' ' '\n' | sed '/^$/d' | sed 's/.*/"&"/' | paste -sd, -)"
def="{\"SsmActionDefinition\":{\"ActionSubType\":\"STOP_EC2_INSTANCES\",\"Region\":\"$REGION\",\"InstanceIds\":[$ids_json]}}"

existing_action="$(aws budgets describe-budget-actions-for-budget --account-id "$ACCOUNT_ID" \
  --budget-name "${PROJECT}-monthly" \
  --query "Actions[?ActionType=='RUN_SSM_DOCUMENTS'].ActionId" --output text 2>/dev/null || true)"

if [ -n "$existing_action" ] && [ "$existing_action" != "None" ]; then
  log "re-pointing existing budget action $existing_action at the current instance ids"
  aws budgets update-budget-action --account-id "$ACCOUNT_ID" --budget-name "${PROJECT}-monthly" \
    --action-id "$existing_action" --definition "$def" >/dev/null
else
  log "creating the STOP_EC2_INSTANCES budget action at 100% ACTUAL"
  aws budgets create-budget-action --account-id "$ACCOUNT_ID" --budget-name "${PROJECT}-monthly" \
    --notification-type ACTUAL --action-type RUN_SSM_DOCUMENTS \
    --action-threshold "ActionThresholdValue=100,ActionThresholdType=PERCENTAGE" \
    --definition "$def" --execution-role-arn "$role_arn" --approval-model AUTOMATIC \
    --subscribers "SubscriptionType=SNS,Address=arn:aws:sns:${REGION}:${ACCOUNT_ID}:${PROJECT}-budget-alerts" >/dev/null
fi
log "spend brake armed against the live instance ids (OQ-064)"

cat <<NEXT

  NEXT (still manual, deliberately):
    1. point DNS at it:   bash deploy/aws/05-route53.sh $env_name $public_ip
    2. get the stack onto the box (P24 part 2), then:
         deploy/scripts/promote-image.sh $env_name <commit-sha> /path/to/.env
    3. issue TLS (AC-081):  certbot --dns-route53 -d $host
    4. re-run THIS script after ANY instance replacement so the brake follows the new id.
       A stale InstanceId does not error — it silently protects nothing.
NEXT
