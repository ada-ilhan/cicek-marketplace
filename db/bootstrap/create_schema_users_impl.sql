-- =====================================================================================================
-- Çiçek marketplace — database bootstrap implementation. Do not run directly unless the substitution variables
-- owner_user, app_user, app_role, data_ts, owner_password and app_password are already DEFINEd in the session
-- (create_schema_users.sql prompts for them and calls this script). Idempotent.
--
-- Data tablespace (only resolved when a user has to be created; existing users are never altered):
--   * data_ts exists, is PERMANENT and ONLINE  -> used (Oracle Database Free, XE 21c: USERS).
--   * data_ts is SYSTEM or SYSAUX              -> rejected (ORA-20902); application objects never go there.
--   * data_ts exists but is not PERMANENT/ONLINE (offline, read only, undo, temporary) -> rejected (ORA-20903).
--   * data_ts does not exist (e.g. the Free "lite" image has no USERS) -> the database default permanent tablespace
--     (DEFAULT_PERMANENT_TABLESPACE of the current container) is used if it is PERMANENT, ONLINE and not SYSTEM/SYSAUX;
--     otherwise ORA-20901 bootstrap.no_data_tablespace. Run it connected to the target PDB.
-- =====================================================================================================
SET VERIFY OFF
SET FEEDBACK OFF
SET SERVEROUTPUT ON
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
DECLARE
  l_owner VARCHAR2(128) := UPPER(DBMS_ASSERT.SIMPLE_SQL_NAME('&owner_user'));
  l_app   VARCHAR2(128) := UPPER(DBMS_ASSERT.SIMPLE_SQL_NAME('&app_user'));
  l_role  VARCHAR2(128) := UPPER(DBMS_ASSERT.SIMPLE_SQL_NAME('&app_role'));
  l_ts    VARCHAR2(128) := UPPER(DBMS_ASSERT.SIMPLE_SQL_NAME('&data_ts'));  -- requested tablespace
  l_ts_sql VARCHAR2(130);                                                    -- selected tablespace, quoted for SQL
  l_count PLS_INTEGER;

  PROCEDURE exec(p_sql IN VARCHAR2, p_log IN VARCHAR2) IS
  BEGIN
    EXECUTE IMMEDIATE p_sql;
    DBMS_OUTPUT.PUT_LINE(p_log);
  END exec;

  -- CONTENTS and STATUS of a tablespace of the current container; both NULL when it does not exist.
  PROCEDURE inspect_tablespace(p_name IN VARCHAR2, p_contents OUT VARCHAR2, p_status OUT VARCHAR2) IS
  BEGIN
    SELECT contents, status INTO p_contents, p_status FROM dba_tablespaces WHERE tablespace_name = p_name;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN
      p_contents := NULL;
      p_status := NULL;
  END inspect_tablespace;

  -- Chooses the tablespace for the new users once, before the first CREATE USER (see the header for the rules).
  PROCEDURE ensure_data_tablespace IS
    l_selected VARCHAR2(128);
    l_default  VARCHAR2(128);
    l_contents VARCHAR2(9);
    l_status   VARCHAR2(9);
  BEGIN
    IF l_ts_sql IS NOT NULL THEN
      RETURN;
    END IF;
    IF l_ts IN ('SYSTEM', 'SYSAUX') THEN
      RAISE_APPLICATION_ERROR(-20902, 'bootstrap.forbidden_tablespace: ' || l_ts ||
        ' must not hold application objects; pass a dedicated permanent tablespace.');
    END IF;

    inspect_tablespace(l_ts, l_contents, l_status);
    IF l_contents IS NOT NULL THEN
      IF l_contents <> 'PERMANENT' OR l_status <> 'ONLINE' THEN
        RAISE_APPLICATION_ERROR(-20903, 'bootstrap.unsuitable_tablespace: ' || l_ts || ' is ' || l_contents || '/' ||
          l_status || '; it must be a permanent, online tablespace.');
      END IF;
      l_selected := l_ts;
    ELSE
      SELECT MAX(property_value) INTO l_default FROM database_properties WHERE property_name = 'DEFAULT_PERMANENT_TABLESPACE';
      IF l_default IS NOT NULL THEN
        inspect_tablespace(l_default, l_contents, l_status);
      END IF;
      IF l_default IS NULL OR l_default IN ('SYSTEM', 'SYSAUX') OR l_contents IS NULL
         OR l_contents <> 'PERMANENT' OR l_status <> 'ONLINE' THEN
        RAISE_APPLICATION_ERROR(-20901, 'bootstrap.no_data_tablespace: tablespace ' || l_ts ||
          ' does not exist and the database has no usable default permanent tablespace (found: ' ||
          NVL(l_default, 'none') || '). Create a dedicated permanent tablespace and pass its name.');
      END IF;
      l_selected := l_default;
      DBMS_OUTPUT.PUT_LINE('Tablespace ' || l_ts || ' not found; using the database default permanent tablespace ' || l_selected);
    END IF;

    l_ts_sql := DBMS_ASSERT.ENQUOTE_NAME(l_selected, FALSE);
    DBMS_OUTPUT.PUT_LINE('Data tablespace for new users: ' || l_selected);
  END ensure_data_tablespace;
BEGIN
  IF INSTR('&owner_password', '"') > 0 OR INSTR('&app_password', '"') > 0 THEN
    RAISE_APPLICATION_ERROR(-20900, 'bootstrap.invalid_password: Passwords must not contain double quotes.');
  END IF;

  -- Schema owner -------------------------------------------------------------------------------------
  SELECT COUNT(*) INTO l_count FROM dba_users WHERE username = l_owner;
  IF l_count = 0 THEN
    ensure_data_tablespace;
    exec('CREATE USER ' || l_owner || ' IDENTIFIED BY "&owner_password" DEFAULT TABLESPACE ' || l_ts_sql ||
         ' QUOTA UNLIMITED ON ' || l_ts_sql, 'Created user ' || l_owner);
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
    ensure_data_tablespace;
    exec('CREATE USER ' || l_app || ' IDENTIFIED BY "&app_password" DEFAULT TABLESPACE ' || l_ts_sql ||
         ' QUOTA 0 ON ' || l_ts_sql, 'Created user ' || l_app);
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
