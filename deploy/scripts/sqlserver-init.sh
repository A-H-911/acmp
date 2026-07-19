#!/usr/bin/env bash
# ACMP least-priv provisioning (P18a Batch 3, ADR-0031). One-shot, runs as `sa`, BEFORE the --migrate-only step.
# Creates the database and the least-privilege runtime login `acmp_svc`:
#   db_datareader + db_datawriter + EXECUTE + member of the `acmp_app` role — but NOT db_owner and NOT sysadmin.
# Because acmp_svc is a non-sysadmin member of acmp_app, the DENY UPDATE/DELETE on schema::audit that the
# Audit_DenyMutation migration applies to that role BINDS (sysadmin/db_owner would bypass it). The acmp_app role is
# pre-created here so acmp_svc can be enrolled before migrate; the migration's `IF ... IS NULL CREATE ROLE` is a
# no-op afterwards. Idempotent — safe to re-run on every deploy.
set -euo pipefail

DB_NAME="${ACMP_DB_NAME:-Acmp}"
DB_SERVER="${ACMP_DB_SERVER:-sqlserver}"
export SQLCMDPASSWORD="$(cat /run/secrets/mssql_sa_password)"   # sa connection (not on the command line)
SVC_PW="$(cat /run/secrets/acmp_svc_password)"
SQLCMD="/opt/mssql-tools18/bin/sqlcmd -S ${DB_SERVER} -U sa -C -No -b"

# 1) database + server login (master scope)
$SQLCMD -Q "IF DB_ID('${DB_NAME}') IS NULL CREATE DATABASE [${DB_NAME}];"
$SQLCMD -Q "IF SUSER_ID('acmp_svc') IS NULL CREATE LOGIN acmp_svc WITH PASSWORD='${SVC_PW}', CHECK_POLICY=OFF;"

# 2) db user + least-priv roles + acmp_app enrolment (database scope)
$SQLCMD -d "${DB_NAME}" -Q "
IF DATABASE_PRINCIPAL_ID('acmp_svc') IS NULL CREATE USER acmp_svc FOR LOGIN acmp_svc;
IF IS_ROLEMEMBER('db_datareader','acmp_svc') = 0 ALTER ROLE db_datareader ADD MEMBER acmp_svc;
IF IS_ROLEMEMBER('db_datawriter','acmp_svc') = 0 ALTER ROLE db_datawriter ADD MEMBER acmp_svc;
GRANT EXECUTE TO acmp_svc;
IF DATABASE_PRINCIPAL_ID('acmp_app') IS NULL CREATE ROLE acmp_app;
IF IS_ROLEMEMBER('acmp_app','acmp_svc') = 0 ALTER ROLE acmp_app ADD MEMBER acmp_svc;"

echo "sqlserver-init: acmp_svc ready (db_datareader + db_datawriter + EXECUTE + acmp_app; NOT sysadmin/db_owner)."
