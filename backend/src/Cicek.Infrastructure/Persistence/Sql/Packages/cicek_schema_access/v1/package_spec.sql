CREATE OR REPLACE PACKAGE cicek_schema_access AUTHID DEFINER AS
  /*
   * Purpose
   *   Keeps the runtime role's object privileges in sync with the objects owned by this schema.
   *   Called by the DbMigrator, as the schema owner, after migrations are applied.
   *
   * Why PL/SQL (ADR-0010)
   *   The privilege set must be derived from the data dictionary of the owning schema and issued as
   *   GRANT/REVOKE DDL by the object owner. Doing this inside the database keeps the least-privilege
   *   rules in one versioned, testable place and works for every deployment path.
   *
   * Transaction ownership
   *   GRANT and REVOKE are DDL and COMMIT implicitly. Never call this procedure inside an application
   *   transaction. It is called only by the migrator, outside any EF Core transaction.
   *
   * Error contract (RAISE_APPLICATION_ERROR, message format "<code>: <text>")
   *   -20001 schema_access.invalid_grantee    the grantee is not a valid simple SQL name
   *   -20002 schema_access.grantee_not_found  the grantee role/user does not exist
   *   -20003 schema_access.invalid_privilege  the dictionary returned an unexpected privilege name
   *
   * Privilege policy
   *   Tables      SELECT, INSERT, UPDATE, DELETE (EF_MIGRATIONS_HISTORY: SELECT only)
   *   Views       SELECT
   *   Sequences   SELECT (system-generated identity sequences excluded)
   *   PL/SQL      EXECUTE (this package excluded: it is deployment-only)
   *   Privileges outside this policy that were granted earlier are revoked.
   */

  c_err_invalid_grantee   CONSTANT PLS_INTEGER := -20001;
  c_err_grantee_not_found CONSTANT PLS_INTEGER := -20002;
  c_err_invalid_privilege CONSTANT PLS_INTEGER := -20003;

  -- p_grantee:  role (normally CICEK_APP_ROLE) receiving the privileges.
  -- p_changes:  number of GRANT/REVOKE statements executed (0 when already in sync).
  PROCEDURE grant_runtime_privileges(
    p_grantee IN  VARCHAR2,
    p_changes OUT PLS_INTEGER);

END cicek_schema_access;
