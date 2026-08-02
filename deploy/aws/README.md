# AWS landing zone — PH-5 / slice P20 (SL-020)

Idempotent `aws-cli` scripts that provision the AWS foundation for the ACMP cloud
deployment (ADR-0034). **No Terraform/CDK** — two single-host environments do not warrant
an orchestration platform (CON-003, INV-012). Everything here is **operator-run**, not CI
and not run by the assistant.

## Before you start

- Authenticated AWS CLI. `00-account.sh` is run **once as root** to create the `acmp-admin`
  IAM user; **every other script runs as `acmp-admin`** (budget guardrails cannot restrain
  root — RISK-024).
- Set your alert email: `export ACMP_ALERT_EMAIL=you@example.com`.
- Region defaults to `us-east-1` (cheapest — OQ-060). Account, domain and hosted-zone id are
  pinned in `_common.sh` and overridable via `ACMP_*` env vars.

## Run order (once)

| # | Script | Creates | Idempotent |
|---|--------|---------|:---:|
| 00 | `00-account.sh` | `acmp-admin` IAM user, SNS alert topic, `$60`/mo budget + 50/80/100% alerts, budget-action role + deny-new-spend policy | ✓ |
| 01 | `01-network.sh` | one security group per env — **inbound 443 only** (SSM for admin, no SSH; DNS-01 so no port 80) | ✓ |
| 02 | `02-s3.sh` | per-env `recordings` + `backups` buckets — block-public-access, SSE-S3, versioning, mpu-abort + noncurrent-expire lifecycle | ✓ |
| 03 | `03-iam.sh` | per-env **app IAM user** (recordings-bucket-scoped, emits the S3 key) and **instance role/profile** (SSM + ECR pull + S3 + Route53 scoped to that env's record only) | ✓ |
| 04 | `04-ecr.sh` | ECR repos `acmp/{api,web,worker,sqlserver-fts}`, scan-on-push, keep-last-10 | ✓ |
| 05 | `05-route53.sh <env> <ip>` | UPSERT the env's A record → a public IP (also used by the P25 boot-updater) | ✓ |

```bash
export ACMP_ALERT_EMAIL=you@example.com
bash deploy/aws/00-account.sh      # as root, then: aws configure as acmp-admin + add MFA
bash deploy/aws/01-network.sh      # from here on, as acmp-admin
bash deploy/aws/02-s3.sh
bash deploy/aws/03-iam.sh          # copy the emitted S3 keys into each env's deploy/.env
bash deploy/aws/04-ecr.sh
# 05 runs later, once an instance exists: bash deploy/aws/05-route53.sh prod 1.2.3.4
```

## Manual steps the CLI can't fully do

- **MFA on `acmp-admin`** and removing root access keys — console (00 prints the reminder).
- **Confirm the SNS subscription email** (AWS sends it after 00).
- **Two budget actions** — 00 creates the execution role + deny policy and prints the exact
  action definitions to add in the Budgets console (100% ACTUAL → apply deny policy; 100%
  FORECAST → stop `Project=ACMP` EC2 instances). AC-085.
- **Cost Anomaly Detection** — enable in the console (free).

## Isolation (AC-083) — verify after 03

```bash
# the UAT app key must be DENIED on the prod bucket:
AWS_ACCESS_KEY_ID=<uat key> AWS_SECRET_ACCESS_KEY=<uat secret> \
  aws s3 ls s3://acmp-prod-recordings   # -> AccessDenied (expected)
```

## Where this fits

P20 lays the ground; it creates **no EC2 instance and starts no billing beyond the hosted
zone**. Instances, TLS, and the running stack come in P21 (`docker-compose.cloud.yml`),
P22–P24 (S3 code, DR, CD) and P25 (instance bootstrap). Nothing here is destructive or
hard to reverse except the IAM-admin/root change in 00, which is a one-time, recommended
security step.
