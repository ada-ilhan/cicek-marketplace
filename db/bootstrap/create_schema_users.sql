-- =====================================================================================================
-- Çiçek marketplace — one-time database bootstrap (run by a DBA, once per environment/PDB)
--
-- Creates the least-privilege account structure described in docs/oracle-database.md:
--   <owner>   schema owner: owns tables/packages; used ONLY by the DbMigrator during deployments
--   <role>    role holding the runtime object privileges (granted by cicek_schema_access)
--   <app>     runtime account used by the API: CREATE SESSION + <role>; owns nothing, no DDL rights
--
-- Usage (SQL*Plus or SQLcl, connected to the target PDB as a DBA account, never from application code):
--   sqlplus system@//localhost:1521/XEPDB1 @db/bootstrap/create_schema_users.sql CICEK_OWNER CICEK_APP CICEK_APP_ROLE USERS
-- Passwords are prompted (hidden) and never stored in this file. Use letters, digits and _ only;
-- they are embedded in quoted identifiers, so a double quote (") is not allowed.
--
-- Idempotent: existing users/roles are kept; privileges are (re)granted.
-- =====================================================================================================
SET VERIFY OFF
SET FEEDBACK OFF
SET SERVEROUTPUT ON
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK

DEFINE owner_user = &1
DEFINE app_user   = &2
DEFINE app_role   = &3
DEFINE data_ts    = &4

ACCEPT owner_password CHAR PROMPT 'Password for &owner_user: ' HIDE
ACCEPT app_password   CHAR PROMPT 'Password for &app_user: ' HIDE

-- The work is done by create_schema_users_impl.sql (kept next to this file) so that non-interactive deployments, which
-- already know the passwords, can define the variables themselves and call it directly (see deploy/oracle/setup).
@@create_schema_users_impl.sql
