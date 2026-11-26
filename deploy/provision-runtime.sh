#!/usr/bin/env bash
set -euo pipefail
# SQL literals are permitted only after validating this restricted password alphabet.
if [[ ! ${ERP_DB_PASSWORD:-} =~ ^[A-Za-z0-9@%_+=:,.-]{16,128}$ ]]; then
  echo 'ERP_DB_PASSWORD must contain 16-128 letters, digits or @%_+=:,.- characters.' >&2
  exit 1
fi
export SQLCMDPASSWORD="${MSSQL_SA_PASSWORD:?Set MSSQL_SA_PASSWORD}"
/opt/mssql-tools18/bin/sqlcmd -S sql -U sa -C -b <<SQL
IF SUSER_ID(N'minierp_app') IS NULL
    CREATE LOGIN minierp_app WITH PASSWORD=N'$ERP_DB_PASSWORD', CHECK_POLICY=ON;
ELSE
    ALTER LOGIN minierp_app WITH PASSWORD=N'$ERP_DB_PASSWORD';
GO
USE MiniErp;
IF DATABASE_PRINCIPAL_ID(N'minierp_app') IS NULL CREATE USER minierp_app FOR LOGIN minierp_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::auth TO minierp_app;
GRANT SELECT, INSERT, UPDATE ON SCHEMA::catalog TO minierp_app;
GRANT SELECT, INSERT, UPDATE ON SCHEMA::inventory TO minierp_app;
GRANT SELECT, INSERT, UPDATE ON SCHEMA::orders TO minierp_app;
GRANT SELECT, INSERT ON SCHEMA::finance TO minierp_app;
GRANT SELECT, INSERT ON SCHEMA::audit TO minierp_app;
GRANT SELECT, EXECUTE ON SCHEMA::reporting TO minierp_app;
GRANT SELECT ON dbo.__EFMigrationsHistory TO minierp_app;
GO
SQL
