using System;
using Cicek.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cicek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IDN_ROLES",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    NORMALIZED_NAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    CONCURRENCY_STAMP = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IDN_ROLES", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "IDN_USERS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    USER_NAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    NORMALIZED_USER_NAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    NORMALIZED_EMAIL = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    EMAIL_CONFIRMED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    PASSWORD_HASH = table.Column<string>(type: "VARCHAR2(512)", unicode: false, maxLength: 512, nullable: true),
                    SECURITY_STAMP = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: true),
                    CONCURRENCY_STAMP = table.Column<string>(type: "VARCHAR2(64)", unicode: false, maxLength: 64, nullable: true),
                    PHONE_NUMBER = table.Column<string>(type: "VARCHAR2(32)", unicode: false, maxLength: 32, nullable: true),
                    PHONE_NUMBER_CONFIRMED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TWO_FACTOR_ENABLED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    LOCKOUT_END = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: true),
                    LOCKOUT_ENABLED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ACCESS_FAILED_COUNT = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IDN_USERS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "IDN_ROLE_CLAIMS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ROLE_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CLAIM_TYPE = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    CLAIM_VALUE = table.Column<string>(type: "NVARCHAR2(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IDN_ROLE_CLAIMS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_IDN_ROLE_CLAIMS_IDN_ROLES_ROLE_ID",
                        column: x => x.ROLE_ID,
                        principalTable: "IDN_ROLES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IDN_REFRESH_SESSIONS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    USER_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    FAMILY_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    TOKEN_HASH = table.Column<string>(type: "CHAR(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    EXPIRES_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    FAMILY_EXPIRES_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    REVOKED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: true),
                    REVOCATION_REASON = table.Column<string>(type: "VARCHAR2(32)", unicode: false, maxLength: 32, nullable: true),
                    REPLACED_BY_SESSION_ID = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    ROW_VERSION = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IDN_REFRESH_SESSIONS", x => x.ID);
                    table.CheckConstraint("CK_IDN_REFRESH_SESSIONS_REASON", "REVOCATION_REASON IS NULL OR REVOCATION_REASON IN (''Rotated'', ''LoggedOut'', ''ReuseDetected'', ''AdministrativeAction'')");
                    table.ForeignKey(
                        name: "FK_IDN_REFRESH_SESSIONS_IDN_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "IDN_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IDN_USER_CLAIMS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    USER_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CLAIM_TYPE = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    CLAIM_VALUE = table.Column<string>(type: "NVARCHAR2(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IDN_USER_CLAIMS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_IDN_USER_CLAIMS_IDN_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "IDN_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IDN_USER_LOGINS",
                columns: table => new
                {
                    LOGIN_PROVIDER = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    PROVIDER_KEY = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    PROVIDER_DISPLAY_NAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    USER_ID = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IDN_USER_LOGINS", x => new { x.LOGIN_PROVIDER, x.PROVIDER_KEY });
                    table.ForeignKey(
                        name: "FK_IDN_USER_LOGINS_IDN_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "IDN_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IDN_USER_ROLES",
                columns: table => new
                {
                    USER_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ROLE_ID = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IDN_USER_ROLES", x => new { x.USER_ID, x.ROLE_ID });
                    table.ForeignKey(
                        name: "FK_IDN_USER_ROLES_IDN_ROLES_ROLE_ID",
                        column: x => x.ROLE_ID,
                        principalTable: "IDN_ROLES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IDN_USER_ROLES_IDN_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "IDN_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IDN_USER_TOKENS",
                columns: table => new
                {
                    USER_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    LOGIN_PROVIDER = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    VALUE = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IDN_USER_TOKENS", x => new { x.USER_ID, x.LOGIN_PROVIDER, x.NAME });
                    table.ForeignKey(
                        name: "FK_IDN_USER_TOKENS_IDN_USERS_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "IDN_USERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IDN_REFRESH_SESSIONS_FAMILY_ID",
                table: "IDN_REFRESH_SESSIONS",
                column: "FAMILY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_IDN_REFRESH_SESSIONS_USER_ID",
                table: "IDN_REFRESH_SESSIONS",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "UX_IDN_REFRESH_SESSIONS_TOKEN_HASH",
                table: "IDN_REFRESH_SESSIONS",
                column: "TOKEN_HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IDN_ROLE_CLAIMS_ROLE_ID",
                table: "IDN_ROLE_CLAIMS",
                column: "ROLE_ID");

            migrationBuilder.CreateIndex(
                name: "UX_IDN_ROLES_NORMALIZED_NAME",
                table: "IDN_ROLES",
                column: "NORMALIZED_NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IDN_USER_CLAIMS_USER_ID",
                table: "IDN_USER_CLAIMS",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_IDN_USER_LOGINS_USER_ID",
                table: "IDN_USER_LOGINS",
                column: "USER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_IDN_USER_ROLES_ROLE_ID",
                table: "IDN_USER_ROLES",
                column: "ROLE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_IDN_USERS_NORMALIZED_EMAIL",
                table: "IDN_USERS",
                column: "NORMALIZED_EMAIL");

            migrationBuilder.CreateIndex(
                name: "UX_IDN_USERS_NORMALIZED_USER_NAME",
                table: "IDN_USERS",
                column: "NORMALIZED_USER_NAME",
                unique: true);

            // PL/SQL: runtime privilege synchronization (ADR-0010). Versioned source, embedded resource.
            migrationBuilder.Sql(SqlResources.Read(SqlResources.SchemaAccessSpecV1));
            migrationBuilder.Sql(SqlResources.Read(SqlResources.SchemaAccessBodyV1));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PACKAGE cicek_schema_access");

            migrationBuilder.DropTable(
                name: "IDN_REFRESH_SESSIONS");

            migrationBuilder.DropTable(
                name: "IDN_ROLE_CLAIMS");

            migrationBuilder.DropTable(
                name: "IDN_USER_CLAIMS");

            migrationBuilder.DropTable(
                name: "IDN_USER_LOGINS");

            migrationBuilder.DropTable(
                name: "IDN_USER_ROLES");

            migrationBuilder.DropTable(
                name: "IDN_USER_TOKENS");

            migrationBuilder.DropTable(
                name: "IDN_ROLES");

            migrationBuilder.DropTable(
                name: "IDN_USERS");
        }
    }
}
