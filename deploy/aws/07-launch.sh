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
# The AL2023 AMI is resolved through an SSM public parameter whose name starts with "/", which
# MSYS/Git Bash rewrites into a Windows path before the native aws.exe sees it — GetParameter then
# answers ParameterNotFound for a parameter AWS publishes and maintains. Found by the AC-075 test:
# the first launch of this script only succeeded because the caller happened to have the variable
# exported in their shell, which is exactly the kind of undocumented manual step AC-075 forbids.
# No file:// arguments are used here, so disabling conversion is safe; on Linux it is ignored.
export MSYS_NO_PATHCONV=1
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

# 01-network.sh creates the group as "<project>-<env>-web"; this lookup used to ask for
# "<project>-<env>" and so died with "run 01-network.sh first" even when 01 HAD been run — a
# misleading message pointing at the wrong script. The name here is the one 01 actually creates.
sg_name="${PROJECT}-${env_name}-web"
sg_id="${ACMP_SG_ID:-}"
[ -n "$sg_id" ] || sg_id="$(aws ec2 describe-security-groups --region "$REGION" \
  --filters "Name=group-name,Values=${sg_name}" \
  --query 'SecurityGroups[0].GroupId' --output text 2>/dev/null)"
[ -n "$sg_id" ] && [ "$sg_id" != "None" ] || die "no security group '${sg_name}' — run 01-network.sh first (or set ACMP_SG_ID)"

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
  # CpuCredits=standard is a SPEND control, not a tuning knob (ADR-0034, ratified). T3 defaults to
  # `unlimited`, which bills surplus CPU credits with NO ceiling — the one line item on this account
  # that can exceed the $60 budget without anyone launching anything else. `standard` throttles
  # instead of billing when the credit balance runs out, which is the correct failure mode here.
  #
  # 50 GB (not 30): docker-compose.cloud.yml already sizes Seq retention against "the 50 GB root
  # volume (RISK-015)", and SQL Server + four images + local .bak files do not fit 30 GB. gp3
  # baseline IOPS/throughput are size-independent, so this buys capacity only, at +$1.60/mo.
  instance_id="$(aws ec2 run-instances --region "$REGION" \
    --image-id "$ami" --instance-type "$type" --security-group-ids "$sg_id" \
    --iam-instance-profile "Name=$profile" \
    --credit-specification 'CpuCredits=standard' \
    --block-device-mappings 'DeviceName=/dev/xvda,Ebs={VolumeSize=50,VolumeType=gp3,DeleteOnTermination=true,Encrypted=true}' \
    --metadata-options 'HttpTokens=required,HttpEndpoint=enabled' \
    --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=$name},{Key=Project,Value=ACMP},{Key=Environment,Value=$env_name}]" \
    --query 'Instances[0].InstanceId' --output text)"
  log "launched $instance_id — waiting for it to be running"
  aws ec2 wait instance-running --region "$REGION" --instance-ids "$instance_id"
fi

# --- Elastic IP: the box must come back at the SAME address after a stop/start (AC-082) ----------
# A stopped instance releases its auto-assigned public IPv4 and gets a different one on start, so
# without this every start would need a Route 53 update before $host resolved again — new code on
# the critical path of the very AC it exists to satisfy, plus a TTL wait inside the 10-minute budget.
#
# COST: AWS charges $0.005/hr for EVERY public IPv4 since 2024-02-01, auto-assigned or Elastic, and
# associating an Elastic IP RELEASES the auto-assigned one — so while the instance runs this costs
# nothing extra. The only delta is while the box is STOPPED, where an EIP keeps billing (~$3.65/mo).
# That is the price of AC-082, and stopping UAT is itself the main cost lever, so it is worth it.
#
# Allocated HERE, at launch, per environment — never up front for both. An allocated-but-unassociated
# EIP bills the same rate for an address doing nothing at all.
eip_name="${PROJECT}-${env_name}"
alloc_id="$(aws ec2 describe-addresses --region "$REGION" \
  --filters "Name=tag:Name,Values=$eip_name" \
  --query 'Addresses[0].AllocationId' --output text 2>/dev/null || echo None)"
if [ -z "$alloc_id" ] || [ "$alloc_id" = "None" ]; then
  log "allocating an Elastic IP for $env_name"
  alloc_id="$(aws ec2 allocate-address --region "$REGION" --domain vpc \
    --tag-specifications "ResourceType=elastic-ip,Tags=[{Key=Name,Value=$eip_name},{Key=Project,Value=ACMP},{Key=Environment,Value=$env_name}]" \
    --query 'AllocationId' --output text)"
else
  log "reusing existing Elastic IP allocation $alloc_id for $env_name"
fi
# Idempotent: re-associating the same address with the same instance is a no-op re-bind, so a
# re-run after an instance replacement re-points the address automatically.
aws ec2 associate-address --region "$REGION" \
  --instance-id "$instance_id" --allocation-id "$alloc_id" >/dev/null
log "associated Elastic IP allocation $alloc_id with $instance_id"

public_ip="$(aws ec2 describe-instances --region "$REGION" --instance-ids "$instance_id" \
  --query 'Reservations[0].Instances[0].PublicIpAddress' --output text)"
log "instance $instance_id  ip $public_ip (elastic)  host $host"

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

# CPU-CREDIT ALARM (RSK-013, AC-084). AC-084 reads "the CPUCreditBalance alarm is not tripped" —
# and until this was added, NO CloudWatch alarm existed in the account at all, so that clause was
# vacuously true. An alarm that cannot trip is the same class of un-failable check as the health
# probes that passed on a box nobody could log into (DEF-023), and it was worth more than the $0.10.
#
# This is a PERFORMANCE signal, not a cost one: the instance runs CpuCredits=standard (verified at
# launch, and the reason the budget brake above exists), so exhausting credits throttles the box to
# its 20% baseline rather than producing a surprise bill. There is deliberately no alarm ACTION —
# v1 has no email (CON), the budget action already owns the spend blast radius, and what AC-084
# needs is an alarm whose STATE can be read. put-metric-alarm is upsert-by-name, so re-runs are a
# clean no-op and a replaced instance simply re-points the dimension.
log "arming the CPUCreditBalance alarm for $instance_id (RSK-013 / AC-084)"
aws cloudwatch put-metric-alarm --region "$REGION" \
  --alarm-name "${name}-cpu-credits-low" \
  --alarm-description "t3 CPU credits low on ${name}: the box is about to throttle to its baseline (RSK-013)." \
  --namespace AWS/EC2 --metric-name CPUCreditBalance \
  --dimensions "Name=InstanceId,Value=${instance_id}" \
  --statistic Average --period 300 --evaluation-periods 2 \
  --threshold 50 --comparison-operator LessThanThreshold \
  --treat-missing-data notBreaching \
  `# DEF-031: without --alarm-actions this alarm changes state and tells NOBODY. It was created` \
  `# that way and sat silent, which is the same shape as DEF-030 (a control that looks armed on` \
  `# every check while its notification path does not exist) and the same shape as the health` \
  `# probes that passed on a box nobody could log into. An alarm with no action is a dashboard` \
  `# widget, not a control. Reuses the budget-alerts topic deliberately: it already carries the` \
  `# operator's CONFIRMED email subscription, so this needs no new infrastructure and no second` \
  `# confirmation click. --ok-actions too, so recovery is visible and a stale ALARM in an inbox` \
  `# does not get mentally filed as "still broken".` \
  --alarm-actions "arn:aws:sns:${REGION}:${ACCOUNT_ID}:${PROJECT}-budget-alerts" \
  --ok-actions "arn:aws:sns:${REGION}:${ACCOUNT_ID}:${PROJECT}-budget-alerts" >/dev/null \
  && log "CPUCreditBalance alarm armed (<50 credits for 2x5min)" \
  || log "WARNING: could not arm the CPUCreditBalance alarm — AC-084's alarm clause has nothing to read"

cat <<NEXT

  NEXT (see deploy/runbooks/cloud-provisioning.md for the full path):
    1. point DNS at it ONCE — the address is Elastic, so a stop/start no longer changes it
       and this never needs re-running for this instance:
         bash deploy/aws/05-route53.sh $env_name $public_ip
    2. publish the environment to SSM (if not already done):
         bash deploy/aws/09-put-env.sh $env_name /path/to/.env.cloud
    3. bootstrap the box:
         bash deploy/aws/08-bootstrap-box.sh $env_name <commit-sha>
    4. issue TLS (AC-081), on the box, as root:
         certbot certonly --dns-route53 -d $host \\
           --deploy-hook /opt/acmp/deploy/scripts/certbot-deploy-hook.sh
    5. re-run THIS script after ANY instance replacement so the brake follows the new id AND the
       Elastic IP re-associates. A stale InstanceId does not error — it silently protects nothing.

  COST: this instance bills ~\$30/mo running (t3.medium) + ~\$4 EBS + ~\$3.65 public IPv4.
  Stop it when idle — stopped it costs only EBS + the Elastic IP (~\$7.65/mo). Do NOT leave two
  environments running: 2 x t3.medium alone is ~\$76/mo against a \$60 budget.
NEXT
