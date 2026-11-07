using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cicek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NullableOptionalText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "POSTAL_CODE",
                table: "SHP_SHOPS",
                type: "VARCHAR2(16)",
                unicode: false,
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "VARCHAR2(16)",
                oldUnicode: false,
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "DESCRIPTION",
                table: "SHP_SHOPS",
                type: "NVARCHAR2(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "POSTAL_CODE",
                table: "ORD_ORDERS",
                type: "VARCHAR2(16)",
                unicode: false,
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "VARCHAR2(16)",
                oldUnicode: false,
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "DESCRIPTION",
                table: "CAT_PRODUCTS",
                type: "NVARCHAR2(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "ALT_TEXT",
                table: "CAT_PRODUCT_IMAGES",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(200)",
                oldMaxLength: 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "POSTAL_CODE",
                table: "SHP_SHOPS",
                type: "VARCHAR2(16)",
                unicode: false,
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "VARCHAR2(16)",
                oldUnicode: false,
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DESCRIPTION",
                table: "SHP_SHOPS",
                type: "NVARCHAR2(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "POSTAL_CODE",
                table: "ORD_ORDERS",
                type: "VARCHAR2(16)",
                unicode: false,
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "VARCHAR2(16)",
                oldUnicode: false,
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DESCRIPTION",
                table: "CAT_PRODUCTS",
                type: "NVARCHAR2(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ALT_TEXT",
                table: "CAT_PRODUCT_IMAGES",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
