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

# 3) Keycloak's own database on the SAME instance (PH-5 / ADR-0036). Only runs when the cloud
# stack mounts the keycloak_svc_password secret, so the pre-PH-5 prod overlay is unaffected.
KC_SECRET=/run/secrets/keycloak_svc_password
if [ -f "$KC_SECRET" ]; then
  KC_DB_NAME="${ACMP_KC_DB_NAME:-keycloak}"
  KC_USER="${ACMP_KC_DB_USER:-keycloak_svc}"
  KC_PW="$(cat "$KC_SECRET")"

  $SQLCMD -Q "IF DB_ID('${KC_DB_NAME}') IS NULL CREATE DATABASE [${KC_DB_NAME}];"

  # READ_COMMITTED_SNAPSHOT is MANDATORY for Keycloak on SQL Server (its docs: the default
  # READ_COMMITTED "can lead to deadlocks during high load"). The GUARD is load-bearing, not
  # decoration: ALTER DATABASE ... SET RCSI needs EXCLUSIVE access, and this script re-runs on
  # every deploy — once Keycloak's connection pool is live, an unguarded ALTER blocks until
  # those connections drop, hanging the deploy. DATABASEPROPERTYEX makes it fire only when the
  # setting is actually off (i.e. first run).
  $SQLCMD -Q "IF DATABASEPROPERTYEX('${KC_DB_NAME}','IsReadCommittedSnapshotOn') = 0
                ALTER DATABASE [${KC_DB_NAME}] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;"

  $SQLCMD -Q "IF SUSER_ID('${KC_USER}') IS NULL CREATE LOGIN ${KC_USER} WITH PASSWORD='${KC_PW}', CHECK_POLICY=OFF;"

  # db_owner, scoped to the keycloak database ONLY. Keycloak runs Liquibase schema migrations
  # on startup, so it genuinely needs DDL and cannot live under the acmp_svc least-priv model.
  # This does NOT reopen the ADR-0031 objection ("db_owner can REVOKE the DENY"): that DENY
  # protects schema::audit inside the Acmp database, where ${KC_USER} has no principal at all.
  $SQLCMD -d "${KC_DB_NAME}" -Q "
IF DATABASE_PRINCIPAL_ID('${KC_USER}') IS NULL CREATE USER ${KC_USER} FOR LOGIN ${KC_USER};
IF IS_ROLEMEMBER('db_owner','${KC_USER}') = 0 ALTER ROLE db_owner ADD MEMBER ${KC_USER};"

  rcsi="$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT DATABASEPROPERTYEX('${KC_DB_NAME}','IsReadCommittedSnapshotOn');")"
  echo "sqlserver-init: ${KC_DB_NAME} ready (RCSI=${rcsi}; ${KC_USER} db_owner on ${KC_DB_NAME} only)."
else
  echo "sqlserver-init: no keycloak_svc_password mounted — skipping the Keycloak database leg."
fi
