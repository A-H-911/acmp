#!/usr/bin/env bash
# 02 — S3 buckets (SL-020 / ADR-0035; AC-078, AC-080, AC-083). Per environment:
#   <project>-<env>-recordings  — meeting recordings + topic attachments (replaces MinIO)
#   <project>-<env>-backups     — off-instance SQL .bak copies (NFR-058, RISK-015)
# Every bucket: Block Public Access ON, SSE-S3 default encryption, versioning ON
# (versioning replaces MinIO's mc-mirror backup leg), and an AbortIncompleteMultipartUpload
# lifecycle rule (RISK/M-6: the Minio SDK multiparts a 2 GiB upload; a failed part bills forever).
set -euo pipefail
. "$(dirname "$0")/_common.sh"
require_aws

make_bucket() { # bucket-name
  local b="$1"
  if aws s3api head-bucket --bucket "$b" >/dev/null 2>&1; then
    log "bucket $b exists"
  else
    log "creating bucket $b"
    # us-east-1 must NOT pass a LocationConstraint (it is the API default); other regions must.
    if [ "$REGION" = "us-east-1" ]; then
      aws s3api create-bucket --bucket "$b" >/dev/null
    else
      aws s3api create-bucket --bucket "$b" \
        --create-bucket-configuration LocationConstraint="$REGION" >/dev/null
    fi
    aws s3api put-bucket-tagging --bucket "$b" \
      --tagging 'TagSet=[{Key=Project,Value=ACMP}]' >/dev/null
  fi
  aws s3api put-public-access-block --bucket "$b" --public-access-block-configuration \
    BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true >/dev/null
  aws s3api put-bucket-encryption --bucket "$b" --server-side-encryption-configuration \
    '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}' >/dev/null
  aws s3api put-bucket-versioning --bucket "$b" --versioning-configuration Status=Enabled >/dev/null
  aws s3api put-bucket-lifecycle-configuration --bucket "$b" --lifecycle-configuration \
    '{"Rules":[
       {"ID":"abort-incomplete-mpu","Status":"Enabled","Filter":{},
        "AbortIncompleteMultipartUpload":{"DaysAfterInitiation":7}},
       {"ID":"expire-noncurrent","Status":"Enabled","Filter":{},
        "NoncurrentVersionExpiration":{"NoncurrentDays":90}}]}' >/dev/null
  log "  $b: public-access blocked, SSE-S3, versioning on, mpu-abort 7d, noncurrent-expire 90d"
}

for env in "${ENVS[@]}"; do
  make_bucket "$(bucket_for "$env")"
  make_bucket "$(backup_bucket_for "$env")"
done
log "done. Buckets are private; access is via the per-env IAM users created in 03-iam.sh."
