CREATE OR REPLACE PACKAGE BODY cicek_schema_access AS

  c_package_name  CONSTANT VARCHAR2(128) := 'CICEK_SCHEMA_ACCESS';
  c_history_table CONSTANT VARCHAR2(128) := 'EF_MIGRATIONS_HISTORY';

  e_invalid_sql_name  EXCEPTION;
  PRAGMA EXCEPTION_INIT(e_invalid_sql_name, -44003);
  e_grantee_not_found EXCEPTION;
  PRAGMA EXCEPTION_INIT(e_grantee_not_found, -1917);
  e_role_not_found    EXCEPTION;
  PRAGMA EXCEPTION_INIT(e_role_not_found, -1919);

  -- Privilege names are concatenated into DDL, so accept only plain upper-case words.
  FUNCTION assert_privilege(p_privilege IN VARCHAR2) RETURN VARCHAR2 IS
  BEGIN
    IF p_privilege IS NULL OR NOT REGEXP_LIKE(p_privilege, '^[A-Z]+( [A-Z]+)*$') THEN
      RAISE_APPLICATION_ERROR(
        c_err_invalid_privilege,
        'schema_access.invalid_privilege: Unexpected privilege name.');
    END IF;
    RETURN p_privilege;
  END assert_privilege;

  FUNCTION desired_table_privileges(p_table_name IN VARCHAR2) RETURN VARCHAR2 IS
  BEGIN
    IF p_table_name = c_history_table THEN
      RETURN 'SELECT';
    END IF;
    -- v2: audit trails are append-only for the application; history can never be rewritten.
    IF p_table_name IN ('AUD_AUDIT_ENTRIES', 'ORD_ORDER_STATUS_HISTORY') THEN
      RETURN 'SELECT,INSERT';
    END IF;
    RETURN 'SELECT,INSERT,UPDATE,DELETE';
  END desired_table_privileges;

  FUNCTION contains_privilege(p_list IN VARCHAR2, p_privilege IN VARCHAR2) RETURN BOOLEAN IS
  BEGIN
    RETURN INSTR(',' || p_list || ',', ',' || p_privilege || ',') > 0;
  END contains_privilege;

  -- Brings one object's grants for p_grantee to exactly p_desired (comma-separated list).
  PROCEDURE sync_object(
    p_object_name IN            VARCHAR2,
    p_desired     IN            VARCHAR2,
    p_grantee     IN            VARCHAR2,
    p_changes     IN OUT NOCOPY PLS_INTEGER)
  IS
    l_object  CONSTANT VARCHAR2(130) := DBMS_ASSERT.ENQUOTE_NAME(p_object_name, FALSE);
    l_grantee CONSTANT VARCHAR2(130) := DBMS_ASSERT.ENQUOTE_NAME(p_grantee, FALSE);
    l_current VARCHAR2(4000) := ',';
    l_priv    VARCHAR2(40);
    l_index   PLS_INTEGER := 1;
  BEGIN
    FOR r IN (
      SELECT privilege
        FROM user_tab_privs_made
       WHERE grantee = p_grantee
         AND table_name = p_object_name)
    LOOP
      l_current := l_current || r.privilege || ',';
      IF NOT contains_privilege(p_desired, r.privilege) THEN
        EXECUTE IMMEDIATE 'REVOKE ' || assert_privilege(r.privilege) || ' ON ' || l_object || ' FROM ' || l_grantee;
        p_changes := p_changes + 1;
      END IF;
    END LOOP;

    LOOP
      l_priv := REGEXP_SUBSTR(p_desired, '[^,]+', 1, l_index);
      EXIT WHEN l_priv IS NULL;
      IF NOT contains_privilege(l_current, l_priv) THEN
        EXECUTE IMMEDIATE 'GRANT ' || assert_privilege(l_priv) || ' ON ' || l_object || ' TO ' || l_grantee;
        p_changes := p_changes + 1;
      END IF;
      l_index := l_index + 1;
    END LOOP;
  END sync_object;

  PROCEDURE grant_runtime_privileges(
    p_grantee IN  VARCHAR2,
    p_changes OUT PLS_INTEGER)
  IS
    l_grantee VARCHAR2(128);
  BEGIN
    BEGIN
      l_grantee := UPPER(DBMS_ASSERT.SIMPLE_SQL_NAME(TRIM(p_grantee)));
    EXCEPTION
      WHEN e_invalid_sql_name OR VALUE_ERROR THEN
        RAISE_APPLICATION_ERROR(
          c_err_invalid_grantee,
          'schema_access.invalid_grantee: The grantee must be a simple SQL name.');
    END;

    p_changes := 0;

    FOR t IN (
      SELECT table_name
        FROM user_tables
       WHERE dropped = 'NO'
         AND nested = 'NO'
         AND secondary = 'N'
         AND (iot_type IS NULL OR iot_type = 'IOT')
       ORDER BY table_name)
    LOOP
      sync_object(t.table_name, desired_table_privileges(t.table_name), l_grantee, p_changes);
    END LOOP;

    FOR v IN (SELECT view_name FROM user_views ORDER BY view_name) LOOP
      sync_object(v.view_name, 'SELECT', l_grantee, p_changes);
    END LOOP;

    FOR s IN (
      SELECT sequence_name
        FROM user_sequences
       WHERE sequence_name NOT LIKE 'ISEQ$$\_%' ESCAPE '\'
       ORDER BY sequence_name)
    LOOP
      sync_object(s.sequence_name, 'SELECT', l_grantee, p_changes);
    END LOOP;

    FOR p IN (
      SELECT object_name
        FROM user_objects
       WHERE object_type IN ('PACKAGE', 'PROCEDURE', 'FUNCTION')
         AND object_name <> c_package_name
       ORDER BY object_name)
    LOOP
      sync_object(p.object_name, 'EXECUTE', l_grantee, p_changes);
    END LOOP;
  EXCEPTION
    WHEN e_grantee_not_found OR e_role_not_found THEN
      RAISE_APPLICATION_ERROR(
        c_err_grantee_not_found,
        'schema_access.grantee_not_found: Grantee ' || l_grantee || ' does not exist.');
  END grant_runtime_privileges;

END cicek_schema_access;
