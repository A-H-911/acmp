#!/usr/bin/env bash
# 00 — account hygiene + spend controls (SL-020; RISK-022, RISK-024, AC-085).
#
# Does three things, all idempotent:
#   1. Creates an IAM admin user 'acmp-admin' (so you stop using root — budget actions
#      and IAM guardrails cannot restrain root, RISK-024). You then attach an MFA device
#      and `aws configure` as this user; root keeps only break-glass console access.
#   2. Creates an SNS topic + email subscription for budget alerts.
#   3. Creates a monthly cost Budget with 50/80/100% notifications, plus two budget
#      ACTIONS: a Deny-IAM policy at 100% (blocks new spend) and an EC2 auto-stop of
#      Project=ACMP instances at 100% forecast (the emergency brake, AC-085).
#
# Run this FIRST, as root (one time), then switch to acmp-admin for 01..05.
#   ACMP_ALERT_EMAIL=you@example.com bash deploy/aws/00-account.sh
set -euo pipefail
. "$(dirname "$0")/_common.sh"
require_aws
[ -n "$ALERT_EMAIL" ] || die "set ACMP_ALERT_EMAIL=you@example.com before running (budget alerts need it)"

ADMIN_USER="${PROJECT}-admin"

# 1) IAM admin user (move off root) ------------------------------------------------------
if aws iam get-user --user-name "$ADMIN_USER" >/dev/null 2>&1; then
  log "IAM user $ADMIN_USER already exists"
else
  log "creating IAM admin user $ADMIN_USER"
  aws iam create-user --user-name "$ADMIN_USER" --tags $TAGS >/dev/null
  aws iam attach-user-policy --user-name "$ADMIN_USER" \
    --policy-arn arn:aws:iam::aws:policy/AdministratorAccess
  key_json="$(aws iam create-access-key --user-name "$ADMIN_USER" --output json)"
  ak="$(printf '%s' "$key_json" | sed -n 's/.*"AccessKeyId": *"\([^"]*\)".*/\1/p')"
  sk="$(printf '%s' "$key_json" | sed -n 's/.*"SecretAccessKey": *"\([^"]*\)".*/\1/p')"
  emit_secret "acmp-admin access key" "aws_access_key_id=$ak  aws_secret_access_key=$sk"
  log "NEXT: 'aws configure' with the key above, add an MFA device in the console, then"
  log "      remove root access keys and run 01..05 as $ADMIN_USER."
fi

# 2) SNS topic for alerts ----------------------------------------------------------------
TOPIC_ARN="$(aws sns create-topic --name "${PROJECT}-budget-alerts" \
  --tags $TAGS --query TopicArn --output text)"           # create-topic is idempotent
if ! aws sns list-subscriptions-by-topic --topic-arn "$TOPIC_ARN" \
      --query "Subscriptions[?Endpoint=='$ALERT_EMAIL']" --output text | grep -q .; then
  aws sns subscribe --topic-arn "$TOPIC_ARN" --protocol email --notification-endpoint "$ALERT_EMAIL" >/dev/null
  log "SNS: confirm the subscription email just sent to $ALERT_EMAIL"
fi

# 3) Budget + notifications + actions ----------------------------------------------------
BUDGET_NAME="${PROJECT}-monthly"
if aws budgets describe-budget --account-id "$ACCOUNT_ID" --budget-name "$BUDGET_NAME" >/dev/null 2>&1; then
  log "budget $BUDGET_NAME already exists (leaving as-is)"
else
  log "creating budget $BUDGET_NAME at \$$BUDGET_LIMIT_USD/mo with 50/80/100% alerts"
  # JSON is passed INLINE, never via a temp file. `mktemp` returns a POSIX path and a native Windows
  # aws.exe cannot open file:///tmp/... — the first live run died exactly there, and `set -e` then took
  # the budget ACTIONS below with it, leaving a provisioned account with NO spend guardrail at all.
  budget_json="$(cat <<JSON
{ "BudgetName": "$BUDGET_NAME", "BudgetLimit": { "Amount": "$BUDGET_LIMIT_USD", "Unit": "USD" },
  "TimeUnit": "MONTHLY", "BudgetType": "COST" }
JSON
)"
  notif_json="$(cat <<JSON
[ { "Notification": { "NotificationType": "ACTUAL", "ComparisonOperator": "GREATER_THAN", "Threshold": 50 },
    "Subscribers": [ { "SubscriptionType": "SNS", "Address": "$TOPIC_ARN" } ] },
  { "Notification": { "NotificationType": "ACTUAL", "ComparisonOperator": "GREATER_THAN", "Threshold": 80 },
    "Subscribers": [ { "SubscriptionType": "SNS", "Address": "$TOPIC_ARN" } ] },
  { "Notification": { "NotificationType": "ACTUAL", "ComparisonOperator": "GREATER_THAN", "Threshold": 100 },
    "Subscribers": [ { "SubscriptionType": "SNS", "Address": "$TOPIC_ARN" } ] } ]
JSON
)"
  aws budgets create-budget --account-id "$ACCOUNT_ID" \
    --budget "$budget_json" --notifications-with-subscribers "$notif_json"

fi

# 3b) Budget-action execution role + a Deny-new-spend policy (attached at 100%) ----------
ACTION_ROLE="${PROJECT}-budget-action"
if ! aws iam get-role --role-name "$ACTION_ROLE" >/dev/null 2>&1; then
  log "creating budget-action execution role $ACTION_ROLE"
  trust="$(cat <<'JSON'
{ "Version": "2012-10-17", "Statement": [ { "Effect": "Allow",
  "Principal": { "Service": "budgets.amazonaws.com" }, "Action": "sts:AssumeRole" } ] }
JSON
)"
  aws iam create-role --role-name "$ACTION_ROLE" --assume-role-policy-document "$trust" --tags $TAGS >/dev/null
  aws iam attach-role-policy --role-name "$ACTION_ROLE" \
    --policy-arn arn:aws:iam::aws:policy/PowerUserAccess >/dev/null   # can apply policies + stop instances

fi
DENY_POLICY_ARN="arn:aws:iam::${ACCOUNT_ID}:policy/${PROJECT}-budget-deny-new-spend"
if ! aws iam get-policy --policy-arn "$DENY_POLICY_ARN" >/dev/null 2>&1; then
  log "creating deny-new-spend policy (applied to acmp-admin at 100% budget)"
  deny="$(cat <<'JSON'
{ "Version": "2012-10-17", "Statement": [ { "Effect": "Deny",
  "Action": [ "ec2:RunInstances", "ec2:AllocateAddress", "rds:CreateDBInstance" ],
  "Resource": "*" } ] }
JSON
)"
  aws iam create-policy --policy-name "${PROJECT}-budget-deny-new-spend" --policy-document "$deny" >/dev/null

fi

# 3c) The 100% deny action, created HERE rather than left to console clicks. The earlier note claimed
#     the CLI could not do this because it "needs the role ARN" — it needs exactly the ARN we just made,
#     so there was never a reason for a human step. A guardrail that depends on someone remembering to
#     click is a guardrail you find missing during the incident it existed for. Idempotent: skip if any
#     APPLY_IAM_POLICY action already exists on this budget.
ROLE_ARN="$(aws iam get-role --role-name "$ACTION_ROLE" --query Role.Arn --output text)"
if aws budgets describe-budget-actions-for-budget --account-id "$ACCOUNT_ID" --budget-name "$BUDGET_NAME" \
     --query "Actions[?ActionType=='APPLY_IAM_POLICY'].ActionId" --output text 2>/dev/null | grep -q .; then
  log "budget deny-action already attached to $BUDGET_NAME"
else
  log "attaching 100%-ACTUAL deny action to $BUDGET_NAME"
  aws budgets create-budget-action --account-id "$ACCOUNT_ID" --budget-name "$BUDGET_NAME" \
    --notification-type ACTUAL --action-type APPLY_IAM_POLICY \
    --action-threshold '{"ActionThresholdValue":100,"ActionThresholdType":"PERCENTAGE"}' \
    --definition "{\"IamActionDefinition\":{\"PolicyArn\":\"$DENY_POLICY_ARN\",\"Users\":[\"$ADMIN_USER\"]}}" \
    --execution-role-arn "$ROLE_ARN" --approval-model AUTOMATIC \
    --subscribers "[{\"SubscriptionType\":\"SNS\",\"Address\":\"$TOPIC_ARN\"}]" >/dev/null
fi

# The EC2 emergency brake (RISK-022) CANNOT be created here, and not because of the CLI:
#   aws budgets create-budget-action --generate-cli-skeleton shows SsmActionDefinition as
#   {ActionSubType, Region, InstanceIds} — a list of INSTANCE IDS. There is no tag filter, so the
#   "filtered by tag Project=ACMP" this script used to print was never possible. It has to be created
#   AFTER P25 launches instances, naming them, and RE-POINTED whenever an instance is replaced — which
#   for the on-demand UAT box is a recurring chore, not a one-off. See AC-085.
log "  EC2 auto-stop action: deferred to P25 — it takes explicit InstanceIds, not a tag filter."
log "done. Enable AWS Cost Anomaly Detection in the console too (free)."
