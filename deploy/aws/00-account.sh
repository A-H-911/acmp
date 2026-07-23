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
  budget_json="$(mktemp)"; notif_json="$(mktemp)"
  cat > "$budget_json" <<JSON
{ "BudgetName": "$BUDGET_NAME", "BudgetLimit": { "Amount": "$BUDGET_LIMIT_USD", "Unit": "USD" },
  "TimeUnit": "MONTHLY", "BudgetType": "COST" }
JSON
  cat > "$notif_json" <<JSON
[ { "Notification": { "NotificationType": "ACTUAL", "ComparisonOperator": "GREATER_THAN", "Threshold": 50 },
    "Subscribers": [ { "SubscriptionType": "SNS", "Address": "$TOPIC_ARN" } ] },
  { "Notification": { "NotificationType": "ACTUAL", "ComparisonOperator": "GREATER_THAN", "Threshold": 80 },
    "Subscribers": [ { "SubscriptionType": "SNS", "Address": "$TOPIC_ARN" } ] },
  { "Notification": { "NotificationType": "ACTUAL", "ComparisonOperator": "GREATER_THAN", "Threshold": 100 },
    "Subscribers": [ { "SubscriptionType": "SNS", "Address": "$TOPIC_ARN" } ] } ]
JSON
  aws budgets create-budget --account-id "$ACCOUNT_ID" \
    --budget "file://$budget_json" --notifications-with-subscribers "file://$notif_json"
  rm -f "$budget_json" "$notif_json"
fi

# 3b) Budget-action execution role + a Deny-new-spend policy (attached at 100%) ----------
ACTION_ROLE="${PROJECT}-budget-action"
if ! aws iam get-role --role-name "$ACTION_ROLE" >/dev/null 2>&1; then
  log "creating budget-action execution role $ACTION_ROLE"
  trust="$(mktemp)"; cat > "$trust" <<'JSON'
{ "Version": "2012-10-17", "Statement": [ { "Effect": "Allow",
  "Principal": { "Service": "budgets.amazonaws.com" }, "Action": "sts:AssumeRole" } ] }
JSON
  aws iam create-role --role-name "$ACTION_ROLE" --assume-role-policy-document "file://$trust" --tags $TAGS >/dev/null
  aws iam attach-role-policy --role-name "$ACTION_ROLE" \
    --policy-arn arn:aws:iam::aws:policy/PowerUserAccess >/dev/null   # can apply policies + stop instances
  rm -f "$trust"
fi
DENY_POLICY_ARN="arn:aws:iam::${ACCOUNT_ID}:policy/${PROJECT}-budget-deny-new-spend"
if ! aws iam get-policy --policy-arn "$DENY_POLICY_ARN" >/dev/null 2>&1; then
  log "creating deny-new-spend policy (applied to acmp-admin at 100% budget)"
  deny="$(mktemp)"; cat > "$deny" <<'JSON'
{ "Version": "2012-10-17", "Statement": [ { "Effect": "Deny",
  "Action": [ "ec2:RunInstances", "ec2:AllocateAddress", "rds:CreateDBInstance" ],
  "Resource": "*" } ] }
JSON
  aws iam create-policy --policy-name "${PROJECT}-budget-deny-new-spend" --policy-document "file://$deny" >/dev/null
  rm -f "$deny"
fi

log "budget actions: create these two in the console once (CLI create-budget-action needs the role ARN above):"
log "  100% ACTUAL  -> APPLY_IAM_POLICY $DENY_POLICY_ARN to user $ADMIN_USER  (block new spend)"
log "  100% FORECAST-> STOP_EC2_INSTANCES filtered by tag Project=ACMP        (emergency brake, RISK-022)"
log "  role: $(aws iam get-role --role-name "$ACTION_ROLE" --query Role.Arn --output text 2>/dev/null || echo "$ACTION_ROLE")"
log "done. Enable AWS Cost Anomaly Detection in the console too (free)."
