-- =====================================================================================================
-- Çiçek marketplace — database bootstrap implementation. Do not run directly unless the substitution variables
-- owner_user, app_user, app_role, data_ts, owner_password and app_password are already DEFINEd in the session
-- (create_schema_users.sql prompts for them and calls this script). Idempotent.
-- =====================================================================================================
SET VERIFY OFF
SET FEEDBACK OFF
SET SERVEROUTPUT ON
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
DECLARE
  l_owner VARCHAR2(128) := UPPER(DBMS_ASSERT.SIMPLE_SQL_NAME('&owner_user'));
  l_app   VARCHAR2(128) := UPPER(DBMS_ASSERT.SIMPLE_SQL_NAME('&app_user'));
  l_role  VARCHAR2(128) := UPPER(DBMS_ASSERT.SIMPLE_SQL_NAME('&app_role'));
  l_ts    VARCHAR2(128) := UPPER(DBMS_ASSERT.SIMPLE_SQL_NAME('&data_ts'));
  l_count PLS_INTEGER;

  PROCEDURE exec(p_sql IN VARCHAR2, p_log IN VARCHAR2) IS
  BEGIN
    EXECUTE IMMEDIATE p_sql;
    DBMS_OUTPUT.PUT_LINE(p_log);
  END exec;
BEGIN
  IF INSTR('&owner_password', '"') > 0 OR INSTR('&app_password', '"') > 0 THEN
    RAISE_APPLICATION_ERROR(-20900, 'bootstrap.invalid_password: Passwords must not contain double quotes.');
  END IF;

  -- Schema owner -------------------------------------------------------------------------------------
  SELECT COUNT(*) INTO l_count FROM dba_users WHERE username = l_owner;
  IF l_count = 0 THEN
    exec('CREATE USER ' || l_owner || ' IDENTIFIED BY "&owner_password" DEFAULT TABLESPACE ' || l_ts ||
         ' QUOTA UNLIMITED ON ' || l_ts, 'Created user ' || l_owner);
  ELSE
    DBMS_OUTPUT.PUT_LINE('User ' || l_owner || ' exists; password unchanged.');
  END IF;
  -- Only what migrations need: tables, sequences, views and PL/SQL. No ANY privileges, no triggers.
  exec('GRANT CREATE SESSION, CREATE TABLE, CREATE SEQUENCE, CREATE VIEW, CREATE PROCEDURE TO ' || l_owner,
       'Granted owner system privileges');

  -- Runtime role -------------------------------------------------------------------------------------
  SELECT COUNT(*) INTO l_count FROM dba_roles WHERE role = l_role;
  IF l_count = 0 THEN
    exec('CREATE ROLE ' || l_role, 'Created role ' || l_role);
  END IF;

  -- Runtime account ----------------------------------------------------------------------------------
  SELECT COUNT(*) INTO l_count FROM dba_users WHERE username = l_app;
  IF l_count = 0 THEN
    exec('CREATE USER ' || l_app || ' IDENTIFIED BY "&app_password" DEFAULT TABLESPACE ' || l_ts ||
         ' QUOTA 0 ON ' || l_ts, 'Created user ' || l_app);
  ELSE
    DBMS_OUTPUT.PUT_LINE('User ' || l_app || ' exists; password unchanged.');
  END IF;
  exec('GRANT CREATE SESSION TO ' || l_app, 'Granted CREATE SESSION to ' || l_app);
  exec('GRANT ' || l_role || ' TO ' || l_app, 'Granted ' || l_role || ' to ' || l_app);
  exec('ALTER USER ' || l_app || ' DEFAULT ROLE ALL', 'Default roles enabled for ' || l_app);
END;
/

PROMPT Bootstrap complete. Next: run the DbMigrator as &owner_user (see README).
UNDEFINE owner_password app_password
EXIT SUCCESS
