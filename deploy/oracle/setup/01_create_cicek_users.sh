#!/bin/bash
# Executed by the Oracle Database Free image on every container start (/opt/oracle/scripts/startup). Idempotent: existing
# users and roles are kept and privileges are re-granted.
# Runs the same bootstrap script DBAs use (db/bootstrap), against the pluggable database named by ORACLE_PDB (FREEPDB1 for Oracle Free, XEPDB1 for XE 21c).
set -euo pipefail

if [[ -z "${CICEK_OWNER_PASSWORD:-}" || -z "${CICEK_APP_PASSWORD:-}" ]]; then
  echo "CICEK_OWNER_PASSWORD and CICEK_APP_PASSWORD must be set" >&2
  exit 1
fi

# The interactive bootstrap reads hidden passwords from the terminal, which does not exist here. Define the variables and
# call the implementation directly. The temporary file (mode 600, removed on exit) is the only place the passwords are written.
umask 077
wrapper="$(mktemp --suffix=.sql)"
trap 'rm -f "$wrapper"' EXIT
cat > "$wrapper" <<SQL
ALTER SESSION SET CONTAINER=${ORACLE_PDB:-FREEPDB1};
DEFINE owner_user = CICEK_OWNER
DEFINE app_user   = CICEK_APP
DEFINE app_role   = CICEK_APP_ROLE
DEFINE data_ts    = USERS
DEFINE owner_password = "${CICEK_OWNER_PASSWORD}"
DEFINE app_password   = "${CICEK_APP_PASSWORD}"
@/opt/cicek/bootstrap/create_schema_users_impl.sql
SQL

sqlplus -S -L "/ as sysdba" "@$wrapper"
