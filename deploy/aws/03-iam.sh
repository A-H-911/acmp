#!/usr/bin/env bash
# 03 — per-environment IAM (SL-020; RISK-018, RISK-021, AC-083). Strict per-env isolation:
#
#   IAM USER  acmp-<env>-app      -> the app's static S3 key (operator's choice, ADR-0035).
#                                    Scoped to the <env> RECORDINGS bucket ONLY. Key emitted
#                                    for you to place in that env's deploy/.env.
#   IAM ROLE  acmp-<env>-instance -> the EC2 instance profile. Carries SSM core (no SSH),
#                                    ECR pull, GetChange/ListHostedZones, ChangeResourceRecordSets
#                                    scoped to THIS env's DNS record only (so a compromised UAT
#                                    box cannot repoint prod, RISK-018), and S3 on this env's
#                                    recordings + backups buckets (for backup.sh / aws s3 cp).
set -euo pipefail
. "$(dirname "$0")/_common.sh"
require_aws

ECR_PREFIX="arn:aws:ecr:${REGION}:${ACCOUNT_ID}:repository/${PROJECT}"

for env in "${ENVS[@]}"; do
  host="$(host_for "$env")"; rec="$(bucket_for "$env")"; bak="$(backup_bucket_for "$env")"

  # --- app IAM user, recordings-bucket-scoped -----------------------------------------
  APP_USER="${PROJECT}-${env}-app"
  if ! aws iam get-user --user-name "$APP_USER" >/dev/null 2>&1; then
    log "creating app user $APP_USER"
    aws iam create-user --user-name "$APP_USER" --tags $TAGS Key=Env,Value="$env" >/dev/null
  fi
  aws iam put-user-policy --user-name "$APP_USER" --policy-name s3-recordings --policy-document "$(cat <<JSON
{ "Version":"2012-10-17","Statement":[
  {"Effect":"Allow","Action":["s3:GetObject","s3:PutObject","s3:DeleteObject"],"Resource":"arn:aws:s3:::${rec}/*"},
  {"Effect":"Allow","Action":["s3:ListBucket","s3:GetBucketLocation"],"Resource":"arn:aws:s3:::${rec}"} ] }
JSON
)"
  # Emit a key only if the user has none yet (idempotent — never mints duplicates).
  if [ -z "$(aws iam list-access-keys --user-name "$APP_USER" --query 'AccessKeyMetadata[0].AccessKeyId' --output text 2>/dev/null | grep -v None)" ]; then
    kj="$(aws iam create-access-key --user-name "$APP_USER" --output json)"
    ak="$(printf '%s' "$kj" | sed -n 's/.*"AccessKeyId": *"\([^"]*\)".*/\1/p')"
    sk="$(printf '%s' "$kj" | sed -n 's/.*"SecretAccessKey": *"\([^"]*\)".*/\1/p')"
    emit_secret "$env S3 key (deploy/.env for $env)" "Minio__AccessKey=$ak  Minio__SecretKey/secret=$sk  bucket=$rec"
  else
    log "$APP_USER already has an access key (rotate manually if needed)"
  fi

  # --- instance role + profile --------------------------------------------------------
  ROLE="${PROJECT}-${env}-instance"
  if ! aws iam get-role --role-name "$ROLE" >/dev/null 2>&1; then
    log "creating instance role $ROLE"
    aws iam create-role --role-name "$ROLE" --tags $TAGS Key=Env,Value="$env" \
      --assume-role-policy-document '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"ec2.amazonaws.com"},"Action":"sts:AssumeRole"}]}' >/dev/null
    aws iam attach-role-policy --role-name "$ROLE" \
      --policy-arn arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore >/dev/null   # SSM, no SSH
  fi
  aws iam put-role-policy --role-name "$ROLE" --policy-name acmp-runtime --policy-document "$(cat <<JSON
{ "Version":"2012-10-17","Statement":[
  {"Sid":"EcrAuth","Effect":"Allow","Action":"ecr:GetAuthorizationToken","Resource":"*"},
  {"Sid":"EcrPull","Effect":"Allow","Action":["ecr:BatchGetImage","ecr:GetDownloadUrlForLayer","ecr:BatchCheckLayerAvailability"],"Resource":"${ECR_PREFIX}/*"},
  {"Sid":"Route53Read","Effect":"Allow","Action":["route53:ListHostedZones","route53:GetChange"],"Resource":"*"},
  {"Sid":"Route53WriteThisHostOnly","Effect":"Allow","Action":"route53:ChangeResourceRecordSets","Resource":"arn:aws:route53:::hostedzone/${HOSTED_ZONE_ID}",
   "Condition":{"ForAllValues:StringEquals":{"route53:ChangeResourceRecordSetsNormalizedRecordNames":["${host}"],"route53:ChangeResourceRecordSetsRecordTypes":["A"]}}},
  {"Sid":"S3Buckets","Effect":"Allow","Action":["s3:GetObject","s3:PutObject","s3:DeleteObject","s3:ListBucket","s3:GetBucketLocation"],
   "Resource":["arn:aws:s3:::${rec}","arn:aws:s3:::${rec}/*","arn:aws:s3:::${bak}","arn:aws:s3:::${bak}/*"]} ] }
JSON
)"
  if ! aws iam get-instance-profile --instance-profile-name "$ROLE" >/dev/null 2>&1; then
    aws iam create-instance-profile --instance-profile-name "$ROLE" >/dev/null
    aws iam add-role-to-instance-profile --instance-profile-name "$ROLE" --role-name "$ROLE" >/dev/null
  fi
  log "$env: instance profile $ROLE ready (DNS scoped to $host only)"
done
log "done. Attach each env's instance profile at launch (P25). App S3 keys emitted above go in deploy/.env."
