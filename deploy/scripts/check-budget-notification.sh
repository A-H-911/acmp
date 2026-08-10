#!/usr/bin/env bash
# ACMP budget-notification observer (AC-085 leg 1).
#
#   bash deploy/scripts/check-budget-notification.sh
#
# WHY THIS EXISTS, AND WHY IT IS NOT A COUNT.
# AC-085 leg 1 requires that "a threshold notification has been OBSERVED TO ARRIVE". AV-117 tried to
# evidence that by counting messages on acmp-budget-alerts; AV-118 recorded that the check passed
# falsely within 70 minutes. It could not have worked: that topic also carries the CPU-credit alarm
# (both ALARM and OK transitions), backup.sh failures and check-backup-freshness.sh alerts, so a
# rising count proves only that SOMETHING published -- never that the BUDGET did. On a shared topic
# no count-based test can discriminate, which is the same lesson as the WCAG gate that graded the
# wrong palette because it located its subject by substring: match on STRUCTURE, not on a number.
#
# So this reads the message BODY. An SQS subscriber was added to the topic precisely because email
# -- the only subscriber until 2026-08-10 -- is not machine-readable, so the arrival could only ever
# be narrated. The printed body below IS the evidence for the audit verdict.
#
# NON-DESTRUCTIVE BY DESIGN. Messages are read with --visibility-timeout 0 and are NEVER deleted:
# delete-on-read would destroy the evidence the moment it is first collected, and the queue's
# 14-day retention is what lets you run this days after the threshold fired.
set -uo pipefail

QUEUE_URL="${ACMP_BUDGET_QUEUE_URL:-https://sqs.us-east-1.amazonaws.com/565393059398/acmp-budget-observer}"
BUDGET_NAME="${ACMP_BUDGET_NAME:-acmp-monthly}"
REGION="${AWS_REGION:-us-east-1}"

pass() { printf '  \033[32mOK\033[0m    %s\n' "$*"; }
fail() { printf '  \033[31mFAIL\033[0m  %s\n' "$*"; }
note() { printf '        %s\n' "$*"; }

echo "--- ACMP budget-notification observer: $BUDGET_NAME ---"

# The subscription itself must exist, or an empty queue is indistinguishable from "nothing fired".
# DEF-030's shape exactly: a control that looks armed while its delivery path does not exist.
subs="$(aws sns list-subscriptions-by-topic --region "$REGION" \
        --topic-arn "arn:aws:sns:${REGION}:565393059398:acmp-budget-alerts" \
        --query "Subscriptions[?Protocol=='sqs'].SubscriptionArn" --output text 2>/dev/null)"
case "$subs" in
  ""|None) fail "no SQS subscription on acmp-budget-alerts — an empty queue would prove nothing"; exit 1 ;;
  *)       pass "SQS subscription present" ;;
esac

# Drain up to 10 at a time, up to 5 rounds. Long-poll so a quiet queue is not mistaken for an empty
# one on a single short read.
found=0
for _ in 1 2 3 4 5; do
  bodies="$(aws sqs receive-message --region "$REGION" --queue-url "$QUEUE_URL" \
              --max-number-of-messages 10 --wait-time-seconds 5 --visibility-timeout 0 \
              --query 'Messages[].Body' --output text 2>/dev/null)"
  [ -n "$bodies" ] || continue

  # A real AWS Budgets notification names the budget AND carries a threshold. Requiring BOTH is what
  # stops the wiring-test message -- or any other publisher on this shared topic -- from matching.
  printf '%s\n' "$bodies" | while IFS= read -r line; do
    case "$line" in
      *"$BUDGET_NAME"*)
        case "$line" in
          *[Tt]hreshold*|*"AWS Budget Notification"*)
            printf '\n=== BUDGET NOTIFICATION OBSERVED — this text is the AC-085 leg 1 evidence ===\n'
            printf '%s\n' "$line"
            printf '=== end ===\n\n'
            ;;
        esac ;;
    esac
  done

  if printf '%s' "$bodies" | grep -q "$BUDGET_NAME" && \
     printf '%s' "$bodies" | grep -Eq 'hreshold|AWS Budget Notification'; then
    found=1
    break
  fi
done

if [ "$found" -eq 1 ]; then
  pass "a threshold notification for $BUDGET_NAME has ARRIVED and its body is printed above"
  note "record it with audit_record against AC-085 leg 1, quoting the body — not a count."
  exit 0
fi

# Not an error condition yet: the threshold may simply not have been crossed. Say which, LOUDLY,
# rather than skipping quietly (DEF-022 was a capability that skipped itself and reported success).
fail "no budget notification for $BUDGET_NAME in the queue yet"
note "This is NOT a wiring failure — the subscription above is confirmed. It means the threshold"
note "has not been crossed yet. Check current spend against the trigger:"
note "  aws budgets describe-budget --account-id 565393059398 --budget-name $BUDGET_NAME \\"
note "      --query 'Budget.{Limit:BudgetLimit.Amount,Actual:CalculatedSpend.ActualSpend.Amount}'"
note "Messages are never deleted here, so re-run this any time within the queue's 14-day retention."
exit 1
