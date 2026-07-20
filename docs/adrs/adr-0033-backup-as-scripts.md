# ADR-0033: Backup / restore as host scripts (not an in-app Hangfire job)

- Status: Accepted
- Date: 2026-07-19 (Proposed); ratified 2026-07-19
- Deciders: Architecture Committee execution; operator ratified 2026-07-19
- Context: P18 (Deployment) — Batch 4, realizing `deployment.md §6` (backup and restore)

## Context and Problem Statement

`deployment.md §6` sketches the nightly backup as a Hangfire recurring job (`IBackupService`). Implementing it that
way pulls SQL `BACKUP DATABASE`, `pg_dump`, `mc mirror`, and off-box `scp` into the .NET application. Most of that is
inherently host/infra work (shelling out to `mc`/`scp`, reaching a second VM over SSH), not application logic.

## Decision Drivers

- **Backup is host infra, not app logic** — SQL native backup runs server-side; the MinIO mirror + the off-box copy
  need `mc`/`scp` and a standby host the app has no business holding SSH keys for.
- **Operator transparency** — an operator can read/run/schedule a shell script and see exactly what it does; an
  embedded job is opaque and couples backup cadence to app deploys.
- **Simplicity / least privilege** — keeps SSH credentials and backup tooling out of the app's trust boundary.

## Considered Options

1. **Hangfire in-app job** (the §6 sketch). Rejected: puts `scp`/`mc` and SSH custody inside .NET; couples backup to
   the app lifecycle; harder for an operator to inspect or run ad hoc.
2. **Host scripts run by cron** (chosen): `deploy/scripts/{backup,restore,promote}.sh` orchestrate the running stack
   via `docker compose exec` (sqlcmd, pg_dump) + a throwaway `minio/mc` container + `scp`, scheduled by
   `crontab.example`.
3. **A dedicated backup sidecar container.** Rejected as heavier than cron scripts for a single-VM ≤20-user deploy,
   with no added capability.

## Decision Outcome

Chosen: **option 2.** `backup.sh` takes a SQL Server native compressed `.bak` (critical; a failure exits non-zero so
cron alerting fires), plus a guarded Keycloak `pg_dump` and MinIO `mc mirror`, plus an optional off-box `scp` to the
warm standby; `restore.sh` does `RESTORE … WITH REPLACE` and verifies a `decisions.decisions` row count; `promote.sh`
brings the standby up + restores + smokes. Schedule: nightly full + every-4h business-day (NFR-057 RPO ≤4h).

## Consequences

- **Positive:** backup tooling and SSH custody stay out of the app; operators can inspect/run/schedule everything;
  the SQL `BACKUP`/`RESTORE` core is validated (seed → backup → delete → restore round-trips the data).
- **Negative / accepted:** no Hangfire dashboard visibility or automatic retry for the backup run — alerting is the
  cron exit code + the SQL error, and the durable in-app record is the nightly **integrity-verify** job (ADR-0030),
  which is separate. The MinIO/Keycloak/off-box legs are guarded (degrade rather than fail the SQL backup) and are
  fully exercised only against the running prod stack + a real standby (operator-side; documented in the runbook).

## Traceability

Realizes `deployment.md §6` (backup/restore) + §7 (warm standby). Supports NFR-056 (RTO ≤8h), NFR-057 (RPO ≤4h),
NFR-058 (off-box storage + quarterly restore test). Pairs with ADR-0032 (the scripts read the same file-backed
secrets) and ADR-0030 (nightly integrity verify — the in-app tamper-detection half). No AC verdict change.
