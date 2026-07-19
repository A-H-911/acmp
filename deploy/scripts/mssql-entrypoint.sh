#!/bin/sh
# Conditional Docker-secrets shim for SQL Server (ADR-0032). The official mssql image has NO native *_FILE
# support, so this entrypoint reads the SA password from the mounted secret file when MSSQL_SA_PASSWORD_FILE is
# set, then execs the engine. It is a NO-OP when the var is unset (plain env-var usage), so behaviour outside the
# secrets path is unchanged. `exec` keeps sqlservr as PID 1 with signals intact.
set -e
if [ -n "${MSSQL_SA_PASSWORD_FILE:-}" ] && [ -f "$MSSQL_SA_PASSWORD_FILE" ]; then
  MSSQL_SA_PASSWORD="$(cat "$MSSQL_SA_PASSWORD_FILE")"
  export MSSQL_SA_PASSWORD
fi
exec /opt/mssql/bin/sqlservr
