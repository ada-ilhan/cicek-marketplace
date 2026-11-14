-- =====================================================================================================
-- Çiçek marketplace — OPTIONAL, idempotent provisioning of the application data tablespace (development, CI, containers).
--
-- Some Oracle images (e.g. Oracle Database Free "lite") ship a PDB without a USERS tablespace and with SYSTEM as the default
-- permanent tablespace. create_schema_users_impl.sql refuses to put application objects there, so a dedicated tablespace
-- is needed. Production DBAs normally manage storage themselves and do NOT run this script; the bootstrap keeps failing safe.
--
-- Call it BEFORE create_schema_users_impl.sql, connected as a DBA to the target PDB, with the substitution variable data_ts
-- already DEFINEd (the same variable the user bootstrap uses; all callers pass USERS). It must not end the session (no EXIT).
--
--   * Container check: refuses to run in CDB$ROOT or PDB$SEED (CON_ID 1 or 2). Non-CDB databases (CON_ID 0) are accepted.
--   * SYSTEM / SYSAUX are rejected as data_ts.
--   * data_ts already exists -> it must be PERMANENT and ONLINE, otherwise the script fails; if it is fine nothing is done.
--   * data_ts missing       -> one datafile is created next to the SYSAUX datafile of the CURRENT container (directory taken
--     from dba_data_files, never hardcoded; no Oracle Managed Files needed): <dir><data_ts lowercase>01.dbf, 100 MB, growing in
--     50 MB steps up to 2 GB. The statement has no REUSE clause, so Oracle itself refuses to overwrite an existing file;
--     registered datafiles/tempfiles with the same name are detected first for a clear message.
--   * ASM disk groups (paths starting with +) are not supported here: create the tablespace manually.
-- Errors: ORA-20910 wrong container, 20911 forbidden name, 20912 unsuitable existing tablespace, 20913 cannot derive the
-- directory, 20914 ASM, 20915 datafile name already in use. Any other Oracle error propagates unchanged.
-- =====================================================================================================
SET VERIFY OFF
SET FEEDBACK OFF
SET SERVEROUTPUT ON
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
DECLARE
  c_initial CONSTANT VARCHAR2(10) := '100M';
  c_next    CONSTANT VARCHAR2(10) := '50M';
  c_max     CONSTANT VARCHAR2(10) := '2G';
  l_ts       VARCHAR2(128) := UPPER(DBMS_ASSERT.SIMPLE_SQL_NAME('&data_ts'));
  l_con_name VARCHAR2(128) := SYS_CONTEXT('USERENV', 'CON_NAME');
  l_con_id   PLS_INTEGER   := TO_NUMBER(SYS_CONTEXT('USERENV', 'CON_ID'));
  l_contents VARCHAR2(9);
  l_status   VARCHAR2(9);
  l_sysaux   VARCHAR2(513);
  l_pos      PLS_INTEGER;
  l_file     VARCHAR2(600);
  l_count    PLS_INTEGER;

  -- CONTENTS and STATUS of a tablespace of the current container; both NULL when it does not exist.
  PROCEDURE inspect_tablespace(p_name IN VARCHAR2, p_contents OUT VARCHAR2, p_status OUT VARCHAR2) IS
  BEGIN
    SELECT contents, status INTO p_contents, p_status FROM dba_tablespaces WHERE tablespace_name = p_name;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN
      p_contents := NULL;
      p_status := NULL;
  END inspect_tablespace;

  PROCEDURE require_usable(p_contents IN VARCHAR2, p_status IN VARCHAR2) IS
  BEGIN
    IF p_contents <> 'PERMANENT' OR p_status <> 'ONLINE' THEN
      RAISE_APPLICATION_ERROR(-20912, 'bootstrap.unsuitable_tablespace: ' || l_ts || ' is ' || p_contents || '/' || p_status ||
        '; it must be a permanent, online tablespace.');
    END IF;
  END require_usable;
BEGIN
  IF l_con_id IN (1, 2) THEN
    RAISE_APPLICATION_ERROR(-20910, 'bootstrap.wrong_container: connected to ' || l_con_name ||
      ' (CON_ID ' || l_con_id || '); run this script inside the target PDB (ALTER SESSION SET CONTAINER).');
  END IF;
  DBMS_OUTPUT.PUT_LINE('Container: ' || l_con_name || ' (CON_ID ' || l_con_id || ')');

  IF l_ts IN ('SYSTEM', 'SYSAUX') THEN
    RAISE_APPLICATION_ERROR(-20911, 'bootstrap.forbidden_tablespace: ' || l_ts || ' must not be provisioned as the data tablespace.');
  END IF;

  inspect_tablespace(l_ts, l_contents, l_status);
  IF l_contents IS NOT NULL THEN
    require_usable(l_contents, l_status);
    DBMS_OUTPUT.PUT_LINE('Tablespace ' || l_ts || ' already exists (' || l_contents || '/' || l_status || '); nothing to do.');
    RETURN;
  END IF;

  -- Directory of the SYSAUX datafile of the current container: where Oracle already writes that container files.
  SELECT MIN(file_name) INTO l_sysaux FROM dba_data_files WHERE tablespace_name = 'SYSAUX';
  IF l_sysaux IS NULL THEN
    RAISE_APPLICATION_ERROR(-20913, 'bootstrap.no_datafile_directory: no SYSAUX datafile found in container ' || l_con_name || '.');
  END IF;
  IF SUBSTR(l_sysaux, 1, 1) = '+' THEN
    RAISE_APPLICATION_ERROR(-20914, 'bootstrap.asm_not_supported: SYSAUX lives in ASM (' || l_sysaux ||
      '); create the ' || l_ts || ' tablespace manually.');
  END IF;
  l_pos := GREATEST(INSTR(l_sysaux, '/', -1), INSTR(l_sysaux, '\', -1));
  IF l_pos = 0 THEN
    RAISE_APPLICATION_ERROR(-20913, 'bootstrap.no_datafile_directory: cannot derive a directory from ' || l_sysaux || '.');
  END IF;
  l_file := SUBSTR(l_sysaux, 1, l_pos) || LOWER(l_ts) || '01.dbf';

  SELECT COUNT(*) INTO l_count
    FROM (SELECT file_name AS f FROM dba_data_files UNION ALL SELECT file_name FROM dba_temp_files)
   WHERE LOWER(f) = LOWER(l_file);
  IF l_count > 0 THEN
    RAISE_APPLICATION_ERROR(-20915, 'bootstrap.datafile_in_use: ' || l_file || ' already belongs to this database.');
  END IF;

  -- No REUSE: an existing file on disk makes Oracle fail instead of being overwritten.
  EXECUTE IMMEDIATE 'CREATE SMALLFILE TABLESPACE ' || DBMS_ASSERT.ENQUOTE_NAME(l_ts, FALSE) ||
                    ' DATAFILE ' || DBMS_ASSERT.ENQUOTE_LITERAL(l_file) ||
                    ' SIZE ' || c_initial || ' AUTOEXTEND ON NEXT ' || c_next || ' MAXSIZE ' || c_max;

  inspect_tablespace(l_ts, l_contents, l_status);
  IF l_contents IS NULL THEN
    RAISE_APPLICATION_ERROR(-20912, 'bootstrap.unsuitable_tablespace: ' || l_ts || ' was not found after creation.');
  END IF;
  require_usable(l_contents, l_status);
  DBMS_OUTPUT.PUT_LINE('Created tablespace ' || l_ts || ' in ' || l_con_name || ': ' || l_file ||
                       ' (initial ' || c_initial || ', next ' || c_next || ', max ' || c_max || ')');
END;
/
